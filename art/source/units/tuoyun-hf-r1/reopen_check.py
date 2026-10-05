"""Run inside saved blend; checks the evaluated mesh and exports a comparison."""
import importlib.util,json,sys
from pathlib import Path
import bpy
p=Path(__file__).with_name('build_sample.py')
s=importlib.util.spec_from_file_location('sample_build',p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
args=sys.argv[sys.argv.index('--')+1:]
assert len(args)==1,'one comparison output directory required'
out=Path(args[0]);out.mkdir(parents=True,exist_ok=True)
rows=m.geometry_report();m.export_glb(out/'reopened.glb')
(out/'reopened.json').write_text(json.dumps({'blender':bpy.app.version_string,'meshes':rows,'triangles':sum(r['triangles'] for r in rows)},indent=2)+'\n')
print('HF_REOPEN_OK',len(rows),'meshes')
