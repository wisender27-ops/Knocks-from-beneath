import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_rigged.blend'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
for level in range(3):
    body=bpy.data.objects['CH_ReferenceSurvivor_Body_LOD'+str(level)]
    bpy.ops.object.select_all(action='DESELECT')
    body.hide_set(False);body.hide_render=False;body.select_set(True)
    bpy.context.view_layer.objects.active=body
    head_index=body.vertex_groups['Head'].index
    for v in body.data.vertices:
        v.select=any(g.group==head_index and g.weight>.5 for g in v.groups)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')
    head=next(o for o in bpy.context.selected_objects if o!=body)
    head.name='CH_ReferenceSurvivor_Head_LOD'+str(level)
    bpy.ops.object.select_all(action='DESELECT')
    for o in [body,head,rig]:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=rig
    name='CH_ReferenceSurvivor'+('' if level==0 else '_LOD'+str(level))+'.fbx'
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports'/name),use_selection=True,add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_game.blend'))
