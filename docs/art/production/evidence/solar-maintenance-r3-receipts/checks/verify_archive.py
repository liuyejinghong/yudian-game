"""Check current F01 inputs and the r3 evidence archive; no GPU claims."""
from pathlib import Path
import hashlib,json,struct
R=Path(__file__).resolve().parent.parent
W=next(p for p in R.parents if (p/'art/manifests/solar-r1.json').is_file())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 index=json.loads((R/'capture-index.json').read_text())
 for f in index['files']:assert sha(R/f['path'])==f['archive_sha256'],f['path']
 for path,h in index['inputs'].items():assert sha(W/path)==h,path
 pngs=list((R/'images').rglob('*.png'));assert len(pngs)==index['png_count']==10
 for p in pngs:assert struct.unpack('>II',p.read_bytes()[16:24])==(1920,1200),p
 m=json.loads((W/'art/manifests/solar-r1.json').read_text());assert m['revision']==3 and m['bounds']['active']['max'][2]==1.85
 assert m['glb_sha256']==sha(W/'art/source/facilities/solar-r1/solar-r1.glb')==sha(W/'prototype/assets/facilities/solar-r1/solar-r1.glb')
 assert all(sha(W/f['path'])==f['sha256'] for f in m['source']['files'])
 assert m['triangles']==888 and len(m['material_bindings'])==74
 imported=json.loads((R/'runs/solar-import.json').read_text())
 binds=[{'node':'Model/'+n['path'].split('/',3)[3],'surface':s['surface'],'role':s['material']} for n in imported['meshes'] for s in n['surfaces']]
 assert binds==m['material_bindings'] and imported['triangles']==888
 actual=json.loads((R/'runs/solar-actual.json').read_text())
 assert not actual['failures'] and actual['cases']==392 and actual['negative_hold']==10 and actual['maintenance_pose_checks']==16
 assert actual['reverse_time']==4 and actual['reason_pose']==9 and actual['missing_clip_holds']
 clearance=json.loads((R/'runs/cover-clearance.json').read_text())
 assert clearance['samples']==111 and clearance['pair_count']==144 and clearance['min_separation_m']>=.005
 assert clearance['moving_bounds']['max'][2]<=m['bounds']['active']['max'][2]
 print('PASS F01 r3 source/runtime/bindings, archive hashes, 10 PNGs, 392+10+16 checks. Blind/owner/engineering NOT_RUN.')
if __name__=='__main__':main()
