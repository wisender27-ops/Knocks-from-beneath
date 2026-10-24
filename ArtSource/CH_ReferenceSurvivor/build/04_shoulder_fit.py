import bpy
from mathutils import Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms_secondary.blend'))
for s,label in [(-1,'R'),(1,'L')]:
 o=bpy.data.objects['CH_ReferenceSurvivor_Sleeve_'+label+'_LOD0'];a=Vector((s*.165,.01,1.46));d=Vector((s*.172,-.025,-.515));length=d.length;axis=d.normalized()
 for v in o.data.vertices:
  t=(v.co-a).dot(axis)/length
  if t<.16:
   centre=a+axis*((v.co-a).dot(axis));f=.52+.48*max(0,min(1,t/.16));v.co=centre+(v.co-centre)*f
 o.data.update()
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms.blend'))
