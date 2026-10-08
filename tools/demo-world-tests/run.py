#!/usr/bin/env python3
"""Native entry/savequit and read-model checks, using explicitly isolated slots."""
import argparse, os, subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--engine',type=Path,required=True);p.add_argument('--project',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--r3-checkpoints',type=Path,required=True);p.add_argument('--ore-save',type=Path,required=True);a=p.parse_args();a.output.mkdir(exist_ok=False,parents=True)
command=[str(a.engine),'--headless','--fixed-fps','60']
if a.project: command+=['--path',str(a.project)]
def run(phase,save):
    env=dict(os.environ,YUDIAN_DEMO_SELF_TEST='1',YUDIAN_PLAYER_GUI_TEST='1',YUDIAN_DEMO_TEST_PHASE=phase,YUDIAN_PLAYER_TEST_SAVE=str(save))
    log=a.output/(phase+'.log')
    with log.open('w') as f: subprocess.run(command+['--log-file',str(a.output/(phase+'.engine.log'))],env=env,stdout=f,stderr=subprocess.STDOUT,check=True,timeout=60)
    text=log.read_text(); marker='DEMO_TEST SAVEQUIT_REQUEST' if phase=='prepare' else 'DEMO_TEST PASS phase='+phase
    assert marker in text and 'MAIN_GROUND_IDENTITY' in text,phase
    assert '\nERROR:' not in text and 'leaked at exit' not in text,phase+' engine error or leaked resource'
    if phase=='prepare': assert 'PLAYER_SAVE_OK' in text[text.index(marker):],phase
    print('PASS:',phase,flush=True)
save=a.output/'entry.json';save.write_text('{broken old test slot');run('prepare',save);run('resume',save)
for phase,name in [('recipe','recipe-input'),('cargo','cargo'),('completed','storage-completed'),('rollback','recipe-input')]:
    save=a.output/(phase+'.json');save.write_bytes((a.r3_checkpoints/('yudian-d12-gui-r3-'+name+'.json')).read_bytes());run(phase,save)
save=a.output/'ore.json';save.write_bytes(a.ore_save.read_bytes());run('ore',save)
save=a.output/'output.json';save.write_bytes((a.ore_save.parent/'output.json').read_bytes());run('output',save)
print('DEMO_WORLD_TESTS PASS processes=8')
