import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_rigged.blend'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
body=bpy.data.objects['CH_ReferenceSurvivor_Body_LOD0']
def select(obs):
 for o in bpy.context.selected_objects:o.select_set(False)
 for o in obs:o.hide_set(False);o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0]
for level in [1,2]:
 lod=bpy.data.objects['CH_ReferenceSurvivor_Body_LOD'+str(level)];lod.hide_render=False;select([lod,rig])
 bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports'/('CH_ReferenceSurvivor_LOD'+str(level)+'.fbx')),use_selection=True,add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
# Mixamo upload is deliberately unrigged, in an unobstructed T-pose, with separate fingers.
select([body]);body.parent=None
for mod in list(body.modifiers):body.modifiers.remove(mod)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports/CH_ReferenceSurvivor_Mixamo_TPose.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
