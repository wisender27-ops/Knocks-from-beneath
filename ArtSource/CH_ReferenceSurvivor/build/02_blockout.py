import bpy, math, runpy
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
helper=runpy.run_path(str(ROOT.parents[1]/'.agents/skills/blender-image-to-3d/assets/build_template.py'))
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_blockout.blend'))
for ob in list(bpy.data.objects):
 if ob.get('owner')=='phase:02_blockout': bpy.data.objects.remove(ob,do_unlink=True)
loft=helper['loft']; box=helper['box']
def mat(name,c):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.diffuse_color=(*c,1);return m
skin=mat('Skin_blockout',(.45,.31,.22));jacket=mat('Jacket_blockout',(.12,.105,.085));denim=mat('Denim_blockout',(.13,.145,.18));hair=mat('Hair_blockout',(.055,.045,.032));shirt=mat('Shirt_blockout',(.36,.33,.28));shoe=mat('Shoes_blockout',(.17,.14,.105))
def finish(ob,m):
 ob['owner']='phase:02_blockout';ob.data.materials.append(m);return ob
def rings(name,stations,m,n=12):
 # Station: z, x centre, y centre, half width, half depth. Dimensions from turnaround.
 secs=[[(x+rx*math.cos(2*math.pi*i/n),y+ry*math.sin(2*math.pi*i/n),z) for i in range(n)] for z,x,y,rx,ry in stations]
 return finish(loft('CH_ReferenceSurvivor_'+name+'_LOD0',secs,smooth=False),m)
def limb(name,a,b,rs,m):
 a,b=Vector(a),Vector(b);d=(b-a).normalized();u=Vector((0,1,0));v=d.cross(u).normalized()
 sections=[]
 for t,r in rs:
  c=a.lerp(b,t);sections.append([c+u*(math.cos(i*math.tau/10)*r*.85)+v*(math.sin(i*math.tau/10)*r) for i in range(10)])
 return finish(loft('CH_ReferenceSurvivor_'+name+'_LOD0',sections,smooth=False),m)
rings('Pelvis',[(.83,0,.025,.165,.10),(.94,0,.018,.19,.115),(1.02,0,.015,.174,.108)],denim)
rings('Shirt',[(.95,0,-.015,.14,.09),(1.18,0,-.005,.145,.10),(1.40,0,.01,.177,.11),(1.51,0,.012,.17,.08)],shirt)
rings('Jacket',[(.926,0,.015,.191,.125),(1.02,0,.016,.199,.131),(1.23,0,.02,.178,.125),(1.43,0,.024,.185,.13),(1.52,0,.025,.154,.10)],jacket)
# Remove front-centre panels so the shirt reads as an open jacket.
ob=bpy.data.objects['CH_ReferenceSurvivor_Jacket_LOD0'];import bmesh
bm=bmesh.new();bm.from_mesh(ob.data)
faces=[f for f in bm.faces if f.calc_center_median().y<-.04 and abs(f.calc_center_median().x)<.095]
bmesh.ops.delete(bm,geom=faces,context='FACES');bm.to_mesh(ob.data);bm.free()
rings('Neck',[(1.45,0,0,.062,.063),(1.56,0,0,.066,.068)],skin)
rings('Head',[(1.54,0,-.023,.055,.057),(1.58,0,-.015,.077,.077),(1.64,0,.004,.083,.087),(1.71,0,.013,.088,.086),(1.765,0,.019,.074,.072)],skin)
rings('Hair',[(1.67,0,.025,.094,.083),(1.735,0,.025,.109,.097),(1.78,0,.025,.09,.083),(1.80,0,.025,.025,.023)],hair)
rings('Hood',[(1.35,0,.105,.12,.078),(1.42,0,.10,.172,.085),(1.51,0,.068,.184,.079),(1.55,0,.05,.115,.066)],jacket)
for s,label in [(-1,'R'),(1,'L')]:
 limb('Sleeve_'+label,(s*.165,.01,1.46),(s*.337,-.015,.945),[(0,.083),(.25,.075),(.5,.068),(.75,.068),(1,.060)],jacket)
 limb('Hand_'+label,(s*.337,-.015,.945),(s*.342,-.021,.78),[(0,.044),(.35,.050),(.7,.047),(1,.025)],skin)
 rings('Jeans_'+label,[(.105,s*.17,.012,.074,.082),(.20,s*.163,.009,.069,.075),(.45,s*.144,.018,.071,.078),(.70,s*.115,.022,.079,.09),(.89,s*.097,.023,.085,.098)],denim)
 rings('Shoe_'+label,[(.001,s*.185,-.047,.104,.146),(.05,s*.185,-.046,.104,.143),(.10,s*.17,-.028,.085,.132),(.135,s*.166,.012,.062,.068)],shoe)
 limb('Ear_'+label,(s*.082,.007,1.685),(s*.085,.005,1.615),[(0,.021),(.5,.025),(1,.016)],skin)
# Flashlight is separate; in the reference character's right hand.
limb('Flashlight',(-.345,-.015,.844),(-.36,-.10,.73),[(0,.022),(.65,.024),(.78,.039),(1,.039)],hair)
# Depth correction from the profile view; preserves front-view proportions.
for ob in bpy.data.collections['LOW'].objects:
 if 'Head_' in ob.name or 'Hair_' in ob.name or 'Ear_' in ob.name:
  for v in ob.data.vertices: v.co.y -= .020
 if 'Jacket_' in ob.name or 'Shirt_' in ob.name:
  for v in ob.data.vertices: v.co.y = v.co.y * 1.10 - .023
 if 'Hand_' in ob.name:
  for v in ob.data.vertices: v.co.y -= .075
 if 'Jeans_' in ob.name:
  for v in ob.data.vertices: v.co.y += .065*math.exp(-((v.co.z-.38)/.25)**2)
 if 'Shoe_' in ob.name:
  for v in ob.data.vertices: v.co.y += .02
 if 'Flashlight_' in ob.name:
  for v in ob.data.vertices: v.co.y -= .075
finish(box('CH_ReferenceSurvivor_Nose_LOD0',(.036,.038,.054),location=(0,-.118,1.654)),skin)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CH_ReferenceSurvivor_master.blend'))



