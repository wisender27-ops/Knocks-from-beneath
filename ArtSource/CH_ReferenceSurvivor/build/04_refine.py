import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms_unrefined.blend'))
profiles=json.loads((ROOT/'build/shape-corrections.json').read_text())
for o in bpy.data.collections['LOW'].objects:
 if o.type!='MESH' or any(k in o.name for k in ['Palm','Index','Middle','Ring','Pinky','Thumb','Flashlight','Head','Hair','Nose','Ear','Neck']):continue
 for v in o.data.vertices:
  z=v.co.z
  for view,axis in [('front',0),('side',1)]:
   rows=profiles[view];row=min(rows,key=lambda r:abs(r['z']-z))
   if abs(row['z']-z)>.025:continue
   value=v.co.x if view=='front' else -v.co.y
   idx=min(range(len(row['model'])),key=lambda j:abs(value-sum(row['model'][j])/2))
   a,b=row['model'][idx];aa,bb=row['ref'][idx]
   scale=max(.8,min(1.2,(bb-aa)/max(.02,b-a)));centre=(aa+bb)/2;oldcentre=(a+b)/2
   change=max(-.03,min(.03,centre+(value-oldcentre)*scale-value))
   # Fore/aft head correction is intentionally excluded to retain the user's approved posture.
   if view=='front':v.co.x+=change*.9
   else:v.co.y-=change*.85
 o.data.update()
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms_refined.blend'));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms.blend'))
