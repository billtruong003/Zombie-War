"""Synthesises Zombie War's UI sounds (M8 P3) as small mono WAVs.

The project had no UI sounds at all and no pack clip fit a toy-like casual UI, so these are made
here: short sine/triangle blips with fast envelopes, bright and soft. Re-run to regenerate:

    python Tools/gen_ui_sfx.py

Output: Assets/_Project/Audio/UI/ui_*.wav. The curated audio builder maps each file to the cue
"sfx.ui.<name>" (e.g. ui_tap.wav -> sfx.ui.tap).
"""
import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Project", "Audio", "UI")


def tone(freq_from, freq_to, dur, shape="sine", attack=0.004, decay=6.0, gain=0.6):
    """One blip: frequency glides from->to, exponential decay after a short attack."""
    n = int(RATE * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / RATE
        f = freq_from + (freq_to - freq_from) * (i / max(1, n - 1))
        phase += 2 * math.pi * f / RATE
        if shape == "sine":
            s = math.sin(phase)
        elif shape == "tri":
            s = 2 / math.pi * math.asin(math.sin(phase))
        else:  # soft square
            s = math.tanh(3 * math.sin(phase))
        env = min(1.0, t / attack) * math.exp(-decay * t / dur)
        out.append(s * env * gain)
    return out


def silence(dur):
    return [0.0] * int(RATE * dur)


def mix(*tracks):
    n = max(len(t) for t in tracks)
    return [sum(t[i] for t in tracks if i < len(t)) for i in range(n)]


def seq(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def click(dur=0.006, gain=0.25):
    rnd = random.Random(7)
    n = int(RATE * dur)
    return [rnd.uniform(-1, 1) * gain * (1 - i / n) for i in range(n)]


def write(name, samples):
    peak = max(1e-6, max(abs(s) for s in samples))
    scale = 0.89 / peak if peak > 0.89 else 1.0
    # 3 ms fade-out so no clip ends on a click.
    fade = int(RATE * 0.003)
    for i in range(fade):
        samples[-1 - i] *= i / fade
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in samples))


def note(n):
    """Semitones from A4."""
    return 440.0 * 2 ** (n / 12)


if __name__ == "__main__":
    C6, E6, G6, C7 = note(15), note(19), note(22), note(27)
    write("ui_tap", mix(click(), tone(1500, 1050, 0.06, "sine", decay=5, gain=0.55)))
    write("ui_confirm", seq(tone(note(12), note(12), 0.07, "tri", gain=0.5), tone(note(19), note(19), 0.12, "tri", gain=0.5)))
    write("ui_back", seq(tone(note(14), note(14), 0.06, "tri", gain=0.45), tone(note(7), note(7), 0.1, "tri", gain=0.45)))
    write("ui_error", seq(tone(190, 170, 0.08, "square", decay=3, gain=0.35), silence(0.03), tone(190, 160, 0.1, "square", decay=3, gain=0.35)))
    write("ui_purchase", mix(
        seq(tone(C6, C6, 0.07, "tri"), tone(E6, E6, 0.07, "tri"), tone(G6, G6, 0.07, "tri"), tone(C7, C7, 0.28, "tri", decay=4)),
        seq(silence(0.21), tone(C7 * 2, C7 * 2, 0.25, "sine", decay=5, gain=0.15))))
    write("ui_levelup", seq(*[tone(note(n), note(n), 0.075 if k < 4 else 0.3, "tri", decay=4 if k == 4 else 6, gain=0.5)
                             for k, n in enumerate([3, 7, 10, 15, 19])]))
    write("ui_card", tone(520, 880, 0.09, "sine", attack=0.01, decay=4, gain=0.4))
    write("ui_equip", mix(click(0.01, 0.35), seq(tone(note(10), note(10), 0.05, "tri", gain=0.5), tone(note(17), note(17), 0.14, "tri", decay=5, gain=0.5))))
    print("wrote", sorted(os.listdir(OUT)))
