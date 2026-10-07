import bpy,json,hashlib
from pathlib import Path
HERE=Path(__file__).resolve().parents[1]
path=HERE/'mars-ground-patch-hifi-r2.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
for im in bpy.data.images:
    if im.name.startswith(('ground-sand-','ground-clast-')) and 'test' not in im.name:
        im.filepath='//textures/'+Path(im.filepath).name
bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True,relative_remap=False)
p=HERE/'manifest.json';r=json.loads(p.read_text());r['files'][path.name]={'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size}
p.write_text(json.dumps(r,indent=2))
