import json,struct,sys,math,collections
from pathlib import Path
path=Path(sys.argv[1]);data=path.read_bytes()
magic,version,total=struct.unpack_from("<III",data)
assert (magic,version,total)==(0x46546c67,2,len(data)), "bad GLB header"
at=12;doc=None;binary=None
while at<len(data):
 length,kind=struct.unpack_from("<II",data,at);at+=8
 chunk=data[at:at+length];at+=length
 if kind==0x4e4f534a:doc=json.loads(chunk)
 if kind==0x004e4942:binary=chunk
assert doc and binary is not None
formats={5120:"b",5121:"B",5122:"h",5123:"H",5125:"I",5126:"f"}
widths={"SCALAR":1,"VEC2":2,"VEC3":3,"VEC4":4,"MAT4":16}
def accessor(index):
 a=doc["accessors"][index];v=doc["bufferViews"][a["bufferView"]]
 assert v.get("buffer",0)==0 and not a.get("sparse"), "unsupported accessor in original sample"
 fmt="<"+formats[a["componentType"]]*widths[a["type"]];size=struct.calcsize(fmt)
 base=v.get("byteOffset",0)+a.get("byteOffset",0);stride=v.get("byteStride",size)
 return [struct.unpack_from(fmt,binary,base+i*stride) for i in range(a["count"])]
rows=[];bad=[];degenerate=[]
for mi,m in enumerate(doc.get("meshes",[])):
 for pi,prim in enumerate(m["primitives"]):
  assert prim.get("mode",4)==4, "only sample triangle lists supported"
  ps=accessor(prim["attributes"]["POSITION"]);ns=accessor(prim["attributes"]["NORMAL"])
  ids=[t[0] for t in accessor(prim["indices"])] if "indices" in prim else list(range(len(ps)))
  assert len(ids)%3==0
  for offset in range(0,len(ids),3):
   a,b,c=[ps[i] for i in ids[offset:offset+3]]
   u=[b[k]-a[k] for k in range(3)];v=[c[k]-a[k] for k in range(3)]
   cross=(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
   size=sum(q*q for q in cross)
   dot=sum(cross[k]*sum(ns[i][k] for i in ids[offset:offset+3])/3 for k in range(3))
   if size<1e-16:degenerate.append((mi,pi,offset//3))
   elif dot<=0:bad.append((mi,pi,offset//3,dot))
  rows.append({"mesh":mi,"name":m.get("name"),"surface":pi,"triangles":len(ids)//3})
result={"file":str(path),"mesh_surfaces":rows,"inward_or_mismatched_triangles":bad,"degenerate_triangles":degenerate,"winding":"glTF CCW against exported normals"}
print(json.dumps(result,indent=2))
if bad or degenerate:sys.exit(1)
print("GLB_FACE_AUDIT_OK")
