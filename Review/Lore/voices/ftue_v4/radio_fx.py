# Radio layer on top of the mastered clean VO. Keeps the words: gentle band (120 Hz - 5.5 kHz, 12 dB/oct),
# a +2 dB presence lift at 2.2 kHz, 12 % parallel soft saturation, then a quiet squelch: 35 ms filtered
# noise click before the line and a 120 ms fading hiss after it (-30 dBFS). Re-mastered to -18 LUFS, peak -1.
# Usage: python radio_fx.py <clean dir> <out dir>
import sys, os, glob, json, numpy as np, soundfile as sf, pyloudnorm as pyln, warnings
from scipy.signal import butter, sosfilt, iirpeak
warnings.filterwarnings("ignore")
from master import limit, SR, TARGET
meter = pyln.Meter(SR); rng = np.random.default_rng(7)
BAND = butter(2, [120, 5500], "bandpass", fs=SR, output="sos")
def presence(y):
    b, a = iirpeak(2200, 1.2, fs=SR)
    from scipy.signal import lfilter
    return y + (10 ** (2 / 20) - 1) * lfilter(b, a, y)
def squelch(n_ms, level_db, fade):
    n = int(n_ms / 1000 * SR); x = sosfilt(BAND, rng.standard_normal(n))
    x /= np.abs(x).max() + 1e-9; env = np.linspace(1, 0, n) ** 2 if fade else np.hanning(n)
    return x * env * 10 ** (level_db / 20)
def radio(y):
    y = sosfilt(BAND, y)
    y = presence(y)
    drive = 2.5; sat = np.tanh(drive * y / (np.abs(y).max() + 1e-9)) * np.abs(y).max() / np.tanh(drive)
    y = 0.88 * y + 0.12 * sat
    y = np.concatenate([squelch(35, -30, False), np.zeros(int(0.03 * SR)), y, squelch(120, -32, True)])
    for _ in range(2):
        y = pyln.normalize.loudness(y, meter.integrated_loudness(y), TARGET); y = limit(y)
    return y
if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]; os.makedirs(dst, exist_ok=True); L = []
    for f in sorted(glob.glob(os.path.join(src, "*.wav"))):
        y, _ = sf.read(f); y = radio(y)
        out = os.path.join(dst, os.path.basename(f)); sf.write(out, y.astype(np.float32), SR, subtype="PCM_16")
        z, _ = sf.read(out); L.append((os.path.basename(f), meter.integrated_loudness(z), 20 * np.log10(np.abs(z).max())))
    v = np.array([x[1] for x in L]); p = np.array([x[2] for x in L])
    print(f"{len(L)} radio files  LUFS {v.min():.2f}..{v.max():.2f}  peak max {p.max():.2f}")
