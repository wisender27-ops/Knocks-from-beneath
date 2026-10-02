from PIL import Image,ImageDraw
from pathlib import Path
p=Path('ArtSource/CH_ReferenceSurvivor/review');out=Image.new('RGB',(1200,600),(48,48,48));d=ImageDraw.Draw(out)
for j,f in enumerate([1,10,20,30]):
 im=Image.open(p/f'06_rig/pose{f}/material_threequarter.png').convert('RGBA');im.thumbnail((300,550));out.paste(im,(j*300,30),im);d.text((j*300+10,5),'POSE '+str(f),fill='white')
out.save(p/'final/extreme_poses.png')
