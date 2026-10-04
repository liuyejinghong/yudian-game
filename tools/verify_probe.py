#!/usr/bin/env python3
"""验证导出探针异常输入在建场景前非零退出，且不留下成功报告。"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[1]


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--app',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    a=p.parse_args()
    if a.output.exists(): raise ValueError('检查目录已存在')
    a.output.mkdir(parents=True)
    binary=a.app.resolve()/'Contents/MacOS/Yudian'
    base=json.loads((ROOT/'prototype/fixtures/s_small.json').read_text())
    changes=[('total_mismatch',lambda x:x['scale'].update(robots_total=100)),
             ('facilities_short',lambda x:x['scale'].update(facilities=3)),
             ('radius_zero',lambda x:x['scale'].update(ring_radius=0)),
             ('position_short',lambda x:x['terrain']['mound'].update(position=[1])),
             ('null_quality',lambda x:x.update(quality=None)),
             ('msaa_invalid',lambda x:x['quality'].update(msaa_3d=3)),
             ('tiny_feature_radius',lambda x:x['terrain']['mineral_pit'].update(radius=1e-30)),
             ('tiny_ring_radius',lambda x:x['scale'].update(ring_radius=1e-30)),
             ('tiny_camera_period',lambda x:x['camera_path'].update(period_seconds=1e-45))]
    results=[]
    with tempfile.TemporaryDirectory() as temp:
        folder=Path(temp)
        valid_frames=folder/'positive-frames.csv'; valid_summary=folder/'positive-summary.json'
        control=subprocess.run([str(binary),'--headless','--','--benchmark','--duration','0.2','--frames-csv',str(valid_frames),'--summary-json',str(valid_summary)],cwd='/private/tmp',capture_output=True,text=True,timeout=12)
        if control.returncode != 0 or not valid_summary.exists():
            raise AssertionError('同一binary合法输入正控制失败；不能接受异常矩阵')
        positive=json.loads(valid_summary.read_text())
        if positive['status']!='completed' or positive['observed']['robots']!=12 or positive['frames']<=0:
            raise AssertionError('合法输入未完成原始S档')
        results.append({'case':'positive_control','exit':0,'status':'completed','robots':12})
        for name,change in changes:
            cfg=json.loads(json.dumps(base)); change(cfg)
            f=folder/f'{name}.json'; f.write_text(json.dumps(cfg))
            changes_args=['--fixture',str(f)]
            results.append(check(binary,name,changes_args,folder,a.output))
        broken=folder/'broken.json'; broken.write_text('{bad json')
        cli=[('broken_json',['--fixture',str(broken)]),('duration_nan',['--duration','NaN']),
             ('duration_infinity',['--duration','Infinity']),('duration_missing',['--duration']),
             ('empty_fixture',['--fixture','']),('unknown',['--duratio','1']),
             ('duplicate',['--duration','1','--duration','2']),
             ('output_res',['--frames-csv','res://output.csv']),
             ('same_output',['--frames-csv',str(folder/'same'),'--summary-json',str(folder/'same')]),
             ('unwritable',['--frames-csv','/System/yudian-denied.csv'])]
        for name,args in cli: results.append(check(binary,name,args,folder,a.output))
    (a.output/'results.json').write_text(json.dumps(results,indent=2)+'\n')
    print(f'PROBE_INPUT_OK: positive control + {len(results)-1} invalid cases, expected errors, exit 1 before scene, no completed summary')


def check(binary,name,args,temp,output):
    frames=temp/f'{name}-frames.csv'; summary=temp/f'{name}-summary.json'
    # 每例显式路径；路径本身的异常例使用自己的覆盖值。
    extras=[]
    if '--frames-csv' not in args: extras+=['--frames-csv',str(frames)]
    if '--summary-json' not in args: extras+=['--summary-json',str(summary)]
    result=subprocess.run([str(binary),'--headless','--','--benchmark',*extras,*args],cwd='/private/tmp',capture_output=True,text=True,timeout=12)
    log=result.stdout+result.stderr
    expected={'total_mismatch':'robots_total','facilities_short':'facilities','radius_zero':'ring_radius','position_short':'position','null_quality':'quality','msaa_invalid':'msaa_3d','broken_json':'json','duration_nan':'--duration','duration_infinity':'--duration','duration_missing':'--duration','empty_fixture':'--fixture','unknown':'--duratio','duplicate':'--duration','output_res':'res://','same_output':'frames_csv','unwritable':'/System/','tiny_feature_radius':'terrain.mineral_pit.radius','tiny_ring_radius':'scale.ring_radius','tiny_camera_period':'camera_path.period_seconds'}[name]
    if result.returncode != 1 or '[Yudian] fixture=' in log or '[Yudian]' not in log or expected.lower() not in log.lower():
        raise AssertionError(f'{name}:未在建场景前非零退出; exit={result.returncode}')
    if summary.exists() and json.loads(summary.read_text()).get('status')=='completed':
        raise AssertionError(name+':出现成功summary')
    log=log.replace(str(ROOT),'<checkout>').replace(str(temp),'<test-input>')
    (output/f'{name}.log').write_text(log)
    return {'case':name,'exit':result.returncode,'scene_created':False,'completed_summary':False}


if __name__=='__main__': main()
