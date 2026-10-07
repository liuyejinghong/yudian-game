#!/usr/bin/env python3
"""D1.2 actual-engine processes; private isolated files at each production boundary."""
import argparse
import os
from pathlib import Path
import subprocess

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--engine', type=Path, required=True)
p.add_argument('--project', type=Path)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
a.output.mkdir(parents=True, exist_ok=False)
command = [str(a.engine), '--headless', '--fixed-fps', '60']
if a.project:
    command += ['--path', str(a.project)]

def run(name, phase, save, marker):
    env = dict(os.environ, YUDIAN_DEVELOPMENT_SELF_TEST='1',
               YUDIAN_DEVELOPMENT_TEST_PHASE=phase, YUDIAN_PLAYER_TEST_SAVE=str(save))
    with (a.output / (name + '.log')).open('w') as log:
        subprocess.run(command + ['--log-file', str(a.output / (name + '.engine.log'))], env=env, stdout=log, stderr=subprocess.STDOUT,
                       check=True, timeout=300)
    output = (a.output / (name + '.log')).read_text()
    assert marker in output and 'MAIN_GROUND_IDENTITY' in output, name
    print('PASS:', name, flush=True)

run('full', 'full', a.output / 'full.json', 'D12_TEST PASS')
for boundary in ('mine', 'cargo', 'input', 'output'):
    save = a.output / (boundary + '.json')
    run(boundary + '-prepare', 'prepare-' + boundary, save, 'D12_TEST PREPARED')
    run(boundary + '-resume', 'resume-' + boundary, save, 'D12_TEST RESTORED')
print('D12_WORLD_TESTS PASS processes=9', flush=True)
