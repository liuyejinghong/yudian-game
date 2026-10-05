"""Open delivered Blender files and re-export with their existing builders."""
import hashlib,importlib.util,json,sys,tempfile
from pathlib import Path
import bpy
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
sys.path.insert(0,str(HERE.parent/'dual-r1'))
from compare_reopen import check
rows=[]
for key in ['wangshan','repair','lander','zhulei','storage','charger','crate','recovery','terrain-original','terrain-flat','terrain-dug']:
    group='new' if key in ['wangshan','repair','lander'] else 'existing'
    source=ROOT/'art/source/lowfi-batch-r1'/group
    builder=source/('build_samples.py' if group=='new' else 'build_'+('terrain' if key.startswith('terrain-') else key)+'.py')
    spec=importlib.util.spec_from_file_location('delivered_builder',builder)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    blend=source/key/(key+'.blend');glb=source/key/(key+'.glb')
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    with tempfile.TemporaryDirectory(prefix='lf-reopen-') as tmp:
        out=Path(tmp)/'reopened.glb';module.export_glb(out)
        result=check(glb,out)
    result.update(asset=key,blend_sha256=hashlib.sha256(blend.read_bytes()).hexdigest(),glb_sha256=hashlib.sha256(glb.read_bytes()).hexdigest(),blender=bpy.app.version_string)
    rows.append(result);print('LF_REOPEN_OK',key,flush=True)
(HERE/'source-reopen.json').write_text(json.dumps(rows,indent=2)+'\n')
