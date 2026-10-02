import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms_refined.blend'))
for o in bpy.data.collections['LOW'].objects:
 if o.type!='MESH':continue
 for v in o.data.vertices:
  if 'Hood' in o.name and v.co.z>1.47:
   f=min(1,(v.co.z-1.47)/.07);v.co.x*=1-.25*f;v.co.z-=.018*f
  if 'Hair' in o.name:v.co.y=.005+(v.co.y-.005)*1.18
  if any(k in o.name for k in ['Jacket','Sleeve']):
   f=max(0,min(1,(v.co.y+.015)/.1));v.co.x*=1+.07*f
 o.data.update()
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms_secondary.blend'));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms.blend'))
