from PIL import Image, ImageDraw, ImageFilter
from pathlib import Path
import random, math
p=Path(__file__).resolve().parents[1]; random.seed(28)
src=Image.open(p/'ref/turnaround.png').convert('RGB')
size=1024;tile=256
atlas=Image.new('RGB',(size,size),(70,63,54)); rough=Image.new('L',(size,size),225)
# All cloth colours authored as albedo, with no directional shading.
palettes={'jacket':[(57,50,42),(62,53,43),(69,60,49),(49,44,37),(77,65,51)],'shirt':[(139,130,110),(151,142,122),(130,123,106),(159,148,125)],'denim':[(64,67,76),(73,77,88),(88,89,94),(60,62,72),(97,93,89)],'shoes':[(67,56,43),(85,70,51),(96,81,60),(56,48,38)],'hair':[(28,23,17),(35,28,20),(42,32,23),(23,20,16)],'skin':[(158,123,92),(165,133,104),(147,115,86),(172,138,107)]}
regions={}
for idx,(name,palette) in enumerate(palettes.items()):
 x=(idx%4)*tile;y=(idx//4)*tile;regions[name]=(x,y,tile,tile)
 patch=Image.new('RGB',(64,64),palette[0]);d=ImageDraw.Draw(patch)
 for _ in range(470):
  xx=random.randrange(64); yy=random.randrange(64);w=random.randrange(2,7);h=random.randrange(2,7)
  d.rectangle((xx,yy,xx+w,yy+h),fill=random.choice(palette))
 atlas.paste(patch.resize((tile,tile),Image.Resampling.NEAREST),(x,y))
# Reference facial features retained, broad illumination removed while preserving eyes/brows/lips.
face=src.crop((267,91,355,193)).resize((64,80),Image.Resampling.BILINEAR)
blur=face.convert('L').filter(ImageFilter.GaussianBlur(9));px=face.load();bl=blur.load()
for yy in range(face.height):
 for xx in range(face.width):
  c=px[xx,yy]; factor=min(1.6,max(.82,124/max(40,bl[xx,yy])))
  px[xx,yy]=tuple(min(230,int(v*factor)) for v in c)
x,y=512,256;regions['face']=(x,y,tile,tile)
atlas.paste(face.resize((tile,tile),Image.Resampling.NEAREST),(x,y))
# Neutral metal, rubber and luminous lens swatches.
for name,x,y,c,r in [('metal',768,256,(57,54,47),120),('rubber',0,512,(26,25,23),230),('lens',256,512,(243,222,170),70),('sole',512,512,(104,94,74),225)]:
 regions[name]=(x,y,tile,tile);ImageDraw.Draw(atlas).rectangle((x,y,x+tile-1,y+tile-1),fill=c);ImageDraw.Draw(rough).rectangle((x,y,x+tile-1,y+tile-1),fill=r)
atlas.save(p/'textures/Survivor_BaseColor.png');rough.save(p/'textures/Survivor_Roughness.png')
import json
(p/'textures/atlas-regions.json').write_text(json.dumps(regions,indent=2))
