#!/usr/bin/env python3
"""Synthesises AVADefenderOfTheCrown's sound effects and music into ../src/AVADefenderOfTheCrown/Sounds as 16-bit mono WAV.

Nothing is sampled from any game: every sound is built here from sine, triangle, saw, square and noise voices, a
few simple filters and envelopes. The title plays Greensleeves, an English tune registered in 1580 and long in the
public domain, on a plucked lute voice over a bass; the fanfares, the victory theme and the lament are written here.
Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave


RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVADefenderOfTheCrown", "Sounds")
RNG = random.Random(1149)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=6.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t) * min(1.0, t / 0.15)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def noise(seconds, attack=0.002, release=1.0, gain=1.0):
    n = int(RATE * seconds)
    return [RNG.uniform(-1, 1) * min(1.0, (i / RATE) / attack) * (1 - i / n) ** release * gain for i in range(n)]


def lowpass(samples, cutoff):
    """A one-pole low-pass filter: softens noise into rumbles and thuds."""
    a = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    out, y = [], 0.0
    for s in samples:
        y += a * (s - y)
        out.append(y)
    return out


def highpass(samples, cutoff):
    low = lowpass(samples, cutoff)
    return [s - l for s, l in zip(samples, low)]


def band(samples, lo, hi):
    return lowpass(highpass(samples, lo), hi)


def swell(samples, rise=0.4):
    """Fades a sound in over the first part of it."""
    n = len(samples)
    return [s * min(1.0, i / (n * rise)) for i, s in enumerate(samples)]


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


def at(seconds, samples, length=None):
    """A sound placed some seconds into a stretch of silence."""
    pre = silence(seconds)
    out = pre + samples
    if length is not None:
        out = (out + silence(length))[: int(RATE * length)]
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def bell(seconds, f, gain=0.6):
    """A bell: a sine with its inharmonic partials, all dying away, the high ones first."""
    return mix(tone(seconds, f, kind="sine", attack=0.002, release=2.0, gain=gain),
               tone(seconds * 0.7, f * 2.76, kind="sine", attack=0.002, release=3.0, gain=gain * 0.4),
               tone(seconds * 0.5, f * 5.4, kind="sine", attack=0.002, release=3.0, gain=gain * 0.2),
               tone(seconds * 0.9, f * 0.5, kind="sine", attack=0.004, release=2.0, gain=gain * 0.3))


def steel(seconds, f, gain=0.6):
    """Steel struck on steel: a cluster of bright inharmonic partials ringing out fast."""
    return mix(*[tone(seconds * d, f * r, kind="sine", attack=0.001, release=4.0, gain=gain * g)
                 for r, g, d in ((1, 1, 1), (1.53, 0.8, 0.8), (2.31, 0.6, 0.6), (3.47, 0.45, 0.5), (4.92, 0.3, 0.35))],
               lowpass(noise(0.03, release=2), 6000))


def brass(seconds, note, gain=0.5):
    """A herald's trumpet: a bright saw and a square a hair apart, swelling in and with a little vibrato."""
    f = hz(note) if isinstance(note, str) else note
    body = mix(tone(seconds, f, kind="saw", attack=0.03, release=0.35, vibrato=0.004, vib_rate=5.5, gain=gain),
               tone(seconds, f * 1.003, kind="square", attack=0.03, release=0.35, vibrato=0.004, vib_rate=5.5, gain=gain * 0.35),
               tone(seconds, f * 2, kind="saw", attack=0.05, release=0.4, gain=gain * 0.15))
    return lowpass(body, 3200)


def pluck(seconds, note, gain=0.6):
    """A lute string: bright at first, dying away, with a little buzz."""
    f = hz(note)
    return mix(tone(seconds, f, kind="tri", attack=0.002, release=1.4, gain=gain),
               tone(min(seconds, 0.18), f * 2, kind="saw", attack=0.001, release=1.6, gain=gain * 0.22),
               tone(min(seconds, 0.06), f * 3, kind="square", attack=0.001, release=1.0, gain=gain * 0.06))


def drum(seconds=0.5, f=70, gain=1.0):
    """A war drum: a skin's thump dropping in pitch, and the slap of the stick."""
    return mix(tone(seconds, f * 1.6, f, kind="sine", attack=0.001, release=2.2, gain=gain),
               lowpass(noise(seconds * 0.4, release=3, gain=gain * 0.5), 900))


