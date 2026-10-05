"""Check actual imported HF shape, inherited sockets and capture identity."""
import hashlib,json
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
def run():
    audit=json.loads((HERE/'hf-audit.json').read_text())
    facts=json.loads((ROOT/'art/source/units/tuoyun-hf-r1/geometry-facts.json').read_text())
    manifest=json.loads((ROOT/'art/manifests/tuoyun-r1.json').read_text())
    near=lambda a,b:all(abs(x-y)<=1e-6 for x,y in zip(a,b))
    by_name={m['path'].rsplit('/',1)[-1]:m for m in audit['meshes']}
    assert audit['triangles']==facts['triangles']==19504 and len(by_name)==46
    for old in manifest['sockets']:
        found=[s for s in audit['sockets'] if s['path'].endswith('/'+old['node'].rsplit('/',1)[-1])]
        assert len(found)==1
        for key in ['position','forward','up']:assert near(found[0][key],old[key])
    box=audit['bounds_including_hidden']
    assert box['min'][1]>=-1e-6 and box['max'][0]-box['min'][0]<=1.30
    cargo=by_name['CargoBox']['actual_vertices_bounds']
    for side in [-1,1]:
        shell=by_name['Deck_side_shell_'+str(side)]['actual_vertices_bounds']
        gap=shell['min'][0]-cargo['max'][0] if side==1 else cargo['min'][0]-shell['max'][0]
        assert gap>=.13 and shell['min'][1]>=.479
    for label in ['LF','LM','LR','RF','RM','RR']:
        cover=by_name['Hub_cover_'+label]['actual_vertices_bounds']
        assert max(abs(cover['min'][0]),abs(cover['max'][0]))<=.65
    roles={s['material'] for m in audit['meshes'] for s in m['surfaces']}
    assert roles<= {'body_light','frame_dark','accent_warm','rubber'}
    path=ROOT/'art/source/units/tuoyun-hf-r1/tuoyun-hf-r1.glb'
    digest=hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest==facts['glb_sha256']==hashlib.sha256((ROOT/'prototype/assets/units/tuoyun-hf-r1/tuoyun-hf-r1.glb').read_bytes()).hexdigest()
    rows=json.loads((HERE/'images-hf/captures.json').read_text());assert len(rows)==16
    hashes={}
    for row in rows:
        if row['sample']=='u01-hf':assert row['source_sha256']==digest
        hashes[row['file']]=hashlib.sha256((HERE/'images-hf'/row['file']).read_bytes()).hexdigest()
    result={'revision':3,'glb_sha256':digest,'triangles':19504,'meshes':46,'sockets_checked':4,'images':16,'image_sha256':hashes,'bounds':box,'scope':'idle static shape only, no full movement/UV/baking/owner acceptance'}
    (HERE/'hf-verification.json').write_text(json.dumps(result,indent=2)+'\n')
    print('PASS HF inherited sockets, static envelope/cargo gaps and 16 capture identities')
if __name__=='__main__':run()
