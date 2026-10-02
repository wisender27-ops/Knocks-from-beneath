import bpy,math,json
from pathlib import Path
from mathutils import Vector,Quaternion
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms.blend'))
low=bpy.data.collections['LOW']
# Rest pose rotates the full arm, preserving authored UVs.
arm_q={};arm_origin={}
for s,label in [(-1,'R'),(1,'L')]:
 origin=Vector((s*.165,.01,1.46));direction=Vector((s*.172,-.025,-.515));q=direction.rotation_difference(Vector((s,0,0)));arm_q[label]=q;arm_origin[label]=origin
 for o in low.objects:
  if any(k in o.name for k in ['Sleeve_'+label,'Cuff_'+label,'Palm_'+label,'Index_'+label,'Middle_'+label,'Ring_'+label,'Pinky_'+label,'Thumb_'+label]):
   for v in o.data.vertices:v.co=origin+q@(v.co-origin)
# Flashlight remains a separate prop, not part of the Mixamo upload.
props=bpy.data.collections.get('PROPS') or bpy.data.collections.new('PROPS')
if props.name not in bpy.context.scene.collection.children:bpy.context.scene.collection.children.link(props)
for o in list(low.objects):
 if 'Flashlight' in o.name:low.objects.unlink(o);props.objects.link(o)
arm_data=bpy.data.armatures.new('SurvivorSkeleton');rig=bpy.data.objects.new('CH_ReferenceSurvivor_Rig',arm_data);bpy.data.collections['RIG_DEF'].objects.link(rig);rig.show_in_front=True
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
bones={}
def bone(name,head,tail,parent=None):
 b=arm_data.edit_bones.new(name);b.head=head;b.tail=tail
 if parent:b.parent=arm_data.edit_bones[parent]
 bones[name]=(Vector(head),Vector(tail));return b
bone('Hips',(0,.022,.90),(0,.018,1.02))
bone('Spine',(0,.018,1.02),(0,.018,1.20),'Hips');bone('Spine1',(0,.018,1.20),(0,.015,1.36),'Spine');bone('Spine2',(0,.015,1.36),(0,.01,1.48),'Spine1');bone('Neck',(0,.01,1.48),(0,-.005,1.57),'Spine2');bone('Head',(0,-.005,1.57),(0,-.003,1.78),'Neck')
for s,label,side in [(-1,'R','Right'),(1,'L','Left')]:
 origin=arm_origin[label];q=arm_q[label]
 def tp(co):return origin+q@(Vector(co)-origin)
 shoulder=origin;elbow=tp((s*.251,-.0025,1.2025));wrist=tp((s*.337,-.015,.945));handtail=tp((s*.346,-.088,.86))
 bone(side+'Shoulder',(s*.045,.01,1.455),shoulder,'Spine2');bone(side+'Arm',shoulder,elbow,side+'Shoulder');bone(side+'ForeArm',elbow,wrist,side+'Arm');bone(side+'Hand',wrist,handtail,side+'ForeArm')
 for j,finger in enumerate(['Index','Middle','Ring','Pinky']):
  xx=s*(.313+j*.019);length=[.066,.073,.069,.056][j];a=tp((xx,-.087,.865));b=tp((xx+s*.006,-.097,.865-length))
  for k in range(3):bone(side+'Hand'+finger+str(k+1),a.lerp(b,k/3),a.lerp(b,(k+1)/3),side+'Hand' if k==0 else side+'Hand'+finger+str(k))
 a=tp((s*.31,-.104,.907));b=tp((s*.293,-.117,.856))
 for k in range(3):bone(side+'HandThumb'+str(k+1),a.lerp(b,k/3),a.lerp(b,(k+1)/3),side+'Hand' if k==0 else side+'HandThumb'+str(k))
 bone(side+'UpLeg',(s*.097,.023,.89),(s*.145,.079,.46),'Hips');bone(side+'Leg',(s*.145,.079,.46),(s*.17,.015,.12),side+'UpLeg');bone(side+'Foot',(s*.17,.015,.12),(s*.18,-.145,.06),side+'Leg');bone(side+'ToeBase',(s*.18,-.145,.06),(s*.18,-.175,.04),side+'Foot')
bpy.ops.object.mode_set(mode='OBJECT')
def dist(p,a,b):
 d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared));return (p-(a+d*t)).length
