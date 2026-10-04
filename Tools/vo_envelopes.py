"""Radio voice loudness envelopes for the radio card's waveform.

WebGL cannot read audio samples at run time (AudioSource.GetOutputData is not supported there), so
each voice line's loudness is measured here, once, from the WAV files and stored as a small JSON the
game reads (RadioVoice.Level). Re-run after adding or replacing voice lines:

    python Tools/vo_envelopes.py

Output: Assets/_Project/Audio/VO/Resources/VO/vo_envelopes.json
  {"fps": 30, "lines": [{"id": "vo_riley_ftue_home", "env": "<base64 bytes>"}, ...]}
One byte per 1/30 s: RMS of that window, divided by the line's 95th-percentile RMS (so a quiet and a
loud line both fill the bars), clamped to 1 and stored as 0..255.
"""
import base64
import glob
import json
import math
import os
import struct
import wave

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CLIPS = os.path.join(ROOT, "Assets", "_Project", "Audio", "VO", "Clips")
OUT = os.path.join(ROOT, "Assets", "_Project", "Audio", "VO", "Resources", "VO", "vo_envelopes.json")
FPS = 30


def envelope(path):
    with wave.open(path, "rb") as w:
        channels, width, rate, frames = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(frames)
    if width != 2:
        raise ValueError(f"{path}: {width * 8}-bit audio, expected 16-bit")
    samples = struct.unpack("<%dh" % (len(raw) // 2), raw)
    if channels > 1:
        samples = samples[::channels]
    step = max(1, rate // FPS)
    rms = []
    for i in range(0, len(samples), step):
        chunk = samples[i:i + step]
        rms.append(math.sqrt(sum(s * s for s in chunk) / max(1, len(chunk))) / 32768.0)
    ref = sorted(rms)[int(len(rms) * 0.95)] if rms else 1.0
    ref = max(ref, 1e-4)
    return bytes(min(255, int(round(min(1.0, r / ref) * 255))) for r in rms)


def main():
    lines = []
    for path in sorted(glob.glob(os.path.join(CLIPS, "*.wav"))):
        env = envelope(path)
        lines.append({"id": os.path.splitext(os.path.basename(path))[0], "env": base64.b64encode(env).decode("ascii")})
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump({"fps": FPS, "lines": lines}, f, separators=(",", ":"))
    print(f"{len(lines)} envelopes -> {os.path.relpath(OUT, ROOT)} ({os.path.getsize(OUT) // 1024} KB)")


if __name__ == "__main__":
    main()
