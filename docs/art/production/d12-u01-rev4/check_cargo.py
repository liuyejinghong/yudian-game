"""Run from repo root: python3 docs/art/production/d12-u01-rev4/check_cargo.py."""
from pathlib import Path
import importlib.util,json
ROOT=Path(__file__).resolve().parents[4]
assert (ROOT/'art/source/units/tuoyun-r1').is_dir()
def load(path):
    spec=importlib.util.spec_from_file_location('cargo',path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module
new=load(ROOT/'art/source/units/tuoyun-r1/generate_tuoyun_r1.py')
old=load(Path(__file__).parent/'before/generate_tuoyun_r1.py')
a,b=old.geom_cargo(),new.geom_cargo()
assert max(v[1] for v in a[1][1])==max(v[1] for v in a[2][1]),'baseline lacks reported coplanar top'
assert abs(max(v[1] for v in b[1][1])-.8)<1e-9
assert max(v[1] for v in b[1][1])<max(v[1] for v in b[2][1])
assert new.bbox(a)==new.bbox(b),'cargo envelope changed'
assert a[0]==b[0] and a[2]==b[2],'base or lid changed'
assert old.SOCKETS==new.SOCKETS
assert old.anim_tracks()==new.anim_tracks()
def nodes(root):
    result={}
    def walk(n):
        result[n.name]=(n.pivot,n.surfs)
        for c in n.children:walk(c)
    walk(root);return result
x,y=nodes(old.build_scene()),nodes(new.build_scene())
assert x.keys()==y.keys()
assert all(x[k]==y[k] for k in x if k!='CargoBox'),'unrelated geometry changed'
assert x['CargoBox'][0]==y['CargoBox'][0]
print(json.dumps({'result':'PASS','baseline_top':.86,'body_top':.8,'lid_top':.86,'unchanged_nodes':len(x)-1,'sockets':'unchanged','animation_tracks':'unchanged'}))
