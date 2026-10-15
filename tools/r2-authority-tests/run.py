#!/usr/bin/env python3
"""Run the existing R2 Main authority contract after a separate project build/import.

Headless engineering evidence only; never builds, imports, exports, or changes simulation.
"""
import argparse
import os
from pathlib import Path
import re
import signal
import subprocess


def absolute_path(raw):
    if raw.startswith(('user://', 'res://')) or not os.path.isabs(raw):
        raise argparse.ArgumentTypeError('must be an explicit absolute filesystem path')
    return Path(raw)


def positive_seconds(raw):
    value = int(raw)
    if value <= 0:
        raise argparse.ArgumentTypeError('must be a positive number of wall-clock seconds')
    return value


def repo_roots():
    """Read the worktree link to protect both this worktree and the shared checkout."""
    repo = Path(__file__).resolve().parents[2]
    roots = {repo}
    metadata = repo / '.git'
    if metadata.is_file():
        link = metadata.read_text(encoding='utf-8').strip()
        if not link.startswith('gitdir: '):
            raise ValueError('cannot identify worktree metadata')
        gitdir = (repo / link[len('gitdir: '):]).resolve(strict=True)
        common = (gitdir / (gitdir / 'commondir').read_text(encoding='utf-8').strip()).resolve(strict=True)
        roots.add(common.parent)
    elif not metadata.is_dir():
        raise ValueError('cannot identify repository root')
    return roots


def require_private_output(raw, project):
    if os.path.lexists(raw):
        raise ValueError('--output already exists; choose a new directory, never overwrite evidence')
    output = raw.resolve()
    home = Path.home()
    # Protect the complete Godot userdata area, including formal user://saves.
    forbidden = repo_roots() | {
        project,
        home / 'Library' / 'Application Support' / 'Godot',
        Path(os.environ.get('XDG_DATA_HOME', str(home / '.local' / 'share'))) / 'godot',
    }
    if os.name == 'nt':
        forbidden.add(Path(os.environ.get('APPDATA', str(home / 'AppData' / 'Roaming'))) / 'Godot')
    for area in forbidden:
        area = area.resolve()
        if output.is_relative_to(area) or area.is_relative_to(output):
            raise ValueError('--output must be outside repositories, project and formal Godot userdata: ' + str(area))
    return output


def stop_owned_process(process):
    """Terminate only the session created for this engine, then reap its direct child."""
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        process.wait()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--engine', type=absolute_path, required=True, help='existing Godot .NET executable')
    parser.add_argument('--project', type=absolute_path, required=True, help='already built/imported prototype directory')
    parser.add_argument('--output', type=absolute_path, required=True, help='new private directory outside repositories and formal userdata')
    parser.add_argument('--timeout', type=positive_seconds, default=1800,
                        help='wall-clock limit in seconds (default: 1800); simulation timing stays unchanged')
    args = parser.parse_args()
    try:
        engine = args.engine.resolve(strict=True)
        project = args.project.resolve(strict=True)
        if not engine.is_file() or not os.access(engine, os.X_OK):
            raise ValueError('--engine must be an existing executable file')
        if not project.is_dir() or not (project / 'project.godot').is_file():
            raise ValueError('--project must contain project.godot')
        output = require_private_output(args.output, project)
        output.mkdir(parents=True, exist_ok=False)
        private_saves = output / 'saves'
        private_saves.mkdir()
    except (OSError, ValueError) as error:
        parser.error(str(error))

    env = {key: value for key, value in os.environ.items() if not key.startswith('YUDIAN_')}
    env.update(YUDIAN_R2_SELF_TEST='1', YUDIAN_PLAYER_GUI_TEST='1',
               YUDIAN_TUTORIAL_TEST_ROOT=str(private_saves))
    stdout_path = output / 'stdout.log'
    native_path = output / 'engine.log'
    command = [str(engine), '--headless', '--fixed-fps', '60', '--path', str(project),
               '--log-file', str(native_path), '--']
    try:
        with stdout_path.open('x', encoding='utf-8') as log:
            process = subprocess.Popen(command, cwd=project, env=env, stdout=log,
                                       stderr=subprocess.STDOUT, start_new_session=True)
            try:
                returncode = process.wait(timeout=args.timeout)
            except subprocess.TimeoutExpired:
                stop_owned_process(process)
                print(f'R2_AUTHORITY NOT_COMPLETED timeout={args.timeout} evidence={output}', flush=True)
                return 124
            except KeyboardInterrupt:
                stop_owned_process(process)
                print(f'R2_AUTHORITY NOT_COMPLETED interrupted evidence={output}', flush=True)
                return 130
    except OSError as error:
        print(f'R2_AUTHORITY FAIL launch_or_log={error} evidence={output}', flush=True)
        return 1

    try:
        stdout = stdout_path.read_text(encoding='utf-8', errors='replace')
        native = native_path.read_text(encoding='utf-8', errors='replace')
    except OSError as error:
        print(f'R2_AUTHORITY FAIL exit={returncode} missing_or_unreadable_log={error} evidence={output}', flush=True)
        return 1
    if returncode != 0:
        print(f'R2_AUTHORITY FAIL exit={returncode} evidence={output}', flush=True)
        return returncode if returncode > 0 else 1
    if re.search(r'\bERROR\b|Unhandled\s*exception', stdout + '\n' + native, re.IGNORECASE):
        print(f'R2_AUTHORITY ERROR_LOG exit=0 evidence={output}', flush=True)
        return 1
    marker = next((line for line in stdout.splitlines()
                   if line == 'R2_AUTHORITY PASS' or line.startswith('R2_AUTHORITY PASS ')), None)
    if marker is None:
        print(f'R2_AUTHORITY FAIL exit=0 missing_pass_marker evidence={output}', flush=True)
        return 1
    print(marker, flush=True)
    print(f'R2_AUTHORITY_RUN exit=0 timeout={args.timeout} evidence={output}', flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
