import copy, hashlib, json, os, pathlib, subprocess
root=pathlib.Path('<user>/yudian-game/main-ground-r1-20261005')
out=pathlib.Path('<scratch>/main-ground-main-20261005')
godot='<user>/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot'
env=dict(os.environ,DOTNET_ROOT=os.path.expanduser('~/.dotnet'))
env.pop('YUDIAN_LIVE_SELF_TEST',None);env.pop('YUDIAN_BENCHMARK',None)
base=json.loads((root/'prototype/fixtures/s_small.json').read_text())
cases=[('old-default',['--quit-after','30'],[],0,None),('old-short-benchmark',[],['--benchmark','--duration','1','--frames-csv',str(out/'fixed-frames.csv'),'--summary-json',str(out/'fixed-summary.json')],0,'benchmark completed'),('live-benchmark-conflict',[],['--live-terrain','--duration','1'],1,'互斥')]
for name,mutate in [('segments',lambda c:c['terrain'].update(segments=512)),('spacing',lambda c:c['terrain'].update(size=10000)),('height',lambda c:c['terrain']['mound'].update(amount=2000)),('layout',lambda c:c['scale'].update(ring_radius=100))]:
    c=copy.deepcopy(base); mutate(c);p=out/f'range-{name}.json';p.write_text(json.dumps(c))
    cases.append((f'live-range-{name}',[],['--live-terrain','--fixture',str(p)],1,'[Yudian] ArgumentException'))
    cases.append((f'old-range-{name}',['--quit-after','10'],['--fixture',str(p)],0,'robots=12'))
receipt=[]
for name,engine,user,expected,contains in cases:
    args=[godot,'--headless','--path',str(root/'prototype'),*engine,'--',*user]
    run=subprocess.run(args,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=45)
    (out/f'{name}.log').write_text(run.stdout)
    ok=run.returncode==expected and (contains is None or contains in run.stdout)
    if expected==0:ok=ok and 'ERROR:' not in run.stdout and 'WARNING:' not in run.stdout
    receipt.append(dict(name=name,args=args,exit=run.returncode,expected=expected,passed=ok))
    print(name,run.returncode,'PASS' if ok else 'FAIL',flush=True)
    if not ok: print(run.stdout,flush=True)
s=json.loads((out/'fixed-summary.json').read_text()); dll=root/'prototype/.godot/mono/temp/bin/Debug/Yudian.dll'
assert s['assembly_sha256']==hashlib.sha256(dll.read_bytes()).hexdigest()
assert s['assembly_hash_source']=='Debug file with loaded MVID check' and s['assembly_mvid']
assert s['observed']['robots']==12 and s['observed']['facilities']==6 and not s['graphical_performance_eligible']
(out/'compat-receipt.json').write_text(json.dumps(receipt,indent=2,ensure_ascii=False))
print('SUMMARY',len(receipt),'cases; assembly identity and headless ineligibility checked')
raise SystemExit(0 if all(x['passed'] for x in receipt) else 1)
