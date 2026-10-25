"""A-01 audio processing helpers (numpy + ffmpeg)."""
import subprocess, numpy as np, os

SR = 44100

def load(path, sr=SR, mono=False):
    ch = 1 if mono else 2
    r = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-f', 'f32le', '-ac', str(ch), '-ar', str(sr), '-'],
                       capture_output=True, check=True)
    a = np.frombuffer(r.stdout, dtype=np.float32).copy()
    return a.reshape(-1, ch)

def save(path, a, sr=SR, quality=7):
    """Save as .wav or .ogg (vorbis) depending on extension."""
    a = np.asarray(a, dtype=np.float32)
    if a.ndim == 1: a = a[:, None]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ext = os.path.splitext(path)[1].lower()
    codec = ['-c:a', 'libvorbis', '-q:a', str(quality)] if ext == '.ogg' else \
            ['-c:a', 'libmp3lame', '-q:a', '2'] if ext == '.mp3' else ['-c:a', 'pcm_s16le']
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ac', str(a.shape[1]), '-ar', str(sr), '-i', '-',
                    *codec, path], input=np.clip(a, -1, 1).tobytes(), check=True)

def mono(a): return a.mean(axis=1, keepdims=True) if a.shape[1] > 1 else a

def peak_db(a): return 20 * np.log10(max(np.abs(a).max(), 1e-9))

def loud_db(a, win=0.4, sr=SR):
    """Loudest windowed RMS (dBFS) — proxy for perceived level of short SFX."""
    x = mono(a)[:, 0].astype(np.float64)
    n = max(1, int(win * sr))
    if len(x) <= n: return 20 * np.log10(max(np.sqrt(np.mean(x ** 2)), 1e-9))
    c = np.cumsum(np.insert(x ** 2, 0, 0))
    rms = np.sqrt((c[n:] - c[:-n]) / n)
    return 20 * np.log10(max(rms.max(), 1e-9))

def rms_db(a):
    x = mono(a)[:, 0]
    return 20 * np.log10(max(np.sqrt(np.mean(x.astype(np.float64) ** 2)), 1e-9))

def gain(a, db): return a * (10 ** (db / 20))

def normalize(a, target_loud, ceiling=-1.0, measure='loud'):
    cur = loud_db(a) if measure == 'loud' else rms_db(a)
    out = gain(a, target_loud - cur)
    pk = peak_db(out)
    if pk > ceiling: out = soft_limit(out, ceiling)
    return out

def soft_limit(a, ceiling_db=-1.0):
    """Simple lookahead-free limiter: tanh-ish knee above threshold, then hard ceiling."""
    c = 10 ** (ceiling_db / 20)
    t = c * 0.7
    x = a.copy()
    m = np.abs(x) > t
    x[m] = np.sign(x[m]) * (t + (c - t) * np.tanh((np.abs(x[m]) - t) / (c - t)))
    return np.clip(x, -c, c)

def trim(a, thresh_db=-50, pad=0.01, sr=SR):
    x = np.abs(mono(a)[:, 0])
    th = 10 ** (thresh_db / 20)
    idx = np.where(x > th)[0]
    if len(idx) == 0: return a
    s = max(0, idx[0] - int(pad * sr)); e = min(len(a), idx[-1] + int(pad * 4 * sr))
    return a[s:e]

def fades(a, fin=0.003, fout=0.02, sr=SR):
    a = a.copy()
    ni, no = min(int(fin * sr), len(a) // 4), min(int(fout * sr), len(a) // 3)
    if ni: a[:ni] *= np.linspace(0, 1, ni)[:, None]
    if no: a[-no:] *= np.linspace(1, 0, no)[:, None]
    return a

def loopify(a, xfade=2.0, sr=SR):
    """Seamless loop: crossfade tail into head (equal power)."""
    n = int(xfade * sr)
    head, body, tail = a[:n], a[n:-n] if len(a) > 2 * n else a[n:], a[-n:]
    t = np.linspace(0, np.pi / 2, n)[:, None]
    mixed = tail * np.cos(t) + head * np.sin(t)
    return np.concatenate([body, mixed]) if len(a) > 2 * n else a

def biquad(a, kind, f, q=0.707, db=0.0, sr=SR):
    from scipy.signal import lfilter
    w0 = 2 * np.pi * f / sr; al = np.sin(w0) / (2 * q); cw = np.cos(w0); A = 10 ** (db / 40)
    if kind == 'lp': b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]; aa = [1 + al, -2 * cw, 1 - al]
    elif kind == 'hp': b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]; aa = [1 + al, -2 * cw, 1 - al]
    elif kind == 'bp': b = [al, 0, -al]; aa = [1 + al, -2 * cw, 1 - al]
    elif kind == 'peak': b = [1 + al * A, -2 * cw, 1 - al * A]; aa = [1 + al / A, -2 * cw, 1 - al / A]
    elif kind == 'lowshelf':
        sq = 2 * np.sqrt(A) * al
        b = [A * ((A + 1) - (A - 1) * cw + sq), 2 * A * ((A - 1) - (A + 1) * cw), A * ((A + 1) - (A - 1) * cw - sq)]
        aa = [(A + 1) + (A - 1) * cw + sq, -2 * ((A - 1) + (A + 1) * cw), (A + 1) + (A - 1) * cw - sq]
    elif kind == 'highshelf':
        sq = 2 * np.sqrt(A) * al
        b = [A * ((A + 1) + (A - 1) * cw + sq), -2 * A * ((A - 1) + (A + 1) * cw), A * ((A + 1) + (A - 1) * cw - sq)]
        aa = [(A + 1) - (A - 1) * cw + sq, 2 * ((A - 1) - (A + 1) * cw), (A + 1) - (A - 1) * cw - sq]
    b = np.array(b) / aa[0]; aa = np.array(aa) / aa[0]
    return lfilter(b, aa, a, axis=0).astype(np.float32)

def resample_pitch(a, semitones):
    """Pitch+speed change (tape style) by resampling."""
    from scipy.signal import resample
    r = 2 ** (semitones / 12)
    n = int(len(a) / r)
    return resample(a, n, axis=0).astype(np.float32)

def describe(path):
    a = load(path)
    return dict(dur=len(a) / SR, peak=peak_db(a), loud=loud_db(a), rms=rms_db(a))
