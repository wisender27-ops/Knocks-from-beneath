from PIL import Image,ImageDraw
from pathlib import Path
p=Path('ArtSource/CH_ReferenceSurvivor')
a=Image.open(p/'ref/front.png').convert('RGB');b=Image.open(p/'review/02_blockout/clay_front.png').convert('RGBA');c=Image.open(p/'review/02_blockout/clay_side.png').convert('RGBA')
out=Image.new('RGB',(1110,650),(45,45,45));d=ImageDraw.Draw(out)
for x,im,label in [(0,a,'REFERENCE'),(370,b,'BLOCKOUT FRONT'),(740,c,'BLOCKOUT SIDE')]:
 im.thumbnail((360,605));xx=x+(370-im.width)//2
 if im.mode=='RGBA':out.paste(im,(xx,35),im)
 else:out.paste(im,(xx,35))
 d.text((x+20,12),label,fill=(235,235,235))
out.save(p/'review/02_blockout/review_sheet.png')