for o in low.objects:
 if o.type!='MESH':continue
 name=o.name;candidates=[];label='L' if '_L_' in name else 'R' if '_R_' in name else None;side='Left' if label=='L' else 'Right'
 if any(k in name for k in ['Head_','Hair','Ear_','Nose_']):candidates=['Head']
 elif 'Neck_' in name:candidates=['Neck','Head']
 elif 'Sleeve_' in name:candidates=[side+'Arm',side+'ForeArm']
 elif 'Cuff_' in name:candidates=[side+'ForeArm',side+'Hand']
 elif 'Palm_' in name:candidates=[side+'Hand']
 elif any(f+'_' in name for f in ['Index','Middle','Ring','Pinky','Thumb']):
  finger=next(f for f in ['Index','Middle','Ring','Pinky','Thumb'] if f+'_' in name);candidates=[side+'Hand'+finger+str(k) for k in range(1,4)]
 elif 'Jeans_' in name:candidates=[side+'UpLeg',side+'Leg']
 elif any(k in name for k in ['Shoe_','Lace','Sole_']):candidates=[side+'Foot']
 elif 'Pelvis_' in name:candidates=['Hips']
 elif any(k in name for k in ['Hood','Drawstring']):candidates=['Spine2','Neck']
 else:candidates=['Hips','Spine','Spine1','Spine2']
 groups={n:o.vertex_groups.new(name=n) for n in candidates}
 for v in o.data.vertices:
  ranks=sorted([(dist(v.co,*bones[n]),n) for n in candidates])[:2]
  weights=[1/(d+.018)**4 for d,n in ranks];total=sum(weights)
  for (d,n),w in zip(ranks,weights):groups[n].add([v.index],w/total,'REPLACE')
 mod=o.modifiers.new('Skin','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=False;o.parent=rig
# Hand attachment sockets, with bone parenting.
for side in ['Left','Right']:
 o=bpy.data.objects.new('SOCKET_'+side+'Hand',None);bpy.data.collections['SOCKETS'].objects.link(o);o.parent=rig;o.parent_type='BONE';o.parent_bone=side+'Hand';o.location=(0,0,0)
# Consolidate the delivery mesh: one skin and one material; authoring parts remain in forms.blend.
for o in bpy.context.selected_objects:o.select_set(False)
meshes=[o for o in low.objects if o.type=='MESH']
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();body=bpy.context.object
body.name='CH_ReferenceSurvivor_Body_LOD0';body.data.name='SurvivorBodyMesh'
for v in body.data.vertices:
 entries=[(g.group,g.weight) for g in v.groups if g.weight>=.01]
 total=sum(w for i,w in entries)
 for g in list(v.groups):body.vertex_groups[g.group].remove([v.index])
 for idx,w in entries:body.vertex_groups[idx].add([v.index],w/total,'REPLACE')
for level,ratio in [(1,.55),(2,.27)]:
 c=bpy.data.collections.new('LOD'+str(level));bpy.context.scene.collection.children.link(c)
 lod=body.copy();lod.data=body.data.copy();lod.name='CH_ReferenceSurvivor_Body_LOD'+str(level);lod.data.name='SurvivorLOD'+str(level)+'Mesh';c.objects.link(lod)
 bpy.context.view_layer.objects.active=lod;d=lod.modifiers.new('LODReduction','DECIMATE');d.ratio=ratio;bpy.ops.object.modifier_apply(modifier=d.name);lod.hide_render=True;lod.hide_set(True)
# Save clean rest pose delivery before creating diagnostic actions.
for im in bpy.data.images:
 if im.type=='IMAGE' and im.has_data:im.pack()
bpy.context.scene.view_settings.view_transform='Standard';bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_rigged.blend'))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_master.blend'))
# Review-only action: reference pose and articulated extremes, never exported as gameplay animation.
for frame in [1,10,20,30]:
 for pb in rig.pose.bones:pb.rotation_mode='QUATERNION';pb.rotation_quaternion=Quaternion()
 if frame==1:
  for label,side in [('L','Left'),('R','Right')]:
   pb=rig.pose.bones[side+'Arm'];basis=pb.bone.matrix_local.to_3x3().to_quaternion()
   pb.rotation_quaternion=basis.inverted()@arm_q[label].inverted()@basis
 if frame==10:
  for s,side in [(1,'Left'),(-1,'Right')]:rig.pose.bones[side+'Arm'].rotation_quaternion=Quaternion((0,1,0),s*math.radians(-65))
 if frame==20:
  for side in ['Left','Right']:rig.pose.bones[side+'ForeArm'].rotation_quaternion=Quaternion((0,0,1),math.radians(85));rig.pose.bones[side+'Leg'].rotation_quaternion=Quaternion((1,0,0),math.radians(-65))
 if frame==30:
  for side in ['Left','Right']:rig.pose.bones[side+'UpLeg'].rotation_quaternion=Quaternion((1,0,0),math.radians(45));rig.pose.bones[side+'Leg'].rotation_quaternion=Quaternion((1,0,0),math.radians(-85))
  rig.pose.bones['Spine'].rotation_quaternion=Quaternion((0,0,1),math.radians(25))
 if frame!=1:
  for pb in rig.pose.bones:
   basis=pb.bone.matrix_local.to_3x3().to_quaternion();pb.rotation_quaternion=basis.inverted()@pb.rotation_quaternion@basis
 for pb in rig.pose.bones:pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=pb.name)
rig.animation_data.action.name='REVIEW_ONLY_DeformationChecks';bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_review.blend'))
(ROOT/'review/rig-contract.json').write_text(json.dumps({'bones':len(bones),'rest_pose':'T','animations':'Added later through Mixamo; review action excluded from delivery','weights':'normalized, up to two influences','Mixamo_upload':'Unrigged T-pose mesh export; service autorig not tested'},indent=2))



