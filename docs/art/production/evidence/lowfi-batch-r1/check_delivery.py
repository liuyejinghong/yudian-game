"""Fail if delivered sources, manifests or native evidence no longer agree."""
import hashlib,json,struct
from prepare_preview import ROOT,HERE,NEW

def read(path):return json.loads(path.read_text())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def log(path,marker):
    text=path.read_text();assert marker in text and not any(x in text for x in ['ERROR','TIMEOUT']),path

def run():
    checks={r['asset']:r for r in read(HERE/'source-checks.json')}
    reopened={r['asset']:r for r in read(HERE/'source-reopen.json')}
    cases=illegal=faults=clips=0
    for key in NEW:
        audit=read(HERE/(key+'-audit.json'));manifest=read(ROOT/'art/manifests/lowfi-batch-r1'/(key+'.json'))
        glb=ROOT/'prototype/assets/lowfi-batch-r1/models'/(key+'.glb')
        assert sha(glb)==audit['glb_sha256']==manifest['glb_sha256']==reopened[key]['glb_sha256']
        assert reopened[key]['max_float_attribute_delta']<1e-6 and reopened[key]['topology_identical']
        assert checks[key]['root_tracks']==0
        for file in manifest['source']['files']:assert sha(ROOT/file['path'])==file['sha256'],file
        assert sha(ROOT/manifest['source']['source_path'])==reopened[key]['blend_sha256']
        assert sha(ROOT/manifest['godot_import']['path'])==manifest['godot_import']['sha256']
        assert manifest['godot_import']['optimizer_enabled'] is False
        assert set(audit['clips'])=={r['clip'] for r in checks[key]['clips']}
        assert audit['frozen_socket_check']==audit['candidate_envelope_check']=='PASS'
        log(HERE/(key+'-state-clean.log'),'LF_STATE_OK '+key)
        cases+=len(audit['pose_cases']);illegal+=audit['illegal_preserved'];faults+=audit['source_faults_preserved'];clips+=len(audit['clips'])
    assert (cases,illegal,faults,clips)==(637,110,12,21),(cases,illegal,faults,clips)
    import_log=(HERE/'import-clean.log').read_text();assert 'ERROR' not in import_log and 'TIMEOUT' not in import_log
    captures=[]
    for path in sorted((HERE/'captures').glob('*/captures.json')):
        log(HERE/('capture-'+path.parent.name+'.log'),'LF_CAPTURE_OK')
        for record in read(path):
            png=path.parent/record['file'];assert sha(png)==record['png_sha256']
            data=png.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack_from('>II',data,16)==(1920,1200)
            assert sha(ROOT/'prototype'/record['source'].removeprefix('res://'))==record['source_sha256']
            captures.append(record)
    expected=sum(len(r['views']) for r in read(HERE/'capture-plan.json'))
    assert expected==284 and len(captures)==expected and len({r['file'] for r in captures})==expected
    views={}
    for r in captures:
        if r['camera'] not in views:views[r['camera']]=r['actual_view']
        assert r['actual_view']==views[r['camera']],r['file']
    log(HERE/'relation-final.log','LF_RELATION_OK')
    relation=read(HERE/'relation/relation.json');assert len(relation['stages'])==3
    for stage in relation['stages']:
        assert sha(HERE/'relation'/stage['file'])==stage['png_sha256']
        assert stage['actual_view']==views['normal']
    log(HERE/'clearance-r2.log','LF_CLEARANCE_OK')
    clearance=read(HERE/'clearance.json')['records'];assert len(clearance)==108 and all(r['separating_axis_gap_m']>0 for r in clearance)
    print('DELIVERY_OK: 11 sources, 21 clips, 637 poses, 110 invalid, 12 missing-source, 108 selected clearances, 284+3 native PNGs')
if __name__=='__main__':run()