def play(bpm, notes, instrument, gain=0.6):
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        out.extend(silence(beats * beat) if note == "-" else instrument(beats * beat, note, gain))
    return out


def greensleeves():
    """Greensleeves: its two strains in 6/8, counted in semiquavers (twelve to a bar), with a bass under it."""
    a = [("C5", 4), ("D5", 2), ("E5", 3), ("F5", 1), ("E5", 2), ("D5", 4), ("B4", 2), ("G4", 3), ("A4", 1), ("B4", 2),
         ("C5", 4), ("A4", 2), ("A4", 3), ("G#4", 1), ("A4", 2), ("B4", 4), ("G#4", 2), ("E4", 4), ("A4", 2)]
    a2 = [("C5", 4), ("D5", 2), ("E5", 3), ("F5", 1), ("E5", 2), ("D5", 4), ("B4", 2), ("G4", 3), ("A4", 1), ("B4", 2),
          ("C5", 3), ("B4", 1), ("A4", 2), ("G#4", 3), ("F#4", 1), ("G#4", 2), ("A4", 6), ("A4", 4)]
    b = [("G5", 6), ("G5", 3), ("F#5", 1), ("E5", 2), ("D5", 4), ("B4", 2), ("G4", 3), ("A4", 1), ("B4", 2),
         ("C5", 4), ("A4", 2), ("A4", 3), ("G#4", 1), ("A4", 2), ("B4", 4), ("G#4", 2), ("E4", 6)]
    b2 = [("G5", 6), ("G5", 3), ("F#5", 1), ("E5", 2), ("D5", 4), ("B4", 2), ("G4", 3), ("A4", 1), ("B4", 2),
          ("C5", 3), ("B4", 1), ("A4", 2), ("G#4", 3), ("F#4", 1), ("G#4", 2), ("A4", 6), ("A4", 6)]
    lead = [("A4", 2)] + a + a2 + b + b2
    # The bass: two notes a bar, on the chords Am G Am E / Am G Am-E Am, then C G Am E / C G Am-E Am.
    bars = [("A2", "A2"), ("G2", "G2"), ("A2", "A2"), ("E2", "E2"), ("A2", "A2"), ("G2", "G2"), ("A2", "E2"), ("A2", "A2"),
            ("C3", "C3"), ("G2", "G2"), ("A2", "A2"), ("E2", "E2"), ("C3", "C3"), ("G2", "G2"), ("A2", "E2"), ("A2", "A2")]
    low = [("-", 2)] + [(n, 6) for bar in bars for n in bar]
    return lead, low


def bass_voice(seconds, note, gain):
    f = hz(note)
    return lowpass(mix(tone(seconds, f, kind="tri", attack=0.005, release=0.8, gain=gain),
                       tone(seconds, f * 2, kind="saw", attack=0.005, release=1.6, gain=gain * 0.2)), 1200)


