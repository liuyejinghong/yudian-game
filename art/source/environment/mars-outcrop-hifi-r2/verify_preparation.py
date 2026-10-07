"""Reopen independently rebuilt bake inputs and compare them with the delivery."""
import bpy,json,sys
from pathlib import Path

HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B
import pipeline as P
import input_identity as I

def differences(a,b,prefix=''):
    if isinstance(a,dict) and isinstance(b,dict):
        return [d for k in sorted(a.keys()|b.keys()) for d in differences(a.get(k),b.get(k),prefix+'.'+k)]
    return [] if a==b else [{'field':prefix.lstrip('.'),'prepared':a,'delivery':b}]

def read_inputs(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    return I.inputs(bpy.data.objects['HIGH_ParentSandstone'],bpy.data.objects['MarsOutcrop'])

def run():
    assert '--prepared-source' in sys.argv,'Pass --prepared-source for the independent rebuild'
    source=Path(sys.argv[sys.argv.index('--prepared-source')+1]).resolve()
    delivery=HERE/'mars-outcrop-hifi-r2.blend'
    assert source!=delivery
    before=P.identity(delivery)
    prepared=read_inputs(source);reference=read_inputs(delivery)
    delta=differences(prepared,reference)
    measurements=json.loads((source.parent/'prepared-inputs.json').read_text())
    report={'status':'PASS' if not delta else 'FAIL','method':'Native reopens; exact indexed mesh and reachable HIGH material-input identities, including world transforms and active/render UV layers',
        'scope':'Public build through complete-face bake preparation; the full public 4K bake/export was not rerun here',
        'prepared_source':{'path':str(source.relative_to(B.ROOT)),**P.identity(source)},
        'delivery_source':{'path':str(delivery.relative_to(B.ROOT)),**before},
        'prepared_inputs':prepared,'delivery_inputs':reference,'differences':delta,
        'pipeline_measurements':{k:measurements[k] for k in ('baseline','high_triangles','game_triangles','uv','uv_validation','projection','complete_surface')},
        'recipe_sources':{n:P.identity(HERE/n) for n in ('build.py','sculpt_stage09.py','finalize.py','pipeline.py','surface_stage17.py','complete_surface.py')},
        'comparison_script':P.identity(Path(__file__)),'fingerprint_script':P.identity(HERE/'input_identity.py')}
    assert before==P.identity(delivery),'Read-only comparison changed the delivery source'
    P.write_json(HERE/'verification/rebuild-preparation.json',report)
    print('PREPARATION_COMPARISON='+json.dumps({'status':report['status'],'differences':delta}),flush=True)
    assert not delta,'Rebuilt bake inputs differ from the frozen delivery; inspect the recorded fields'

if __name__=='__main__':run()
