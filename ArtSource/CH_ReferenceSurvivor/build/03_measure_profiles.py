from PIL import Image
import json
from pathlib import Path
p=Path(__file__).resolve().parents[1];s=.001865285
profiles={}
for view,axis in [('front',241),('side',130)]:
 ref=Image.open(p/'ref'/f'{view}_clean.png').getchannel('A');mod=Image.open(p/'review/03_forms'/f'sil_{view}.png').getchannel('A')
 rows=[]
 for y in range(0,ref.height,4):
  def spans(im):
   arr=[im.getpixel((x,y))>127 for x in range(im.width)];runs=[];start=None
   for x,on in enumerate(arr+[False]):
    if on and start is None:start=x
    if not on and start is not None:
     if x-start>=6:runs.append([start,x-1])
     start=None
   return runs
  rr,mm=spans(ref),spans(mod)
  if len(rr)==len(mm) and 1<=len(rr)<=3:rows.append({'z':(988-y)*s,'ref':[[(v-axis)*s for v in a] for a in rr],'model':[[(v-axis)*s for v in a] for a in mm]})
 profiles[view]=rows
(p/'build/shape-corrections.json').write_text(json.dumps(profiles))
