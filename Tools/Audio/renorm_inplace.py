"""A-01: in-place loudness fix for retained project clips. Same path/extension => .meta/GUID untouched.
Originals backed up to scratchpad/backup before overwrite."""
import os, sys, shutil, subprocess, json
sys.path.insert(0, os.path.dirname(__file__))
from audiolib import *

SP = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BACKUP = os.path.join(SP, 'backup')

TARGETS = {  # path: (target loud dBFS, highpass Hz or 0)
    "Assets/Resources/Audio/GeneratorShutdown.mp3": (-17, 0),
    "Assets/Resources/Audio/CornerJumpscare.mp3": (-12, 0),
    "Assets/Audio/monster sounds/freesound_community-jumpscare-94984.mp3": (-13, 0),
    "Assets/Audio/monster sounds/1.MP3": (-21, 30),
    "Assets/Audio/monster sounds/0071879de0ff7c9.mp3": (-21, 30),
    "Assets/Audio/monster sounds/2.MP3": (-19, 0),
    "Assets/Basement/Audio/BasementBreath.wav": (-22, 60),
    "Assets/Basement/Audio/BasementAttack.wav": (-15, 30),
    "Assets/Basement/Audio/PlayerRise.wav": (-24, 60),
    "Assets/Basement/Audio/PlayerFall.wav": (-20, 0),
    "Assets/Audio/MicrowaveQuest/eatingSound.mp3": (-26, 120),
    "Assets/Audio/MicrowaveQuest/microwaveStartSound.mp3": (-24, 0),
    "Assets/Audio/MicrowaveQuest/microwaveEndSound.mp3": (-24, 0),
    "Assets/Audio/door/скрип.MP3": (-24, 80),
    "Assets/Resources/Audio/Horror/FloorCreak.wav": (-22, 0),
    "Assets/Resources/Audio/Horror/PlateImpact.wav": (-20, 0),
    "Assets/Audio/LightSwitch/fanrotationSound.mp3": (-26, 0),
    "Assets/Resources/Audio/DeskLampToggle.mp3": (-24, 0),
}

def probe(path):
    r = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'a:0', '-show_entries', 'stream=sample_rate,channels',
                        '-of', 'json', path], capture_output=True, text=True, encoding='utf-8', check=True)
    s = json.loads(r.stdout)['streams'][0]
    return int(s['sample_rate']), int(s['channels'])

def main(root):
    for rel, (target, hp) in TARGETS.items():
        path = os.path.join(root, rel)
        sr, ch = probe(path)
        bk = os.path.join(BACKUP, rel)
        os.makedirs(os.path.dirname(bk), exist_ok=True)
        if not os.path.exists(bk): shutil.copy2(path, bk)
        a = load(bk, sr=44100)  # always from the original
        if hp: a = biquad(a, 'hp', hp)
        before = loud_db(a)
        a = normalize(a, target, -1.0)
        if ch == 1: a = mono(a)
        ext = os.path.splitext(path)[1].lower()
        tmp = path + '.tmp' + ext
        codec = ['-c:a', 'libmp3lame', '-q:a', '2'] if ext == '.mp3' else ['-c:a', 'pcm_s16le']
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ac', str(a.shape[1]), '-ar', '44100', '-i', '-',
                        *codec, '-f', 'mp3' if ext == '.mp3' else 'wav', tmp],
                       input=np.clip(a, -1, 1).astype(np.float32).tobytes(), check=True)
        os.replace(tmp, path)
        print(f"{before:6.1f} -> {loud_db(a):6.1f} pk={peak_db(a):5.1f}  {rel}")

if __name__ == '__main__':
    main(sys.argv[1])
