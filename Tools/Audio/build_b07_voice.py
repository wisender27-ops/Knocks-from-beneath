# B-07 «Стакан воды»: голос монстра «Don't move» (Assets/Resources/Audio/GlassOfWater/DontMove.ogg).
# 1) База — Piper TTS, голос en_US-joe-medium (https://huggingface.co/rhasspy/piper-voices), по слову:
#      echo "Don't" | python -m piper -m en_US-joe-medium.onnx --length-scale 1.55 -f w_ont.wav
#      echo "move."  | python -m piper -m en_US-joe-medium.onnx --length-scale 1.55 -f w_move.wav
# 2) python build_b07_voice.py <папка с w_ont.wav, w_move.wav> → DontMove.wav, затем
#      ffmpeg -i DontMove.wav -ac 1 -ar 44100 -c:a libvorbis -q:a 6 DontMove.ogg
# Цепочка (по гайдам по «монстр-голосам»: тон + форманты + насыщение + пространство, слои):
#   основной слой — тон и форманты вниз (rubberband pitch 0.63, formant shifted) + лёгкое вибрато;
#   под ним октава вниз с кольцевой модуляцией 36 Гц — нечеловеческий гул; шёпот (случайная фаза в FFT) с задержкой 35 мс;
#   tanh-насыщение, обратный ревёрб («затягивание» перед словами), короткая комната. Нужен ffmpeg с rubberband.
import sys,subprocess,numpy as np,os
W=sys.argv[1]; SR=44100
def load(path,af=None):
    cmd=['ffmpeg','-v','error','-i',path]+(['-af',af] if af else [])+['-ac','1','-ar',str(SR),'-f','f32le','-']
    return np.frombuffer(subprocess.run(cmd,capture_output=True,check=True).stdout,np.float32).copy()
def save(a,path):
    subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar',str(SR),'-ac','1','-i','-',path],input=a.astype(np.float32).tobytes(),check=True)
def trim(a,th=0.01):
    i=np.where(np.abs(a)>th)[0]; return a[max(0,i[0]-200):i[-1]+2000]
dont=trim(load(W+'/w_ont.wav')); move=trim(load(W+'/w_move.wav'))
gap=np.zeros(int(0.55*SR),np.float32)
dry=np.concatenate([np.zeros(int(0.9*SR),np.float32),dont,gap,move,np.zeros(int(1.6*SR),np.float32)])
save(dry,W+'/dry.wav')
main=load(W+'/dry.wav','rubberband=pitch=0.63:formant=shifted,vibrato=f=4.5:d=0.08')
sub =load(W+'/dry.wav','rubberband=pitch=0.5:formant=shifted,lowpass=f=1800')
whis=load(W+'/dry.wav',"rubberband=pitch=0.9:formant=preserved,afftfilt=real='hypot(re,im)*cos((random(0)*2-1)*2*3.14)':imag='hypot(re,im)*sin((random(1)*2-1)*2*3.14)':win_size=256:overlap=0.75,highpass=f=1200")
n=max(len(main),len(sub),len(whis)+int(0.035*SR))
def pad(a,off=0):
    o=np.zeros(n,np.float32); o[off:off+len(a)]=a[:n-off]; return o
t=np.arange(n)/SR
sub=pad(sub)*np.sin(2*np.pi*36*t)          # кольцевая модуляция — «нечеловеческий» гул
mix=pad(main)*1.0+sub*0.55+pad(whis,int(0.035*SR))*0.35
mix=np.tanh(mix/np.max(np.abs(mix))*2.2)  # насыщение
# обратный ревёрб: вдох-«затягивание» перед словами
rev_ir=np.random.default_rng(7).standard_normal(int(1.4*SR)).astype(np.float32)*np.exp(-np.linspace(0,7,int(1.4*SR)))
from scipy.signal import fftconvolve
swell=fftconvolve(mix[::-1],rev_ir)[:n][::-1]; swell/=np.max(np.abs(swell))
room_ir=np.random.default_rng(3).standard_normal(int(0.5*SR)).astype(np.float32)*np.exp(-np.linspace(0,9,int(0.5*SR)))
room=fftconvolve(mix,room_ir)[:n]; room/=np.max(np.abs(room))
out=mix*0.85+room*0.18+swell*0.22
# хвост тишины в начале оставляем коротким
i=np.where(np.abs(out)>0.02)[0]; out=out[max(0,i[0]-int(0.05*SR)):i[-1]+int(0.3*SR)]
fade=int(0.25*SR); out[-fade:]*=np.linspace(1,0,fade)
out=out/np.max(np.abs(out))*0.89
save(out,W+'/DontMove.wav'); print(len(out)/SR)
