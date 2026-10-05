"""Verify archived Mars r2 evidence and current model inputs; no GPU claims."""
from pathlib import Path
import hashlib,json,struct
R=Path(__file__).resolve().parent.parent
W=next(p for p in R.parents if (p/'art/manifests').is_dir())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 index=json.loads((R/'capture-index.json').read_text())
 for f in index['files']:assert sha(R/f['path'])==f['archive_sha256'],f['path']
 for path,h in index['inputs'].items():assert sha(W/path)==h,path
 images=list((R/'images').rglob('*.png'));assert len(images)==index['png_count']==33
 for p in images:assert struct.unpack('>II',p.read_bytes()[16:24])==(1920,1200),p
 for a,k,cases,tri,surf in [('tuoyun','units',98,7312,109),('solar','facilities',392,888,74),('processor','facilities',49,1224,33)]:
  m=json.loads((W/f'art/manifests/{a}-r1.json').read_text())
  assert m['glb_sha256']==sha(W/f'art/source/{k}/{a}-r1/{a}-r1.glb')==sha(W/f'prototype/assets/{k}/{a}-r1/{a}-r1.glb')
  assert len(m['material_bindings'])==surf and m['triangles']==tri
  assert all(sha(W/f['path'])==f['sha256'] for f in m['source']['files'])
  r=json.loads((R/f'runs/{a}-actual.json').read_text())
  assert r['cases']==cases and r['negative_hold']==10 and not r['failures'] and r['missing_clip_holds']
  assert r['reverse_time']==4 and r['reason_pose']==9
  if a=='tuoyun':assert r['wheel_pose_checks']==24 and len(set(r['move_tracks']))==6
 print('PASS archive hashes, 33 PNG dimensions, current source/runtime/manifest inputs, 539+30+24 reports. Owner/engineering/gameplay NOT_RUN.')
if __name__=='__main__':main()
