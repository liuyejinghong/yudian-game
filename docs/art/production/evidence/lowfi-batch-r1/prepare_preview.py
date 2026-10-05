"""Copy actual runtime inputs into an isolated, reproducible art-only Godot project."""
import shutil,sys
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
PROJECT=ROOT/'prototype/.godot/art-lowfi-batch-r1'
NEW=['wangshan','repair','lander','zhulei','storage','charger','crate','recovery','terrain-original','terrain-flat','terrain-dug']
OLD={'tuoyun':('units','tuoyun-r1'),'solar':('facilities','solar-r1'),'processor':('facilities','processor-r1')}
def run(keys=NEW):
    assert keys and set(keys)<=set(NEW),keys
    PROJECT.mkdir(parents=True,exist_ok=True)
    (PROJECT/'project.godot').write_text('config_version=5\n[application]\nconfig/name="Yudian Art LF Batch"\n[display]\nwindow/size/viewport_width=1920\nwindow/size/viewport_height=1200\n[rendering]\nrenderer/rendering_method="forward_plus"\n')
    shutil.copytree(ROOT/'prototype/assets/materials/art-r1',PROJECT/'assets/materials/art-r1',dirs_exist_ok=True)
    shutil.copytree(ROOT/'prototype/scenes/art_preview_r1',PROJECT/'scenes/art_preview_r1',dirs_exist_ok=True)
    for category,name in OLD.values():
        shutil.copytree(ROOT/'prototype/assets'/category/name,PROJECT/'assets'/category/name,dirs_exist_ok=True)
    runtime=ROOT/'prototype/assets/lowfi-batch-r1'
    for key in keys:
        model=runtime/'models'/(key+'.glb')
        assert model.is_file() and model.stat().st_size>0,model
        sidecar=model.with_suffix('.glb.import')
        assert sidecar.is_file() and '"optimizer/enabled": false' in sidecar.read_text(),sidecar
        tow=key in ['wangshan','zhulei']
        scene='[gd_scene load_steps=%d format=3]\n'% (4 if tow else 3)
        scene+='[ext_resource type="Script" path="res://assets/lowfi-batch-r1/preview.gd" id="1"]\n'
        scene+='[ext_resource type="PackedScene" path="res://assets/lowfi-batch-r1/models/%s.glb" id="2"]\n'%key
        if tow:scene+='[ext_resource type="PackedScene" path="res://assets/lowfi-batch-r1/models/recovery.glb" id="3"]\n'
        scene+='[node name="SampleRoot" type="Node3D"]\nscript=ExtResource("1")\nasset_key="%s"\n'%key
        scene+='[node name="Model" parent="." instance=ExtResource("2")]\n'
        if tow:scene+='[node name="TowDemo" parent="." instance=ExtResource("3")]\nposition=Vector3(0, 0.24, -0.975)\nvisible=false\n'
        (runtime/(key+'.tscn')).write_text(scene)
    shutil.copytree(runtime,PROJECT/'assets/lowfi-batch-r1',dirs_exist_ok=True)
    for name in ['validate.gd','capture.gd','relation.gd','clearance.gd']:
        if (HERE/name).is_file():shutil.copy2(HERE/name,PROJECT/name)
    print('PREVIEW_READY',PROJECT)
if __name__=='__main__':run(sys.argv[1:] or NEW)
