#!/usr/bin/env python3
"""Run the actual Main player mode in separate native engine processes; retain each run."""
import argparse
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--godot', type=Path, required=True)
p.add_argument('--dotnet', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
a.output = a.output.resolve()
a.output.mkdir(parents=True, exist_ok=False)
env = dict(os.environ, DOTNET_ROOT=str(a.dotnet.parent))
with (a.output / 'build.log').open('w') as log:
    subprocess.run([str(a.dotnet), 'build', str(ROOT / 'prototype/Yudian.csproj'), '--no-restore', '-m:1', '-p:UseSharedCompilation=false', '-nodeReuse:false'], env=env, stdout=log, stderr=subprocess.STDOUT, check=True)

def run(name, phase, save, marker):
    test_env = dict(env, YUDIAN_PLAYER_SELF_TEST='1', YUDIAN_PLAYER_TEST_PHASE=phase, YUDIAN_PLAYER_TEST_SAVE=str(save))
    log_path = a.output / (name + '.log')
    with log_path.open('w') as log:
        subprocess.run([str(a.godot), '--headless', '--path', str(ROOT / 'prototype'), '--fixed-fps', '60', '--log-file', str(a.output / (name + '-engine.log')), '--'], env=test_env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=120)
    assert marker in log_path.read_text(), (name, 'missing completion marker')
    print('PASS:', name)

full = a.output / 'full.json'
cross = a.output / 'cross-process.json'
run('full', 'full', full, 'PLAYER_SELF_TEST PASS steps=22')
run('prepare', 'prepare', cross, 'PLAYER_CROSS_PROCESS_PREPARE PASS')
run('resume', 'resume', cross, 'PLAYER_CROSS_PROCESS_RESUME PASS')
run('completed', 'completed', cross, 'PLAYER_CROSS_PROCESS_COMPLETED PASS')
run('committed-before-physics', 'completed', Path(str(full) + '.awaiting'), 'PLAYER_CROSS_PROCESS_COMPLETED PASS')
