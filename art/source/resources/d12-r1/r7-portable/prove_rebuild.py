"""Read only an isolated rebuild; compare full frozen GLB visual payloads."""
from pathlib import Path
import hashlib,json,sys
import bpy
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).parent))
from prove_portable import ROOT,AUTHOR,IDS,external_files,visual_payload


def main():
    isolated=Path(sys.argv[sys.argv.index('--')+1])
    assert isolated!=ROOT and str(isolated).startswith('/private/tmp/')
    source=isolated/'art/source/resources/d12-r1/r7-portable/resources-grey-r7.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    closure=external_files();resources={}
    for name in IDS:
        built=isolated/'art/exports/resources/d12-r1/r7'/(name+'.glb')
        frozen=ROOT/'art/exports/resources/d12-r1/r7'/(name+'.glb')
        assert visual_payload(built)==visual_payload(frozen),name
        resources[name]={'rebuilt_sha256':hashlib.sha256(built.read_bytes()).hexdigest(),
                'frozen_sha256':hashlib.sha256(frozen.read_bytes()).hexdigest(),
                'full_visual_attributes_indices_material_node_transform_extras_exact':True}
    report={'result':'PASS','blender':bpy.app.version_string,'isolated_root':str(isolated),
            'rebuilt_source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
            'closure':closure,'resources':resources,'current_assets_not_overwritten':True}
    (AUTHOR/'isolated-rebuild-proof.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PORTABLE_ISOLATED_REBUILD_OK '+json.dumps(report))


if __name__=='__main__':main()
