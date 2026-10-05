"""Check actual F04 import, U01 clearance evidence and real capture identities."""
import hashlib,json,struct
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def run():
    audit=json.loads((HERE/'f04-audit.json').read_text())
    facts=json.loads((ROOT/'art/source/facilities/charger-r1/geometry-facts.json').read_text())
    station=digest(ROOT/'art/source/facilities/charger-r1/charger-r1.glb')
    unit=digest(ROOT/'prototype/assets/units/tuoyun-r1/tuoyun-r1.glb')
    assert station==facts['sha256']['glb']==digest(ROOT/'prototype/assets/facilities/charger-r1/charger-r1.glb')
    assert unit==json.loads((ROOT/'art/manifests/tuoyun-r1.json').read_text())['glb_sha256']
    assert audit['triangles']==facts['triangles_total']==144 and len(audit['meshes'])==9
    surfaces=[s for m in audit['meshes'] for s in m['surfaces']]
    assert len(surfaces)==9 and {s['material'] for s in surfaces}=={'body_light','frame_dark','accent_warm'}
    near=lambda a,b:len(a)==len(b) and all(abs(x-y)<=1e-6 for x,y in zip(a,b))
    expected={'Socket_Dock':([.65,.57,-.35],[-1,0,0]),'Socket_PowerIn':([1.055,.18,-.66],[0,0,-1])}
    assert len(audit['sockets'])==2
    for socket in audit['sockets']:
        position,forward=expected[socket['path'].rsplit('/',1)[-1]]
        assert near(socket['position'],position) and near(socket['forward'],forward) and near(socket['up'],[0,1,0])
    box=audit['bounds_including_hidden']
    assert near(box['min'],[-.9,0,-1.1]) and near(box['max'],[1.3,1.45,1.1])
    clearance=json.loads((HERE/'f04-clearance.json').read_text())
    assert clearance['station_sha256']==station and clearance['u01_sha256']==unit
    assert clearance['charge_samples']==len(clearance['rows'])==61 and clearance['nonground_mesh_pairs_per_sample']==133
    assert clearance['minimum_separating_axis_gap_m']>1e-6 and clearance['rejected_virtual_head_clash_samples']>0
    rows=json.loads((HERE/'images-f04/captures.json').read_text())
    assert {(r['camera'],r['yaw'],r['pose']) for r in rows}=={(c,y,p) for c in ['normal','close'] for y in [0,90] for p in ['empty','idle_demo','charge_demo']}
    assert len(rows)==12
    hashes={}
    for row in rows:
        assert row['source_sha256']==station and row['demo_sha256']==unit and row['unit_offset_y']==.02
        assert row['actual_view']['render']['rendering_driver_actual']=='metal'
        data=(HERE/'images-f04'/row['file']).read_bytes()
        assert data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',data[16:24])==(1920,1200)
        hashes[row['file']]=hashlib.sha256(data).hexdigest()
    result={'station_sha256':station,'u01_sha256':unit,'triangles':144,'meshes':9,'surfaces':9,'sockets_checked':2,'images':12,'charge_samples':61,'minimum_separating_axis_gap_m':clearance['minimum_separating_axis_gap_m'],'image_sha256':hashes,'scope':'static waiting pose only; physical insertion/gameplay/state/owner acceptance NOT_RUN'}
    (HERE/'f04-verification.json').write_text(json.dumps(result,indent=2)+'\n')
    print('PASS F04 actual import, sockets, 61 charge poses and 12 PNG identities')
if __name__=='__main__':run()
