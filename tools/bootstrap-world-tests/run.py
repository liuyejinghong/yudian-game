#!/usr/bin/env python3
"""Run D1.1 in nine actual engine processes, using an isolated save per boundary."""
import argparse
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--engine', type=Path, required=True)
p.add_argument('--project', type=Path, help='Source project; omit for exported executable')
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
a.output.mkdir(parents=True, exist_ok=False)
command = [str(a.engine), '--headless', '--fixed-fps', '60']
if a.project:
    command += ['--path', str(a.project)]

def run(name, phase, save, marker):
    env = dict(os.environ, YUDIAN_BOOTSTRAP_SELF_TEST='1',
               YUDIAN_BOOTSTRAP_TEST_PHASE=phase, YUDIAN_PLAYER_TEST_SAVE=str(save))
    with (a.output / (name + '.log')).open('w') as log:
        subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT,
                       check=True, timeout=240)
    output = (a.output / (name + '.log')).read_text()
    assert marker in output, (name, 'missing completion marker')
    assert 'MAIN_GROUND_IDENTITY' in output, (name, 'missing loaded identity')
    print('PASS:', name, flush=True)

run('full', 'full', a.output / 'full.json', 'BOOTSTRAP_TEST PASS')
for boundary, phase, marker in [
    ('cargo', '', 'CROSS_PROCESS_RESTORED'),
    ('service', '-service', 'CROSS_PROCESS_SERVICE_RESTORED'),
    ('active', '-active', 'CROSS_PROCESS_ACTIVE_RESTORED'),
    ('charge', '-charge', 'CROSS_PROCESS_CHARGE_RESTORED'),
]:
    save = a.output / (boundary + '.json')
    run(boundary + '-prepare', 'prepare' + phase, save, 'BOOTSTRAP_TEST PREPARED')
    run(boundary + '-resume', 'resume' + phase, save, 'BOOTSTRAP_TEST ' + marker)
print('BOOTSTRAP_WORLD_TESTS PASS processes=9', flush=True)
