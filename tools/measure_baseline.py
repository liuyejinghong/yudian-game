#!/usr/bin/env python3
"""运行导出包，保留原始帧、进程内存原文和逐次独立汇总。"""
import argparse
import csv
import hashlib
import json
import math
import os
from pathlib import Path
import re
import signal
import subprocess


def recompute(path):
    with path.open() as f:
        reader = csv.DictReader(f)
        if reader.fieldnames != ['frame', 'time_s', 'frame_ms', 'fps']:
            raise ValueError('CSV 字段不符')
        rows = list(reader)
    if not rows: raise ValueError('没有帧数据')
    times = [float(row['frame_ms']) for row in rows]
    if any(not math.isfinite(x) or x <= 0 for x in times): raise ValueError('无效帧时间')
    if [int(row['frame']) for row in rows] != list(range(1, len(rows)+1)):
        raise ValueError('帧序号不连续')
    ordered = sorted(times)
    return {'frames':len(rows),'duration_csv_s':float(rows[-1]['time_s']),
            'avg_fps':len(rows)/sum(times)*1000,
            'nearest_rank_ms':{str(p):ordered[math.ceil(len(rows)*p/100)-1] for p in [50,95,99]},
            'max_ms':ordered[-1],'over_33ms':sum(x>33 for x in times),
            'csv_sha256':hashlib.sha256(path.read_bytes()).hexdigest()}


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--app',type=Path)
    p.add_argument('--output',type=Path)
    p.add_argument('--runs',type=int,default=3)
    p.add_argument('--duration',type=float,default=45)
    p.add_argument('--recompute',type=Path)
    a=p.parse_args()
    if a.recompute:
        print(json.dumps(recompute(a.recompute),indent=2)); return
    if not a.app or not a.output: p.error('--app 与 --output 必需')
    if a.runs < 1 or not math.isfinite(a.duration) or not 0 < a.duration <= 3600: p.error('无效采样参数')
    a.app=a.app.resolve(); a.output=a.output.resolve()
    if a.output.exists(): raise ValueError('证据目录已存在，不覆盖')
    a.output.mkdir(parents=True)
    for index in range(1,a.runs+1):
        folder=a.output/f'run-{index}'; folder.mkdir()
        args=['/usr/bin/time','-l','-o',str(folder/'memory.txt'),str(a.app/'Contents/MacOS/Yudian'),
              '--rendering-driver','metal','--','--benchmark','--duration',str(a.duration),
              '--frames-csv',str(folder/'frames.csv'),'--summary-json',str(folder/'summary.json')]
        with (folder/'launch.log').open('w') as log:
            process=subprocess.Popen(args,cwd='/private/tmp',stdout=log,stderr=subprocess.STDOUT,start_new_session=True,
                                     env={**os.environ,'DOTNET_ROOT':'/nonexistent','DOTNET_ROOT_ARM64':'/nonexistent','DOTNET_MULTILEVEL_LOOKUP':'0'})
            try:
                code=process.wait(timeout=a.duration+60)
            except BaseException:
                # 包装器与应用同组；超时或人工中断只终止本次测量的进程组。
                try: os.killpg(process.pid,signal.SIGKILL)
                except ProcessLookupError: pass
                process.wait()
                raise
        if code: raise RuntimeError(f'run-{index} exit={code}')
        raw=(folder/'memory.txt').read_text()
        rss=re.search(r'(\d+)\s+maximum resident set size',raw)
        footprint=re.search(r'(\d+)\s+peak memory footprint',raw)
        memory={'maximum_resident_set_size_bytes':int(rss[1]) if rss else None,
                'peak_memory_footprint_bytes':int(footprint[1]) if footprint else None,
                'method':'macOS /usr/bin/time -l; separate metrics, process lifetime including startup'}
        if not memory['maximum_resident_set_size_bytes'] or not memory['peak_memory_footprint_bytes']:
            raise ValueError('内存采集未得到正值，保留失败证据')
        summary=json.loads((folder/'summary.json').read_text())
        actual=summary['observed']
        if summary['status']!='completed' or actual['headless'] or actual['rendering_driver']!='metal':
            raise ValueError('并非完整 Metal 图形运行')
        calc=recompute(folder/'frames.csv')
        if calc['frames']!=summary['frames'] or abs(calc['duration_csv_s']-summary['duration_actual_s'])>1e-5:
            raise ValueError('CSV/summary 不一致')
        record={'exit_code':code,'memory':memory,'recomputed':calc,
                'launch':'Yudian --rendering-driver metal -- --benchmark --duration N --frames-csv <new-directory>/frames.csv --summary-json <new-directory>/summary.json',
                'environment':'DOTNET_ROOT and DOTNET_ROOT_ARM64 nonexistent; DOTNET_MULTILEVEL_LOOKUP=0; PATH retained',
                'app_binary_sha256':hashlib.sha256((a.app/'Contents/MacOS/Yudian').read_bytes()).hexdigest()}
        (folder/'verification.json').write_text(json.dumps(record,indent=2)+'\n')
        print(f'run-{index}: '+json.dumps({'p50_p95_p99_ms':calc['nearest_rank_ms'],'memory':memory}),flush=True)


if __name__=='__main__': main()
