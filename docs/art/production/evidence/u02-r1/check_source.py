"""Run with Blender --background --factory-startup --python-exit-code 1 --python FILE."""
import importlib.util, tempfile
from pathlib import Path
from types import SimpleNamespace

source=Path(__file__).resolve().parents[5]/'art/source/units/zhulei-r1'
spec=importlib.util.spec_from_file_location('u02_build',source/'build_sample.py')
build=importlib.util.module_from_spec(spec);spec.loader.exec_module(build)
build.cmd_verify(SimpleNamespace(verify=source/'zhulei-r1.blend',compare=source/'zhulei-r1.glb'))
with tempfile.TemporaryDirectory(prefix='u02-empty-') as tmp:
    empty=Path(tmp)/'empty.glb';empty.touch()
    try:build.check_outputs([empty])
    except AssertionError:print('U02_EXISTING_EMPTY_OUTPUT_REJECTED')
    else:raise AssertionError('existing zero-byte GLB accepted')
