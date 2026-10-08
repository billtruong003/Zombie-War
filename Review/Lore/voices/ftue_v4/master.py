# VO mastering so every line plays at the same perceived level:
# trim -> 70 Hz high-pass -> gentle compressor (3:1, 5/120 ms) -> -18 LUFS -> 2 ms look-ahead limiter at -1 dBFS
# -> re-normalise -> verify. Usage: python master.py <verify.json> <picks.json|-> <src dir> <dst dir>
import json, sys, os, numpy as np, librosa, soundfile as sf, pyloudnorm as pyln, warnings
from scipy.signal import butter, sosfilt
warnings.filterwarnings("ignore")
SR, TARGET, CEIL = 44100, -18.0, 10 ** (-1 / 20)
meter = pyln.Meter(SR)

def envelope(x, att, rel):
    a, r = np.exp(-1 / (att * SR)), np.exp(-1 / (rel * SR)); e = np.zeros_like(x); v = 0.0
    for i, s in enumerate(np.abs(x)):
        c = a if s > v else r; v = c * v + (1 - c) * s; e[i] = v
    return e

def compress(y, ratio=3.0, att=0.005, rel=0.12):
    env = envelope(y, att, rel); db = 20 * np.log10(env + 1e-9)
    thr = np.percentile(db[db > db.max() - 40], 70)          # compress the loudest ~30% of speech
    gain_db = np.where(db > thr, (thr - db) * (1 - 1 / ratio), 0.0)
    return y * 10 ** (gain_db / 20)

def limit(y, look=0.002):
    n = int(look * SR); pk = np.abs(y)
    need = np.maximum(pk / CEIL, 1.0)
    win = np.lib.stride_tricks.sliding_window_view(np.pad(need, (0, n)), n + 1).max(axis=1)[: len(y)]
    g = 1 / win; sm = np.copy(g)
    r = np.exp(-1 / (0.05 * SR))
    for i in range(1, len(g)): sm[i] = g[i] if g[i] < sm[i - 1] else r * sm[i - 1] + (1 - r) * g[i]
    return y * sm

def process(path):
    y, _ = librosa.load(path, sr=SR, mono=True)
    db = 20 * np.log10(librosa.feature.rms(y=y, frame_length=2048, hop_length=256)[0] + 1e-9)
    on = np.where(db > db.max() - 35)[0]
    y = y[max(0, on[0] * 256 - int(0.12 * SR)): min(len(y), on[-1] * 256 + 2048 + int(0.2 * SR))].copy()
    y = sosfilt(butter(2, 70, "hp", fs=SR, output="sos"), y)
    y = compress(y)
    for _ in range(2):
        y = pyln.normalize.loudness(y, meter.integrated_loudness(y), TARGET)
        y = limit(y)
    f = int(0.01 * SR); y[:f] *= np.linspace(0, 1, f); y[-f:] *= np.linspace(1, 0, f)
    return y

def short_term_max(y):
    w = int(0.4 * SR); h = int(0.1 * SR); best = -99
    for s in range(0, max(1, len(y) - w), h):
        seg = y[s:s + w]
        if len(seg) >= w: best = max(best, meter.integrated_loudness(np.pad(seg, (0, int(0.4 * SR)))) + 3.0)
    return best

if __name__ == "__main__":
    rep = json.load(open(sys.argv[1], encoding="utf-8"))
    picks = json.load(open(sys.argv[2])) if sys.argv[2] != "-" else {}
    src, dst = sys.argv[3], sys.argv[4]; os.makedirs(dst, exist_ok=True); rows = []
    for r in rep:
        t = picks.get(r["id"], r.get("best_nat", r.get("best")))
        file = next(x["file"] for x in r["takes"] if x["t"] == t)
        y = process(os.path.join(src, file))
        sf.write(os.path.join(dst, r["id"] + ".wav"), y.astype(np.float32), SR, subtype="PCM_16")
        y2, _ = sf.read(os.path.join(dst, r["id"] + ".wav"))
        rows.append(dict(id=r["id"], agent=r["agent"], take=t, lufs=round(meter.integrated_loudness(y2), 2),
                         peak=round(20 * np.log10(np.abs(y2).max()), 2), st_max=round(short_term_max(y2), 1), dur=round(len(y2) / SR, 2)))
    json.dump(rows, open(os.path.join(dst, "loudness.json"), "w"), indent=1)
    L = np.array([x["lufs"] for x in rows]); P = np.array([x["peak"] for x in rows]); S = np.array([x["st_max"] for x in rows])
    print(f"{len(rows)} files  LUFS {L.min():.2f}..{L.max():.2f} (spread {L.max()-L.min():.2f} LU)  peak max {P.max():.2f} dBFS  short-term max {S.min():.1f}..{S.max():.1f}")
    for a in sorted({x['agent'] for x in rows}):
        aa = [x for x in rows if x["agent"] == a]
        print(f"  {a:6s} LUFS avg {np.mean([x['lufs'] for x in aa]):.2f}  short-term max avg {np.mean([x['st_max'] for x in aa]):.1f}")
