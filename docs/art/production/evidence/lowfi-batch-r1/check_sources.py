"""Check actual GLB clips and source/runtime identity before preview acceptance."""
import json,math,struct,sys
from pathlib import Path
from prepare_preview import HERE,ROOT,NEW
sys.path.insert(0,str(HERE.parent/'dual-r1'))
from compare_reopen import read
EXPECTED={
 'wangshan':dict(move=1,work=2,charge=1,disabled=1,maintenance=1),
 'zhulei':dict(move=1,work=2,charge=1,disabled=1,maintenance=1),
 'repair':dict(work=2,disabled=1,maintenance=1),
 'lander':dict(work=1,disabled=1,maintenance=1),
 'storage':dict(disabled=1,maintenance=1),
 'charger':dict(work=2,disabled=1,maintenance=1),
}
def values(j,b,index):
 a=j['accessors'][index];v=j['bufferViews'][a['bufferView']]
 n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
 assert a['componentType']==5126 and not a.get('sparse')
 stride=v.get('byteStride',4*n);start=v.get('byteOffset',0)+a.get('byteOffset',0)
 out=[struct.unpack_from('<'+'f'*n,b,start+i*stride) for i in range(a['count'])]
 assert out and all(math.isfinite(x) for row in out for x in row)
 return out

def terrain_check(key,j,b):
 node=next(n for n in j['nodes'] if n['name']=='TerrainSurface')
 assert all(not node.get(k) for k in ['translation','rotation','matrix']),key
 mesh=j['meshes'][node['mesh']];assert len(mesh['primitives'])==1
 primitive=mesh['primitives'][0];points=values(j,b,primitive['attributes']['POSITION'])
 unique=set((x,z) for x,y,z in points);assert len(unique)==49*49
 for x,y,z in points:
  boundary=max(0,1-(max(abs(x),abs(z))/4.5)**6)
  base=.35*boundary
  hill=.85*math.exp(-((x+2)**2/1.1**2+(z-1)**2/1.25**2))*boundary
  ore=.50*math.exp(-((x-2)**2+(z+1)**2/1.1**2))*boundary
  height=base+hill+ore if key=='terrain-original' else base+ore if key=='terrain-flat' else base-.30/.50*ore
  assert abs(y-height)<1e-6,(key,x,y,z,height)
 a=j['accessors'][primitive['indices']];view=j['bufferViews'][a['bufferView']]
 fmt={5123:'H',5125:'I'}[a['componentType']];size=struct.calcsize(fmt)
 offset=view.get('byteOffset',0)+a.get('byteOffset',0);stride=view.get('byteStride',size)
 indices=[struct.unpack_from('<'+fmt,b,offset+i*stride)[0] for i in range(a['count'])]
 assert len(indices)==48*48*6
 topology=[tuple((points[v][0],points[v][2]) for v in indices[i:i+3]) for i in range(0,len(indices),3)]
 return topology

def run():
 rows=[];terrain_topology=None
 for key in NEW:
  path=ROOT/'prototype/assets/lowfi-batch-r1/models'/(key+'.glb')
  j,b=read(path);assert path.stat().st_size>100
  roots=set(j['scenes'][j.get('scene',0)]['nodes'])
  if key.startswith('terrain-'):
   topology=terrain_check(key,j,b)
   if terrain_topology is not None:assert topology==terrain_topology,'terrain topology differs'
   terrain_topology=topology
  expected=EXPECTED.get(key,{})
  clips=j.get('animations',[]);assert sorted(c['name'] for c in clips)==sorted(expected),key
  assert all(m['name'] in ['body_light','frame_dark','rubber','accent_warm','solar_face','soil_mars'] for m in j.get('materials',[])),key
  checked=[]
  for clip in clips:
   name=clip['name'];changed=[];targets=[];duration=0
   assert clip['channels'],(key,name,'empty clip')
   for channel in clip['channels']:
    target=channel['target'];assert target['node'] not in roots,(key,name,'author root targeted')
    sampler=clip['samplers'][channel['sampler']]
    times=[r[0] for r in values(j,b,sampler['input'])];data=values(j,b,sampler['output'])
    assert sampler.get('interpolation','LINEAR')=='LINEAR',(key,name,'nonlinear source track')
    assert abs(min(times))<.0001 and max(times)<=expected[name]+.0001,(key,name,times)
    duration=max(duration,max(times))
    assert target['path'] in ['translation','rotation','scale']
    moves=any(min(sum((x-y)**2 for x,y in zip(row,data[0])),sum((x+y)**2 for x,y in zip(row,data[0])))>1e-10 for row in data[1:]) if target['path']=='rotation' else any(any(abs(x-y)>1e-5 for x,y in zip(row,data[0])) for row in data[1:])
    assert not (target['path']=='scale' and moves),(key,name,'animated geometry scale')
    node=j['nodes'][target['node']]['name'];targets.append([node,target['path']])
    if moves:changed.append([node,target['path']])
   assert abs(duration-expected[name])<.0001,(key,name,duration)
   if name in ['move','work']:assert changed,(key,name,'no actual source movement')
   checked.append(dict(clip=name,duration_s=expected[name],changed_targets=changed,all_targets=targets))
  group='new' if key in ['wangshan','repair','lander'] else 'existing'
  source=ROOT/'art/source/lowfi-batch-r1'/group/key
  assert (source/(key+'.glb')).read_bytes()==path.read_bytes(),key
  assert len(list(source.glob('*.blend')))==1,key
  rows.append(dict(asset=key,author_roots=[j['nodes'][i]['name'] for i in roots],root_tracks=0,clips=checked,source_runtime_identical=True,terrain_formula_topology_check="PASS" if key.startswith("terrain-") else "na"))
 (HERE/'source-checks.json').write_text(json.dumps(rows,indent=2)+'\n')
 print('LF_SOURCES_OK',len(rows),'GLBs',sum(len(r['clips']) for r in rows),'real clips')
if __name__=='__main__':run()
