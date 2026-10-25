"""A-01 synthesized layers: drones, room tone, hums, heartbeat, tinnitus. All original (no third-party audio)."""
import numpy as np
from audiolib import SR, biquad, loopify, gain, rms_db, normalize, fades

rng = np.random.default_rng(1207)

def pink(n, ch=2):
    # Voss-ish pink via filtering white noise
    w = rng.standard_normal((n, ch)).astype(np.float32)
    b = np.array([0.049922035, -0.095993537, 0.050612699, -0.004408786]); a = np.array([1, -2.494956002, 2.017265875, -0.522189400])
    from scipy.signal import lfilter
    return lfilter(b, a, w, axis=0).astype(np.float32)

def brown(n, ch=2):
    w = rng.standard_normal((n, ch)).astype(np.float32)
    x = np.cumsum(w, axis=0); x -= np.linspace(x[0], x[-1], n)  # remove drift so it loops
    x = biquad(x, 'hp', 18)
    return (x / (np.abs(x).max() + 1e-9)).astype(np.float32)

def lfo(n, rate, phase=0.0, depth=1.0):
    t = np.arange(n) / SR
    return (1 - depth) + depth * (0.5 + 0.5 * np.sin(2 * np.pi * rate * t + phase))

def loop_rate(rate, dur):
    """Snap an LFO rate so an integer number of cycles fits the loop (seamless)."""
    return max(1, round(rate * dur)) / dur

def drone(dur, root, partials, noise_amt, bright, seed=0, stereo_detune=0.15):
    """Slowly breathing drone. partials: list of (ratio, amp). Built to loop exactly in `dur` seconds."""
    global rng
    rng = np.random.default_rng(seed)
    n = int(dur * SR); t = np.arange(n) / SR
    out = np.zeros((n, 2), np.float32)
    for i, (ratio, amp) in enumerate(partials):
        f = root * ratio
        f = round(f * dur) / dur  # integer cycles => seamless
        for c in range(2):
            det = (stereo_detune * (1 if c else -1) * (i + 1)) / dur * round(dur)  # tiny integer-cycle detune
            fc = f + round(det * dur) / dur
            ph = rng.uniform(0, 2 * np.pi)
            am = lfo(n, loop_rate(rng.uniform(0.02, 0.07), dur), rng.uniform(0, 6.28), 0.6)
            out[:, c] += (amp * am * np.sin(2 * np.pi * fc * t + ph)).astype(np.float32)
    nz = pink(n) * noise_amt
    nz = biquad(nz, 'lp', bright); nz = biquad(nz, 'hp', 40)
    sweep = lfo(n, loop_rate(0.03, dur), 1.0, 0.7)[:, None]
    out += nz * sweep
    out = biquad(out, 'lp', bright * 1.5)
    return out

def make_loop(a, xfade=3.0):
    return loopify(a, xfade)

def room_tone(dur=40.0):
    n = int(dur * SR)
    x = brown(n) * 0.5 + pink(n) * 0.05
    x = biquad(x, 'lp', 900); x = biquad(x, 'hp', 35)
    t = np.arange(n) / SR
    hum = (0.012 * np.sin(2 * np.pi * 50 * t) + 0.004 * np.sin(2 * np.pi * 100 * t))[:, None]
    return make_loop((x + hum).astype(np.float32), 2.0)

def electric_buzz(dur=6.0):
    n = int(dur * SR); t = np.arange(n) / SR
    x = np.zeros(n)
    for h, a in [(1, 1.0), (2, .5), (3, .35), (4, .2), (6, .12), (8, .08)]:
        x += a * np.sin(2 * np.pi * 100 * h * t)
    x = np.tanh(2.5 * x) * 0.4
    crackle = (rng.random(n) < 0.0008) * rng.standard_normal(n) * 0.6
    y = (x + biquad(crackle[:, None], 'bp', 3000, 1.0)[:, 0])[:, None]
    y = biquad(y, 'hp', 80); y = biquad(y, 'lp', 6000)
    return make_loop(np.repeat(y, 1, axis=1).astype(np.float32), 0.5)

def heartbeat_single(bpm_hint=70):
    n = int(0.9 * SR); t = np.arange(n) / SR
    def thump(t0, f, amp, dec):
        tt = np.clip(t - t0, 0, None)
        env = (tt > 0) * np.exp(-tt / dec) * (1 - np.exp(-tt / 0.004))
        return amp * env * np.sin(2 * np.pi * (f + 25 * np.exp(-tt / 0.03)) * tt)
    x = thump(0.0, 48, 1.0, 0.07) + thump(0.27, 42, 0.7, 0.08)
    x += biquad((rng.standard_normal(n) * np.exp(-t / 0.02) * 0.08)[:, None], 'lp', 300)[:, 0]
    return x[:, None].astype(np.float32)

def tinnitus(dur=7.0):
    n = int(dur * SR); t = np.arange(n) / SR
    env = np.minimum(1, t / 0.05) * np.exp(-t / 2.2)
    x = env * (np.sin(2 * np.pi * 5100 * t) * 0.6 + np.sin(2 * np.pi * 5137 * t) * 0.4)
    return (x[:, None] * 0.5).astype(np.float32)

def sub_hit(dur=3.5, f0=55):
    n = int(dur * SR); t = np.arange(n) / SR
    f = f0 + 70 * np.exp(-t / 0.08)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(ph) * np.exp(-t / 1.1) * (1 - np.exp(-t / 0.003))
    nz = biquad((rng.standard_normal(n) * np.exp(-t / 0.35))[:, None], 'lp', 700)[:, 0] * 0.5
    y = np.tanh(1.6 * (x + nz))
    st = np.stack([y, np.roll(y, 220)], axis=1)
    return biquad(st, 'lp', 4000).astype(np.float32)

def riser(dur=4.0):
    n = int(dur * SR); t = np.arange(n) / SR
    nz = pink(n) * (t / dur)[:, None] ** 2
    out = np.zeros((n, 2), np.float32)
    for c in range(2):
        out[:, c] = biquad(nz[:, c:c + 1], 'bp', 600, 2.0)[:, 0]
    f = 80 + 220 * (t / dur) ** 2
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * (t / dur) ** 2 * 0.3
    out += tone[:, None]
    return fades(out, 0.5, 0.01)
