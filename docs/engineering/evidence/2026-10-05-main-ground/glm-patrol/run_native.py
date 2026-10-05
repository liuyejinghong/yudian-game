import os
import subprocess

WORKER = "<user>/yudian-game/ground-patrol-worker-20261005"
GODOT = "<user>/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot"
OUT = "<scratch>/ground-patrol-worker-20261005"
RUN = "run5"

env = dict(os.environ, DOTNET_ROOT=os.path.expanduser("~/.dotnet"))
cmd = [GODOT, "--headless", "--path", "prototype",
       "res://scenes/ground-patrol-tests/GroundPatrolTests.tscn"]

log_path = f"{OUT}/ground-patrol-native-{RUN}.log"
receipt_path = f"{OUT}/ground-patrol-native-{RUN}-receipt.txt"

try:
    r = subprocess.run(cmd, cwd=WORKER, env=env, capture_output=True,
                       text=True, timeout=240)
    stdout, stderr, code, status = r.stdout, r.stderr, r.returncode, "ok"
except subprocess.TimeoutExpired as e:
    def s(x):
        return x.decode("utf-8", "replace") if isinstance(x, bytes) else (x or "")
    stdout, stderr = s(e.stdout), s(e.stderr)
    code, status = None, "TIMEOUT after 240s"

with open(log_path, "w") as f:
    f.write("--- stdout ---\n")
    f.write(stdout)
    f.write("\n--- stderr ---\n")
    f.write(stderr)

with open(receipt_path, "w") as f:
    f.write(f"status={status}\nexit={code}\nlog={log_path}\n")

print(f"status={status} exit={code}")
