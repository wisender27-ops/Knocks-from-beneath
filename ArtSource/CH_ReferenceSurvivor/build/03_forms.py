import bpy,bmesh,math,runpy,json,random
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
H=runpy.run_path(str(ROOT.parents[1]/'.agents/skills/blender-image-to-3d/assets/build_template.py'))
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_blockout.blend'))
# Keep the approved blockout as a separate editable checkpoint.
bpy.context.preferences.filepaths.save_version=0
if not (ROOT/'CH_ReferenceSurvivor_blockout.blend').exists():bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_blockout.blend'))
for o in list(bpy.data.objects):
 if o.get('owner')=='phase:03_forms' or any(k in o.name for k in ['_Hair_LOD0','_Hand_L_LOD0','_Hand_R_LOD0','_Nose_LOD0']):bpy.data.objects.remove(o,do_unlink=True)
col=H['col'];loft=H['loft'];box=H['box']
def material_role(ob,role):ob['role']=role;ob['owner']='phase:03_forms';return ob
def tube(name,a,b,radii,role,n=8):
 a,b=Vector(a),Vector(b);axis=(b-a).normalized();u=Vector((0,1,0));v=axis.cross(u).normalized();u=v.cross(axis).normalized()
 secs=[]
 for t,r1,r2 in radii:
  c=a.lerp(b,t);secs.append([c+u*(math.cos(i*math.tau/n)*r1)+v*(math.sin(i*math.tau/n)*r2) for i in range(n)])
 return material_role(loft('CH_ReferenceSurvivor_'+name+'_LOD0',secs,smooth=False),role)
def cube(name,size,loc,role):return material_role(box('CH_ReferenceSurvivor_'+name+'_LOD0',size,location=loc),role)
# Closed wedge nose, fitted between brow and philtrum rather than a detached box.
verts=[(-.018,-.096,1.677),(.018,-.096,1.677),(-.020,-.104,1.624),(.020,-.104,1.624),(-.010,-.132,1.639),(.010,-.132,1.639)]
me=bpy.data.meshes.new('NoseMesh');me.from_pydata(verts,[],[(0,1,5,4),(0,4,2),(1,3,5),(2,4,5,3),(0,2,3,1)]);me.update();ob=bpy.data.objects.new('CH_ReferenceSurvivor_Nose_LOD0',me);col('LOW').objects.link(ob);material_role(ob,'skin')
# Skull cap, lowered behind the ears, open front above the eyebrows.
secs=[]
for level in range(4):
 ring=[]
 for i in range(16):
  a=i*math.tau/16;front=max(0,-math.sin(a));back=max(0,math.sin(a));z=(1.677+front*.057-back*.055) if level==0 else [0,1.752,1.78,1.80][level]
  rx=[.094,.102,.077,.020][level];ry=[.092,.095,.075,.02][level]
  ring.append((rx*math.cos(a),.005+ry*math.sin(a),z))
 secs.append(ring)
material_role(loft('CH_ReferenceSurvivor_HairCap_LOD0',secs,cap_ends=False,smooth=False),'hair')
random.seed(42)
for i in range(32):
 a=random.uniform(0,math.tau);z=random.uniform(1.71,1.785);rx=.088;ry=.077
 start=Vector((rx*math.cos(a),.008+ry*math.sin(a),z));end=start+Vector((random.uniform(-.025,.025),random.uniform(-.020,.020),-random.uniform(.035,.085)))
 if math.sin(a)<-.35:end.z=max(end.z,1.691)
 tube('HairLock%02d'%i,start,end,[(0,.017,.021),(.5,.020,.014),(1,.004,.007)],'hair',4)
for s,label in [(-1,'R'),(1,'L')]:
 # Palm and fingers in a relaxed pose; later transformed into the rig rest pose.
 tube('Palm_'+label,(s*.337,-.087,.945),(s*.346,-.088,.86),[(0,.028,.041),(.35,.030,.047),(1,.022,.041)],'skin')
 for j,finger in enumerate(['Index','Middle','Ring','Pinky']):
  xx=s*(.313+j*.019);length=[.066,.073,.069,.056][j]
  tube(finger+'_'+label,(xx,-.087,.865),(xx+s*.006,-.097,.865-length),[(0,.016,.008),(.3,.015,.008),(.6,.014,.007),(1,.010,.006)],'skin',6)
 tube('Thumb_'+label,(s*.31,-.104,.907),(s*.293,-.117,.856),[(0,.016,.014),(.5,.014,.012),(1,.011,.010)],'skin',6)
 # Jacket hem, ribbed cuffs, pull tabs and hanging cords are separate geometry.
 tube('Cuff_'+label,(s*.326,-.014,.975),(s*.340,-.016,.928),[(0,.054,.063),(1,.051,.061)],'jacket')
 tube('Drawstring_'+label,(s*.075,-.119,1.46),(s*.071,-.14,1.29),[(0,.004,.004),(1,.004,.004)],'shirt',4)
 cube('Zipper_'+label,(.009,.006,.46),(s*.075,-.150,1.16),'metal')
 cube('Pocket_'+label,(.072,.012,.10),(s*.145,-.135,1.06),'jacket')
 for k in range(4):cube('Lace%d_'%k+label,(.065,.008,.006),(s*.17,-.10+k*.019,.117-k*.003),'shirt')
 cube('Sole_'+label,(.187,.276,.018),(s*.182,-.027,.014),'sole')
