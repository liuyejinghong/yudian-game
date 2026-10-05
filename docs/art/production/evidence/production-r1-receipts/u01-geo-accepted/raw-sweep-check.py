import struct,json,math,itertools,sys
from pathlib import Path
p=Path(sys.argv[1]);b=p.read_bytes();jlen=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+jlen]);off=20+jlen;blen=struct.unpack_from('<I',b,off)[0];buf=b[off+8:off+8+blen]
def acc(i):
 a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];fmt='<'+({5126:'f',5125:'I',5123:'H'}[a['componentType']])*({'VEC3':3,'SCALAR':1}[a['type']]);size=struct.calcsize(fmt);base=v.get('byteOffset',0)+a.get('byteOffset',0);return [struct.unpack_from(fmt,buf,base+k*v.get('byteStride',size)) for k in range(a['count'])]
def sub(a,b):return tuple(x-y for x,y in zip(a,b))
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def unit(a):
 n=math.sqrt(dot(a,a));return tuple(x/n for x in a) if n>1e-9 else None
def axes(parts):
 pts,idx=parts;norm=[];edges=[]
 for i in range(0,len(idx),3):
  a,b,c=[pts[k] for k in idx[i:i+3]];norm.append(cross(sub(b,a),sub(c,a)));edges.extend([sub(b,a),sub(c,b),sub(a,c)])
 return norm,edges
def gap(A,B):
 an,ae=axes(A);bn,be=axes(B);dirs=an+bn+[cross(a,b) for a in ae for b in be];seen=set();best=-float('inf')
 for v in dirs:
  u=unit(v)
  if u is None:continue
  if next(x for x in u if abs(x)>1e-8)<0:u=tuple(-x for x in u)
  key=tuple(round(x,7) for x in u)
  if key in seen:continue
  seen.add(key);a=[dot(u,p) for p in A[0]];b=[dot(u,p) for p in B[0]];best=max(best,min(a)-max(b),min(b)-max(a))
 return best
parents={c:i for i,n in enumerate(d['nodes']) for c in n.get('children',[])}
def translation(i):
 t=d['nodes'][i].get('translation',[0,0,0]);return tuple(t) if i not in parents else tuple(x+y for x,y in zip(t,translation(parents[i])))
parts={}
for i,n in enumerate(d['nodes']):
 if 'mesh' not in n:continue
 m=d['meshes'][n['mesh']];t=translation(i)
 for k,pr in enumerate(m['primitives']):parts[n['name'],k]=([tuple(x+y for x,y in zip(v,t)) for v in acc(pr['attributes']['POSITION'])],[x[0] for x in acc(pr['indices'])])
lid=parts['HoodLid',0]
# Exact actual mesh box provides separated/contact/overlap controls.
box=parts['Chassis',0];width=max(p[0] for p in box[0])-min(p[0] for p in box[0])
ctrl={}
for name,shift in [('separate',width+.1),('contact',width),('overlap',width-.1)]:
 moved=([(p[0]+shift,p[1],p[2]) for p in box[0]],box[1]);ctrl[name]=gap(box,moved)
assert ctrl['separate']>.09 and abs(ctrl['contact'])<1e-6 and ctrl['overlap']<-.009,ctrl
obstacles={str(k):v for k,v in parts.items() if k[0]=='Head' or k in [('Chassis',0),('Body',0)]}
rows=[];bad=[]
for deg in range(-35,71):
 a=math.radians(deg);c,s=math.cos(a),math.sin(a);pts=[(x,.64+(y-.64)*c-(z+.25)*s,-.25+(y-.64)*s+(z+.25)*c) for x,y,z in lid[0]];rv=(pts,lid[1]);gaps={k:gap(rv,v) for k,v in obstacles.items()};row={'degrees':deg,'bounds':{'min':[min(p[i] for p in pts) for i in range(3)],'max':[max(p[i] for p in pts) for i in range(3)]},'gaps':gaps};rows.append(row)
 for k,g in gaps.items():
  if g < -1e-6:bad.append({'degrees':deg,'part':k,'gap':g})
result={'file':str(p),'method':'raw GLB positions/indices; signed SAT face normals and all triangle-edge cross products','controls':ctrl,'sample_count':len(rows),'continuous_sweep':'NOT_RUN; finite integer-degree samples only','failures':bad,'rows':rows};Path(sys.argv[2]).write_text(json.dumps(result,indent=2));print('SIGNED_CONTROLS',ctrl,'SAMPLES',len(rows),'FAILURES',len(bad),bad[:8]);sys.exit(bool(bad))
