"""Check delivered files against actual Godot audits and archived capture evidence."""
import hashlib,json,struct
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
def read(path):return json.loads(path.read_text())
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def near(a,b,tol=1e-6):return len(a)==len(b) and all(abs(x-y)<=tol for x,y in zip(a,b))
def run():
    source=ROOT/'art/source/units/zhulei-r1';runtime=ROOT/'prototype/assets/units/zhulei-r1'
    facts=read(source/'geometry-facts.json');hashes={}
    for name,expected in facts['files'].items():
        path=source/name;hashes[name]=digest(path)
        assert hashes[name]==expected['sha256'] and path.stat().st_size==expected['bytes']
        if name.endswith('.glb'):assert path.read_bytes()==(runtime/name).read_bytes()
    ref=digest(ROOT/'art/source/units/tuoyun-r1/tuoyun-r1.glb')
    assert ref==facts['source']['sha256']==digest(ROOT/'prototype/assets/units/tuoyun-r1/tuoyun-r1.glb')
    aliases={'u02-idle':'zhulei-r1.glb','u02-work-demo':'zhulei-work.glb','u02-maintenance-demo':'zhulei-maintenance.glb'}
    for pose,alias in [('idle','u02-idle'),('work','u02-work-demo'),('maintenance','u02-maintenance-demo')]:
        audit=read(HERE/('audit-'+alias.removeprefix('u02-')+'.json'))
        assert audit['triangles']==facts['triangles_total']==8416 and len(audit['meshes'])==50
        meshes={m['path'].rsplit('/',1)[-1]:m for m in audit['meshes']}
        assert len(meshes)==50 and sum(n.startswith('Wheel_') for n in meshes)==6
        assert all(n in meshes for n in ['Rocker_L','Rocker_R','Bogie_L','Bogie_R'])
        surfaces=[s for m in meshes.values() for s in m['surfaces']]
        assert len(surfaces)==66 and {s['material'] for s in surfaces}==set(facts['roles_used'])
        expected=facts['poses'][pose];bounds=audit['bounds_including_hidden']
        assert near(bounds['min'],expected['envelope']['min'],1e-5) and near(bounds['max'],expected['envelope']['max'],1e-5)
        assert bounds['min'][1]>=-1e-6 and abs(bounds['min'][1])<1e-4
        assert len(audit['sockets'])==5
        for socket in audit['sockets']:
            name=socket['path'].rsplit('/',1)[-1]
            for key in ['position','forward','up']:assert near(socket[key],expected['sockets'][name][key])
        hinge=meshes['HoodHingeMount']['actual_vertices_bounds'];wall=meshes['RearWallBack']['actual_vertices_bounds']
        assert abs(hinge['min'][1]-wall['max'][1])<=1e-6
        for name in ['HoodHingeMount','HoodLidPanel']:
            a=meshes['HoodHingeAxle']['actual_vertices_bounds'];b=meshes[name]['actual_vertices_bounds']
            assert all(min(a['max'][i],b['max'][i])>=max(a['min'][i],b['min'][i])-1e-6 for i in range(3))
        assert not any('Cargo' in m['path'] for m in meshes.values())
        box=lambda name:meshes[name]['actual_vertices_bounds']
        for side in ['L','R']:
            rod=box('SupportSlide'+side+'_Rod');pad=box('SupportSlide'+side+'_Pad')
            assert abs(rod['min'][1]-pad['max'][1])<=1e-6
            for axis,neg,pos in [(0,'XNeg','XPos'),(2,'ZNeg','ZPos')]:
                inner_min=box('SupportSleeve'+side+'_Wall'+neg)['max'][axis]
                inner_max=box('SupportSleeve'+side+'_Wall'+pos)['min'][axis]
                assert rod['min'][axis]-inner_min>.0054 and inner_max-rod['max'][axis]>.0054
            assert rod['max'][1]<box('SupportSleeve'+side+'_Cap')['min'][1]
    pose=read(HERE/'pose.json')
    assert pose['source_sha256']==hashes['zhulei-r1.glb'] and pose['samples']==len(pose['rows'])==132
    assert pose['minimum_separating_axis_gap_m']>1e-6 and 'U02_POSE_OK samples=132' in (HERE/'pose-sat.log').read_text()
    assert 'U02_EXISTING_EMPTY_OUTPUT_REJECTED' in (HERE/'source-check.log').read_text()
    rows=read(HERE/'images/captures.json');image_hashes={}
    assert len(rows)==24 and {(r['sample'],r['camera'],r['yaw']) for r in rows}=={(s,c,y) for s in [*aliases,'u01-ref'] for c in ['normal','close'] for y in [0,90,180]}
    for row in rows:
        assert row['source_sha256']==(ref if row['sample']=='u01-ref' else hashes[aliases[row['sample']]])
        assert row['actual_view']['render']['rendering_driver_actual']=='metal' and not row['loaded_demo']
        assert all(b['role'] in {'body_light','frame_dark','accent_warm','rubber','solar_dark','light_signal'} for b in row['bindings'])
        data=(HERE/'images'/row['file']).read_bytes()
        assert data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',data[16:24])==(1920,1200)
        image_hashes[row['file']]=hashlib.sha256(data).hexdigest()
    result={'source_sha256':hashes,'triangles':8416,'meshes':50,'surfaces':66,'sockets':5,'images':24,'pose_samples':132,'minimum_separating_axis_gap_m':pose['minimum_separating_axis_gap_m'],'image_sha256':image_hashes,'scope':'static GEO candidate and finite pose samples; seven-state animation/gameplay/owner acceptance NOT_RUN'}
    (HERE/'verification.json').write_text(json.dumps(result,indent=2)+'\n')
    print('PASS U02 files, actual import/sockets/support fit, 132 pose records and 24 PNG identities')
if __name__=='__main__':run()