# Tag existing masses, then add additional joint rings for deformation without changing silhouette.
for o in list(col('LOW').objects):
 if o.type!='MESH':continue
 if 'role' not in o:
  name=o.name
  role='jacket' if any(k in name for k in ['Jacket','Sleeve','Hood']) else 'denim' if any(k in name for k in ['Jeans','Pelvis']) else 'shirt' if 'Shirt' in name else 'shoes' if 'Shoe' in name else 'metal' if 'Flashlight' in name else 'skin'
  o['role']=role
 if any(k in o.name for k in ['Jeans','Sleeve','Jacket','Shirt','Pelvis']):
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=2,use_grid_fill=True);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(o.data);bm.free()
 if 'Jacket_' in o.name:
  bpy.context.view_layer.objects.active=o
  mod=o.modifiers.new('GarmentThickness','SOLIDIFY');mod.thickness=.004;mod.offset=-1
  bpy.ops.object.modifier_apply(modifier=mod.name)
# One texture atlas and one character material. Point sampling preserves the reference's pixel style.
regions=json.loads((ROOT/'textures/atlas-regions.json').read_text())
mat=bpy.data.materials.get('Survivor_Atlas') or bpy.data.materials.new('Survivor_Atlas')
n=mat.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled');bs.inputs['Roughness'].default_value=.88;bs.inputs['Specular IOR Level'].default_value=.12
tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ROOT/'textures/Survivor_BaseColor.png'),check_existing=True);tex.interpolation='Closest';tex.extension='EXTEND'
r=n.new('ShaderNodeTexImage');r.image=bpy.data.images.load(str(ROOT/'textures/Survivor_Roughness.png'),check_existing=True);r.image.colorspace_settings.name='Non-Color';r.interpolation='Closest'
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);mat.node_tree.links.new(r.outputs['Color'],bs.inputs['Roughness']);mat.node_tree.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
for o in col('LOW').objects:
 if o.type!='MESH':continue
 # Applied local coordinates simplify skinning and exporter contracts.
 o.data.transform(o.matrix_local);o.matrix_basis.identity();o.data.materials.clear();o.data.materials.append(mat)
 uv=o.data.uv_layers.new(name='UVMap') if not o.data.uv_layers else o.data.uv_layers.active
 role=o['role'];reg=regions[role]
 for f in o.data.polygons:
  face_region=('Head_' in o.name and f.normal.y<-.45 and f.center.z<1.73)
  reg=regions['face'] if face_region else regions[role];tx,ty,tw,th=reg
  for li in f.loop_indices:
   co=o.data.vertices[o.data.loops[li].vertex_index].co
   if face_region:u=(co.x/.166)+.5;v=(co.z-1.53)/.19
   else:
    # Triplanar projection per face; overlapping UVs intentionally share tileable cloth.
    axis=max(range(3),key=lambda k:abs(f.normal[k]));a,b=[k for k in range(3) if k!=axis]
    u=(co[a]*3.0)%1;v=(co[b]*3.0)%1
    # Prevent wraps inside a polygon by using its shared tile origin.
    origin_u=math.floor(f.center[a]*3);origin_v=math.floor(f.center[b]*3)
    u=max(.01,min(.99,co[a]*3-origin_u));v=max(.01,min(.99,co[b]*3-origin_v))
   u=max(.012,min(.988,u));v=max(.012,min(.988,v))
   uv.data[li].uv=((tx+4+u*(tw-8))/1024,1-(ty+4+(1-v)*(th-8))/1024)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
for im in [tex.image,r.image]:im.pack()
bpy.context.scene.view_settings.view_transform='Standard'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms_unrefined.blend'))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_forms.blend'))

