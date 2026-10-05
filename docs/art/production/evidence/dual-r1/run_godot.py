"""Run this task's Godot command with its actual .NET installation and timeout."""
import argparse,os,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--godot',required=True);p.add_argument('--dotnet-root',required=True);p.add_argument('--log',type=Path,required=True);p.add_argument('args',nargs=argparse.REMAINDER);a=p.parse_args()
assert Path(a.godot).is_file() and (Path(a.dotnet_root)/'host/fxr').is_dir()
env=os.environ.copy();env['DOTNET_ROOT']=a.dotnet_root;env['PATH']=a.dotnet_root+os.pathsep+env.get('PATH','')
args=a.args[1:] if a.args and a.args[0]=='--' else a.args
a.log.parent.mkdir(parents=True,exist_ok=True)
with a.log.open('w') as f:
    try:r=subprocess.run([a.godot,*args],env=env,stdout=f,stderr=subprocess.STDOUT,timeout=55)
    except subprocess.TimeoutExpired:f.write('\nTASK_TIMEOUT_55_SECONDS\n');raise SystemExit(124)
print(a.log.read_text());raise SystemExit(r.returncode)