def crowd(seconds, gain=1.0):
    """A crowd's roar: many voices of filtered noise, each swelling and falling, with a few shouts riding on top."""
    n = int(RATE * seconds)
    out = [0.0] * n
    for k in range(10):
        f = RNG.uniform(350, 1100)
        part = band(noise(seconds, attack=0.2, release=0.6), f * 0.6, f * 1.4)
        rate, ph = RNG.uniform(2, 5), RNG.uniform(0, 6)
        for i in range(n):
            out[i] += part[i] * (0.6 + 0.4 * math.sin(2 * math.pi * rate * i / RATE + ph))
    for k in range(5):
        start = RNG.uniform(0, seconds * 0.6)
        f = RNG.uniform(220, 420)
        shout = lowpass(tone(0.45, f, f * 1.25, kind="saw", attack=0.05, release=0.9, vibrato=0.03, vib_rate=7, gain=0.6), 1600)
        out = mix(out, at(start, shout, seconds))
    return [s * gain * min(1.0, i / (RATE * 0.25)) * min(1.0, (n - i) / (RATE * 0.6)) for i, s in enumerate(out)]


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))

    # The map: a wooden click, a refusal, coins counted out, the war drum, the herald's trumpets, a peal of bells, a horn.
    write("Click.wav", mix(lowpass(noise(0.03, release=2), 3000), tone(0.03, 900, 600, "tri", attack=0.001, release=2, gain=0.4)), 0.25)
    write("Refused.wav", lowpass(tone(0.25, 110, 98, "saw", attack=0.01, release=0.6), 700), 0.3)
    coins = silence(0.7)
    for k in range(7):
        coins = mix(coins, at(k * 0.07 + RNG.uniform(0, 0.03), bell(0.35, RNG.uniform(2600, 3600), 0.4), 0.7))
    write("Coins.wav", coins, 0.45)
    write("Drum.wav", cat(drum(0.32, 62), drum(0.22, 66, 0.7), drum(0.7, 58)), 0.75)
    write("Fanfare.wav", cat(brass(0.13, "C5"), brass(0.13, "C5"), brass(0.13, "C5"), brass(0.32, "G5"), silence(0.05),
                             brass(0.16, "E5"), brass(0.7, "C6")), 0.5)
    peal = silence(3.2)
    for k, n in enumerate(["C6", "B5", "A5", "G5", "F5", "E5", "D5", "C5"]):
        peal = mix(peal, at(k * 0.32, bell(2.0, hz(n), 0.5), 3.2))
    write("Wedding.wav", peal, 0.5)
    write("Horn.wav", cat(lowpass(mix(tone(0.5, hz("A3"), kind="saw", attack=0.08, release=0.3, vibrato=0.006, vib_rate=5),
                                      tone(0.5, hz("A3") * 1.005, kind="saw", attack=0.08, release=0.3)), 1400),
                          lowpass(tone(0.9, hz("E4"), kind="saw", attack=0.06, release=0.7, vibrato=0.008, vib_rate=5), 1600)), 0.45)

    # The lists: hooves at the gallop, the lance on the boss, a lance shattering, a miss, a fall, and the crowd.
    hooves = silence(2.6)
    beat = 0.36
    for k in range(7):
        for j, off in enumerate((0, 0.07, 0.14)):
            hit = mix(lowpass(noise(0.08, release=3, gain=1.0), 500), tone(0.08, 130, 80, "sine", attack=0.001, release=3, gain=0.8))
            hooves = mix(hooves, at(k * beat + off, [s * (0.6 + 0.4 * (k / 7)) for s in hit], 2.6))
    write("Hooves.wav", hooves, 0.6)
    write("LanceHit.wav", mix(tone(0.5, 90, 45, "sine", attack=0.001, release=2.0), lowpass(noise(0.3, release=2.5), 2400),
                              steel(0.6, 520, 0.5)), 0.8)
    splinters = silence(0.7)
    for k in range(9):
        splinters = mix(splinters, at(RNG.uniform(0, 0.25), highpass(noise(RNG.uniform(0.02, 0.06), release=2), 1500), 0.7))
    write("Shatter.wav", mix(splinters, lowpass(noise(0.25, release=2.5, gain=0.8), 1800), tone(0.3, 140, 70, "sine", attack=0.001, release=2.5, gain=0.6)), 0.75)
    write("Whoosh.wav", swell(band(noise(0.6, attack=0.01, release=1.0), 400, 2400), 0.5), 0.4)
    write("Fall.wav", mix(tone(0.6, 70, 40, "sine", attack=0.001, release=2.0), lowpass(noise(0.5, release=2, gain=0.8), 700),
                          at(0.05, steel(0.4, 780, 0.35)), at(0.15, steel(0.3, 1010, 0.25))), 0.8)
    write("Cheer.wav", crowd(2.6), 0.55)

    # The swordfight: a swing, steel on steel, a blow that lands, a grunt.
    write("Swish.wav", band([s * math.sin(math.pi * i / (RATE * 0.22)) for i, s in enumerate(noise(0.22, release=0.2))], 1200, 5000), 0.35)
    write("Clang.wav", steel(0.7, 760, 0.8), 0.6)
    write("Slash.wav", mix(lowpass(noise(0.18, release=2.5), 1600), tone(0.2, 160, 80, "sine", attack=0.001, release=2, gain=0.7)), 0.6)
    write("Hurt.wav", lowpass(mix(tone(0.35, 180, 120, "saw", attack=0.01, release=1.2, vibrato=0.04, vib_rate=11),
                                  noise(0.3, attack=0.01, release=1.5, gain=0.25)), 1100), 0.5)

    # The siege: the catapult's creak and thump, the wall struck, a dull landing, Greek fire.
    creak = lowpass(tone(0.35, 70, 55, "saw", attack=0.02, release=0.4, vibrato=0.2, vib_rate=31, gain=0.5), 1500)
    thump = mix(tone(0.25, 110, 60, "sine", attack=0.001, release=2.2), lowpass(noise(0.15, release=2.5, gain=0.6), 800))
    write("Launch.wav", cat(creak, thump, swell(band(noise(0.4, release=1.0, gain=0.6), 300, 1500), 0.3)), 0.6)
    rubble = silence(1.4)
    for k in range(14):
        rubble = mix(rubble, at(0.08 + RNG.uniform(0, 0.9), lowpass(noise(RNG.uniform(0.04, 0.1), release=2, gain=RNG.uniform(0.3, 0.7)), 1400), 1.4))
    write("Crash.wav", mix(tone(1.0, 70, 32, "sine", attack=0.001, release=1.6), lowpass(noise(0.8, release=1.6), 900), rubble), 0.8)
    write("Thud.wav", mix(tone(0.4, 80, 45, "sine", attack=0.001, release=2.0), lowpass(noise(0.3, release=2.2, gain=0.7), 600)), 0.6)
    crackle = silence(1.2)
    for k in range(30):
        crackle = mix(crackle, at(RNG.uniform(0, 1.1), highpass(noise(0.012, release=1), 2500), 1.2))
    write("Fire.wav", mix(swell(lowpass(noise(1.2, attack=0.05, release=1.0), 900), 0.25), [s * 0.5 for s in crackle]), 0.55)

    # The battle's din: shouts, steel and hooves together.
    din = [s * 0.5 for s in crowd(2.2)]
    for k in range(14):
        din = mix(din, at(RNG.uniform(0, 1.8), steel(0.3, RNG.uniform(600, 1300), RNG.uniform(0.2, 0.45)), 2.2))
    for k in range(6):
        din = mix(din, at(RNG.uniform(0, 1.9), drum(0.25, RNG.uniform(60, 90), 0.4), 2.2))
    write("Din.wav", din, 0.55)

    # The music: Greensleeves on the title, a triumph for a crown won, a lament for a cause lost.
    lead, low = greensleeves()
    # A semiquaver is played as half a beat: a bar of 6/8 takes about 1.4 seconds.
    tune = mix(play(257, [(n, u / 2) for n, u in lead], pluck, 0.6), play(257, [(n, u / 2) for n, u in low], bass_voice, 0.4))
    write("TitleTune.wav", cat(tune, silence(1.5)), 0.42)
    triumph = cat(brass(0.2, "C5"), brass(0.2, "E5"), brass(0.2, "G5"), brass(0.6, "C6"), silence(0.05),
                  brass(0.2, "A5"), brass(0.2, "B5"), brass(0.9, "C6"))
    under = cat(mix(brass(1.25, "C4", 0.35), brass(1.25, "G4", 0.3)), mix(brass(1.4, "F4", 0.35), brass(1.4, "C5", 0.25), brass(1.4, "A4", 0.25)))
    rolls = cat(*[drum(0.18, 70, 0.5) for _ in range(10)], drum(0.9, 60))
    write("Victory.wav", cat(mix(triumph, under, [s * 0.6 for s in rolls]), silence(0.5)), 0.6)
    lament_notes = [("A4", 1.2), ("G4", 0.6), ("F4", 0.6), ("E4", 1.2), ("D4", 1.2), ("C#4", 0.9), ("D4", 2.0)]
    lament = play(60, lament_notes, lambda s, n, g: lowpass(tone(s, hz(n), kind="saw", attack=0.12, release=0.5, vibrato=0.006, vib_rate=5, gain=g), 1500), 0.5)
    drone = lowpass(mix(tone(len(lament) / RATE, hz("D2"), kind="saw", attack=0.4, release=0.6, gain=0.35),
                        tone(len(lament) / RATE, hz("A2"), kind="saw", attack=0.4, release=0.6, gain=0.25)), 600)
    write("Defeat.wav", cat(mix(lament, drone, at(0, drum(1.0, 50, 0.6))), silence(0.5)), 0.55)


if __name__ == "__main__":
    main()
