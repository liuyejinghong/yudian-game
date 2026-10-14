#!/usr/bin/env python3
"""Real Main authority: two finite natural directions, checkpoint/restart, lifecycle guards and old power recovery.

No engine TimeScale, health, or material grants on the natural routes. The full-storage
route also cancels a fully supplied solar proposal and switches to storage, proving
first-learning-phase receipts survive a genuine change of mind. This is an engineering
command driver, not graphical player experience acceptance.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--engine', type=Path, required=True)
parser.add_argument('--project', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--phase', choices=('all', 'full-storage', 'full-solar', 'boundaries', 'recovery'), default='all')
parser.add_argument('--timeout', type=int, default=1200, help='wall-clock process limit; physics deltas and TimeScale stay unchanged')
parser.add_argument('--legacy-copy', type=Path, help='explicit private readonly schema3 trigger copy, never a formal save path')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=False)
command = [str(args.engine), '--headless', '--fixed-fps', '60', '--path', str(args.project)]
processes = 0


def run(name, phase, root, marker, direction='storage'):
    global processes
    root.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, YUDIAN_TUTORIAL_SELF_TEST='1', YUDIAN_TUTORIAL_TEST_PHASE=phase,
               YUDIAN_TUTORIAL_TEST_ROOT=str(root.resolve()), YUDIAN_TUTORIAL_NATURAL_DIRECTION=direction)
    for key in ('YUDIAN_PLAYER_SELF_TEST', 'YUDIAN_BOOTSTRAP_SELF_TEST', 'YUDIAN_DEVELOPMENT_SELF_TEST', 'YUDIAN_PLAYER_GUI_TEST'):
        env.pop(key, None)
    with (args.output / (name + '.log')).open('w') as log:
        result = subprocess.run(command + ['--log-file', str(args.output / (name + '.engine.log'))], env=env,
                                stdout=log, stderr=subprocess.STDOUT, timeout=args.timeout)
    text = (args.output / (name + '.log')).read_text()
    if result.returncode != 0 or marker not in text or 'MAIN_GROUND_IDENTITY' not in text or 'ERROR:' in text:
        raise RuntimeError(f'{name} failed exit={result.returncode}; inspect {args.output / (name + ".log")}')
    processes += 1
    for line in text.splitlines():
        if marker in line or 'TUTORIAL_CANCEL_LINEAGE PASS' in line:
            print(line, flush=True)
    print('PASS:', name, 'exit=0', flush=True)


phases = ('full-storage', 'full-solar', 'boundaries', 'recovery') if args.phase == 'all' else (args.phase,)
for phase in phases:
    root = args.output / (phase + '-slots')
    if phase.startswith('full-'):
        direction = phase[5:]
        run(phase + '-prepare', phase, root, 'TUTORIAL_PREPARED PASS', direction)
        run(phase + '-resume', 'resume', root, 'TUTORIAL_FULL PASS', direction)
    elif phase == 'recovery':
        if not args.legacy_copy:
            raise ValueError('recovery requires the explicitly authorized private --legacy-copy')
        original = args.legacy_copy.read_bytes()
        if json.loads(original)['Schema'] != 3:
            raise ValueError('recovery copy must be schema3')
        root.mkdir(parents=True, exist_ok=False)
        shutil.copyfile(args.legacy_copy, root / 'd13-tutorial-v4-slot-1.json')
        digest = hashlib.sha256(original).hexdigest()
        run(phase, phase, root, 'TUTORIAL_RECOVERY PASS')
        assert args.legacy_copy.read_bytes() == original, 'source recovery copy was changed'
        print('PASS: legacy source unchanged sha256=' + digest, flush=True)
    else:
        run(phase, phase, root, 'TUTORIAL_BOUNDARIES PASS')
print('TUTORIAL_WORLD_TESTS PASS processes=' + str(processes), flush=True)
