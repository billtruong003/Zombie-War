# Master every take of the non-FTUE lines for listening: same chain as master.py, written as 128 kbps-ish MP3.
import glob, os, sys, json, numpy as np, soundfile as sf, pyloudnorm as pyln
from concurrent.futures import ProcessPoolExecutor
from master import process, SR
SRC = "D:/Projects/Zombie-War/Review/Lore/voices/rest_v4/"; DST = SRC + "mastered/"
def one(f):
    out = DST + os.path.basename(f)
    if not os.path.exists(out):
        y = process(f); sf.write(out, y.astype(np.float32), SR, format="MP3")
    z, sr = sf.read(out); m = pyln.Meter(sr)
    return os.path.basename(f), m.integrated_loudness(z), 20 * np.log10(np.abs(z).max() + 1e-9)
if __name__ == "__main__":
    os.makedirs(DST, exist_ok=True)
    files = sorted(glob.glob(SRC + "*.mp3"))
    before = []
    for f in files[:0]: pass
    with ProcessPoolExecutor(6) as ex: res = list(ex.map(one, files))
    L = np.array([r[1] for r in res]); P = np.array([r[2] for r in res])
    json.dump(res, open(DST + "loudness.json", "w"))
    print(f"{len(res)} takes  LUFS {L.min():.2f}..{L.max():.2f} (spread {L.max()-L.min():.2f})  peak max {P.max():.2f} dBFS")
