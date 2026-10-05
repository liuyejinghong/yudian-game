"""Finite source-vertex checks for the solar service cover and its handle."""
from pathlib import Path
import math,json,struct,hashlib,subprocess,sys,importlib.util
W=next(p for p in [Path.cwd(),*Path(__file__).resolve().parents] if (p/'art/manifests/solar-r1.json').is_file())
D=W/'art/source/facilities/solar-r1';g=json.loads((D/'solar-r1.gltf').read_text());data=(D/'solar-r1.bin').read_bytes()
parents={c:i for i,n in enumerate(g['nodes']) for c in n.get('children',[])}
def values(index):
 a=g['accessors'][index];b=g['bufferViews'][a['bufferView']];size={'SCALAR':1,'VEC3':3,'VEC4':4}[a['type']];start=b.get('byteOffset',0)+a.get('byteOffset',0)
 code={5126:'f',5123:'H',5125:'I'}[a['componentType']];width=struct.calcsize(code)
 return [struct.unpack_from('<'+code*size,data,start+i*size*width) for i in range(a['count'])]
def rotate(v,q):
 x,y,z=v;a,b,c,w=q
 cross=(b*z-c*y,c*x-a*z,a*y-b*x)
 twice=(b*cross[2]-c*cross[1],c*cross[0]-a*cross[2],a*cross[1]-b*cross[0])
 return tuple(v[i]+2*w*cross[i]+2*twice[i] for i in range(3))
def world(i,v,angle):
 n=g['nodes'][i];q=n.get('rotation',[0,0,0,1])
 if n['name']=='ServiceCover':q=[math.sin(angle/2),0,0,math.cos(angle/2)]
 v=rotate(v,q);t=n.get('translation',[0,0,0]);v=tuple(v[k]+t[k] for k in range(3))
 return world(parents[i],v,angle) if i in parents else v
def bounds(verts):return [[min(v[k] for v in verts) for k in range(3)],[max(v[k] for v in verts) for k in range(3)]]
def gap(a,b):return max(max(a[0][i]-b[1][i],b[0][i]-a[1][i]) for i in range(3))
meshes={i:values(g['meshes'][n['mesh']]['primitives'][0]['attributes']['POSITION']) for i,n in enumerate(g['nodes']) if 'mesh' in n}
clip=next(c for c in g['animations'] if c['name']=='maintenance');assert len(clip['channels'])==1
channel=clip['channels'][0];assert g['nodes'][channel['target']['node']]['name']=='ServiceCover' and channel['target']['path']=='rotation'
sampler=clip['samplers'][channel['sampler']];times=values(sampler['input']);q=values(sampler['output'])[-1];end=math.degrees(2*math.atan2(q[0],q[3]));assert abs(end+110)<.0001 and times==[(0.,),(1.,)]
moving=[i for i in meshes if g['nodes'][i]['name'] in ['CoverPanel','CoverHandle']];assert len(moving)==2
fixed={i:bounds([world(i,v,0) for v in verts]) for i,verts in meshes.items() if i not in moving}
spec=importlib.util.spec_from_file_location('tuoyun_geometry',W/'art/source/units/tuoyun-r1/generate_tuoyun_r1.py');u=importlib.util.module_from_spec(spec);spec.loader.exec_module(u);assert not u._sat_selftest()[0]
tris={}
for i in meshes:
 primitive=g['meshes'][g['nodes'][i]['mesh']]['primitives'][0];ix=[x[0] for x in values(primitive['indices'])] if 'indices' in primitive else list(range(len(meshes[i])));assert primitive.get('mode',4)==4 and len(ix)%3==0
 tris[i]=[tuple(ix[k:k+3]) for k in range(0,len(ix),3)]
fixed_verts={i:[world(i,v,0) for v in meshes[i]] for i in fixed}
sat_pairs=set();checks={};low=[math.inf]*3;high=[-math.inf]*3
for step in range(111):
 angle=math.radians(end*step/110)
 for i in moving:
  bb=bounds([world(i,v,angle) for v in meshes[i]])
  low=[min(low[k],bb[0][k]) for k in range(3)];high=[max(high[k],bb[1][k]) for k in range(3)]
  for j,b in fixed.items():
   name=g['nodes'][i]['name']+' / '+g['nodes'][j]['name'];separation=gap(bb,b)
   if separation<.005:
    separation=u.sat_gap(([world(i,v,angle) for v in meshes[i]],tris[i]),(fixed_verts[j],tris[j]));sat_pairs.add(name)
   checks[name]=min(checks.get(name,math.inf),separation)
assert min(checks.values())>=.005-1e-7,sorted(checks.items(),key=lambda x:x[1])[:10]
manifest=json.loads((W/'art/manifests/solar-r1.json').read_text());assert all(low[k]>=manifest['bounds']['active']['min'][k]-1e-5 and high[k]<=manifest['bounds']['active']['max'][k]+1e-5 for k in range(3))
# Wing rotations are around Z and foot retraction is along X: their Z limits never enter the cover envelope.
wing_z=[(g['nodes'][i]['name'],bb[1][2]) for i,bb in fixed.items() if g['nodes'][i]['name'].startswith(('Left','Right'))]
assert wing_z and max(z for _,z in wing_z)<low[2]-.005
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();before={p.name:sha(p) for p in D.iterdir() if p.suffix in ('.gltf','.bin','.glb')}
r=subprocess.run([sys.executable,str(D/'generate_solar_r1.py')],capture_output=True,text=True);assert r.returncode==0,r.stdout+r.stderr
assert all(sha(D/n)==h for n,h in before.items());assert sha(D/'solar-r1.glb')==sha(W/'prototype/assets/facilities/solar-r1/solar-r1.glb')==manifest['glb_sha256']
report={'samples':111,'source_end_x_deg':end,'moving_parts':['CoverPanel','CoverHandle'],'neighbor_meshes':len(fixed),'pair_count':len(checks),'min_separation_m':min(checks.values()),'moving_bounds':{'min':low,'max':high},'sat_pairs_when_aabb_inconclusive':sorted(sat_pairs),'lowest_pairs':sorted(checks.items(),key=lambda x:x[1])[:12],'fold_wings_separated_by_z_all_phases':True,'source_regeneration_byte_match':True,'method':'finite integer-angle source vertices and separating AABB axes, complete convex SAT when AABB is inconclusive; not full continuous engineering validation','engineering_reliability':'NOT_RUN'}
(Path(__file__).parent/'cover-clearance.json').write_text(json.dumps(report,indent=2)+'\n');print('PASS',report)
