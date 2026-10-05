import importlib.util, math, json, hashlib, shutil, subprocess,sys
from pathlib import Path
W=next(p for p in Path(__file__).resolve().parents if (p/'art/manifests').is_dir())
OUT=Path(__file__).parent
path=W/'art/source/facilities/processor-r1/generate_processor_r1.py'
spec=importlib.util.spec_from_file_location('processor',path)
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
def bounds(v):return [[min(p[i] for p in v) for i in range(3)],[max(p[i] for p in v) for i in range(3)]]
def gap(a,b):return max(max(a[0][i]-b[1][i],b[0][i]-a[1][i]) for i in range(3))
checks={}
def check(label,newparts,oldparts,transform):
 minimum=float('inf')
 for step in range(101):
  for _,a,_ in newparts:
   for _,b,_ in oldparts:
    minimum=min(minimum,gap(bounds(a),bounds(transform(b,step/100))))
 assert minimum>=.005-1e-9,(label,minimum)
 checks[label]=minimum
check('collar_vs_feed_gate',m.g_dust_collar(),m.g_feed_gate(),lambda v,t:m._rot_x_about(v,m.GATE_PIVOT,25*t))
check('collar_vs_stop_gate',m.g_dust_collar(),m.g_stop_gate(),lambda v,t:[(x,y-.8*t,z) for x,y,z in v])
check('collar_vs_input_crate',m.g_dust_collar(),m.g_crate(m.BOX_IN_CZ,'in'),lambda v,t:v)
check('inner_cover_vs_service_cover',m.g_inner_cover(),m.g_service_cover(),lambda v,t:m._rot_y_about(v,m.COVER_PIVOT,70*t))
check('heat_panel_vs_press_ram',m.g_heat_panel(),m.g_press_ram(),lambda v,t:[(x,y-.35*t,z) for x,y,z in v])
check('heat_panel_vs_press_guide',m.g_heat_panel(),m.g_press_guide(),lambda v,t:v)
# Solar guard is below every sampled folding panel. The cover opens outwards.
p=W/'art/source/facilities/solar-r1/solar-r1.gltf'
g=json.loads(p.read_text());binary=(p.parent/'solar-r1.bin').read_bytes()
import struct
parents={c:i for i,n in enumerate(g['nodes']) for c in n.get('children',[])}
def verts(n):
 a=g['accessors'][g['meshes'][n['mesh']]['primitives'][0]['attributes']['POSITION']]
 b=g['bufferViews'][a['bufferView']];start=b.get('byteOffset',0)+a.get('byteOffset',0)
 return [struct.unpack_from('<fff',binary,start+i*12) for i in range(a['count'])]
def pose_world(index,v,angles):
 n=g['nodes'][index];a=angles.get(n['name'])
 if a is None:
  q=n.get('rotation',[0,0,0,1]);a=math.degrees(2*math.atan2(q[2],q[3]))
 co,si=math.cos(math.radians(a)),math.sin(math.radians(a))
 t=n.get('translation',[0,0,0]);v=(co*v[0]-si*v[1]+t[0],si*v[0]+co*v[1]+t[1],v[2]+t[2])
 return pose_world(parents[index],v,angles) if index in parents else v
def lerp_keys(values,t):
 k=min(int(t),3);f=t-k
 return values[k]*(1-f)+values[k+1]*f
panel_ids=[i for i,n in enumerate(g['nodes']) if 'mesh' in n and any(n['name'].startswith(a) for a in ['LeftInner','LeftOuter','RightInner','RightOuter'])]
fixed=[i for i,n in enumerate(g['nodes']) if 'mesh' in n and n['name'].startswith(('FixedCableGuard','ProtectedElectronics','ElectronicsInnerCover'))]
minimum=float('inf')
for sample in range(101):
 t=4*sample/100
 angles={prefix+part:sign*lerp_keys(keys,t) for prefix,sign in [('Left',-1),('Right',1)] for part,keys in [('Inner',[90,90,90,51,12]),('Outer',[180,270,360,360,360])]}
 for i in panel_ids:
  moving=bounds([pose_world(i,v,angles) for v in verts(g['nodes'][i])])
  for j in fixed:
   static=bounds([pose_world(j,v,{}) for v in verts(g['nodes'][j])])
   minimum=min(minimum,gap(moving,static))
assert minimum>=.005-1e-9,minimum
checks['solar_protection_vs_folding_panels']=minimum
cover_i=next(i for i,n in enumerate(g['nodes']) if n['name']=='CoverPanel')
inner_i=next(i for i,n in enumerate(g['nodes']) if n['name']=='ElectronicsInnerCover')
inner=bounds([pose_world(inner_i,v,{}) for v in verts(g['nodes'][inner_i])])
# Cover rotates around X; sample actual plate vertices with the parent hinge.
cover=g['nodes'][cover_i];hinge=g['nodes'][parents[cover_i]]
minimum=float('inf')
for step in range(101):
 a=math.radians(-60*step/100);co,si=math.cos(a),math.sin(a)
 pts=[]
 for v in verts(cover):
  x,y,z=[v[k]+cover['translation'][k] for k in range(3)]
  pts.append((x+hinge['translation'][0],co*y-si*z+hinge['translation'][1],si*y+co*z+hinge['translation'][2]))
 minimum=min(minimum,gap(inner,bounds(pts)))
assert minimum>=.005-1e-9,minimum
checks['solar_inner_cover_vs_service_cover']=minimum
(OUT/'facility-clearance.json').write_text(json.dumps({'checks':checks,'samples_per_motion':101,'method':'source vertices transformed, disjoint AABB axes certify separation; finite samples only','engineering_reliability':'NOT_RUN'},indent=2)+'\n')
# Rerun each generator and independently compare all source binary bytes.
for a in ['solar','processor']:
 d=W/'art/source/facilities'/f'{a}-r1'
 hashes={f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in d.iterdir() if f.suffix in ['.gltf','.bin','.glb']}
 r=subprocess.run([sys.executable,str(d/f'generate_{a}_r1.py')],capture_output=True,text=True)
 assert r.returncode==0,r.stdout+r.stderr
 for name,expected in hashes.items():assert hashlib.sha256((d/name).read_bytes()).hexdigest()==expected,name
 shutil.copyfile(d/f'{a}-r1.glb',W/'prototype/assets/facilities'/f'{a}-r1'/f'{a}-r1.glb')
print('PASS 8 facility separation groups, 101 samples each; independent source regeneration byte match.')
