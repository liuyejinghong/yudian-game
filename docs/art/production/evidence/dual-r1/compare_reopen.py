"""Compare Blender-reopened GLB by actual geometry, allowing float32 rounding."""
from pathlib import Path
import json,struct,sys
def read(p):
    d=p.read_bytes();n=struct.unpack_from('<I',d,12)[0];j=json.loads(d[20:20+n]);k=20+n;bn=struct.unpack_from('<I',d,k)[0];return j,d[k+8:k+8+bn]
def check(source,reopened):
    a,ab=read(source);b,bb=read(reopened);assert a==b,'structure changed'
    delta=0
    for accessor in a['accessors']:
        v=a['bufferViews'][accessor['bufferView']];off=v.get('byteOffset',0)+accessor.get('byteOffset',0)
        n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[accessor['type']]*accessor['count'];fmt={5126:'f',5125:'I',5123:'H'}[accessor['componentType']]
        av=struct.unpack_from('<'+fmt*n,ab,off);bv=struct.unpack_from('<'+fmt*n,bb,off)
        if fmt=='f':delta=max(delta,max(abs(x-y) for x,y in zip(av,bv)))
        else:assert av==bv,'topology changed'
    assert delta<1e-6,delta
    return {'blend_reopened':True,'glb_structure_identical':True,'topology_identical':True,'max_float_attribute_delta':delta,'tolerance':1e-6,'glb_byte_identical':source.read_bytes()==reopened.read_bytes()}
if __name__=='__main__':
    assert len(sys.argv)==4,'source GLB / reopened GLB / output JSON required'
    result=check(Path(sys.argv[1]),Path(sys.argv[2]));Path(sys.argv[3]).write_text(json.dumps(result,indent=2)+'\n');print('PASS normalized reopen',result)
