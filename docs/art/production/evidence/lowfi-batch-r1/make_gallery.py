"""Build a local viewer from actual Godot screenshot records."""
import json
from pathlib import Path
HERE=Path(__file__).resolve().parent
NAMES={'tuoyun':'驮运 U01','zhulei':'筑垒 U02','wangshan':'望山 U03','solar':'太阳能 F01','processor':'加工 F02','storage':'仓储 F03','charger':'充电 F04','repair':'维修 F05','lander':'着陆器 F06','crate':'货箱','recovery':'回收杆候选','terrain-original':'原始地表参考','terrain-flat':'整平地表参考','terrain-dug':'开挖地表参考'}
rows=[]
for path in sorted((HERE/'captures').glob('*/captures.json')):
    for row in json.loads(path.read_text()):
        row['url']=str((path.parent/row['file']).relative_to(HERE));rows.append(row)
expected=sum(len(r['views']) for r in json.loads((HERE/'capture-plan.json').read_text()))
assert len(rows)==expected and len({r['url'] for r in rows})==expected
payload=json.dumps(rows,ensure_ascii=False).replace('</','<\\/')
names=json.dumps(NAMES,ensure_ascii=False)
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>余电 · 低保真整批看样</title>
<style>body{margin:0;background:#edf0ef;color:#263438;font:16px/1.6 system-ui,sans-serif}main{max-width:1250px;margin:auto;padding:32px}h1{font-size:32px;margin:0}p{max-width:950px}small{color:#58676b}img{display:block;width:100%;height:auto}a{color:#365f72}select{font:inherit;padding:8px;max-width:100%;margin:8px 12px 8px 0}figure{margin:20px 0;background:#fff}figcaption{padding:12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}.meta{padding:12px;background:white}footer{margin-top:32px} @media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<main><small>余电 / 美术看样 / 2026-10-05</small><h1>机器人与设施低保真模型看样</h1>
<p>三类机器人、六类设施、货箱与回收杆候选，以及三版地表造型参考。地表贴图、环境地标和铁／铜资源素材尚未制作；本页不是完整游戏素材库。以下均为 Godot 4.7.2 / Metal 实机截图；统一灯光、材质与固定镜头。所有者最终视觉接受尚未进行。</p>
<figure><a href="relation/terrain-original_normal.png"><img src="relation/terrain-original_normal.png" alt="正常指挥镜头下的独立基地关系样板"></a><figcaption>正常指挥镜头 · 独立摆放样板。维修、充电、拖救和货物都是美术示意，未接正式游戏规则。</figcaption></figure>
<h2>逐件与状态对照</h2><label>对象 <select id="asset"></select></label><label>姿态 / 载荷 / 时间 <select id="pose"></select></label><label>正常镜头方向 <select id="yaw"></select></label>
<div class="pair"><figure><a id="normalLink"><img id="normal" alt="正常指挥距离"></a><figcaption id="normalCaption">正常指挥距离；保留完整原图与实际屏幕占比。</figcaption></figure><figure><a id="closeLink"><img id="close" alt="固定近景"></a><figcaption id="closeCaption">固定近景；大设施可能裁切，完整轮廓以正常镜头核对。</figcaption></figure></div><div class="meta" id="meta"></div>
<h2>地表前后</h2><div class="pair"><figure><a href="relation/terrain-flat_normal.png"><img src="relation/terrain-flat_normal.png" alt="整平后参考"></a><figcaption>整平参考：左侧土坡被移除，矿点造型保留。</figcaption></figure><figure><a href="relation/terrain-dug_normal.png"><img src="relation/terrain-dug_normal.png" alt="开挖后参考"></a><figcaption>开挖参考：矿点附近变成低洼。三版均非正式 T3 数据或矿物结算。</figcaption></figure></div>
<p>本轮确认了用途轮廓、空有载和主要开闭姿态。小接口、盖板及细微扫描动作在正常距离下仍有限；整场状态提示与性能需正式接入后另验。高保真、UV、烘焙和 Astra 制作尚未启动。</p>
<footer><a href="README.md">交付与验证记录</a> · <a href="technical-handoff.md">技术接入需求</a></footer></main>
<script>
const rows=ROWS,names=NAMES,asset=document.querySelector('#asset'),pose=document.querySelector('#pose'),yaw=document.querySelector('#yaw');
function options(select,values,label){select.replaceChildren(...values.map(value=>{const option=document.createElement('option');option.value=value;option.textContent=label(value);return option}));}
options(asset,Object.keys(names),v=>names[v]);
function poses(){options(pose,[...new Set(rows.filter(r=>r.asset===asset.value).map(r=>r.label))],v=>v);directions();}
function directions(){options(yaw,[...new Set(rows.filter(r=>r.asset===asset.value&&r.label===pose.value&&r.camera==='normal').map(r=>r.yaw))],v=>v+'°');show();}
function show(){const group=rows.filter(r=>r.asset===asset.value&&r.label===pose.value);for(const mode of ['normal','close']){const row=group.find(r=>r.camera===mode&&r.yaw===Number(yaw.value))||group.find(r=>r.camera===mode);document.querySelector('#'+mode).src=row.url;document.querySelector('#'+mode+'Link').href=row.url;document.querySelector('#'+mode+'Caption').textContent=(mode==='normal'?'正常指挥距离':'固定近景')+' / 实际方向 '+row.yaw+'°'+(mode==='close'?'；大设施可能裁切，完整轮廓以正常镜头核对。':'；保留完整原图与实际屏幕占比。');}const p=group[0].preset;document.querySelector('#meta').textContent=names[asset.value]+' · '+p.state+' · '+p.cargo+' · '+p.time_s+' s · '+p.phase+' · 原图链接可查看 1920×1200 像素；该方向无近景时显示已有近景，实际角度见图下注记。';}
asset.onchange=poses;pose.onchange=directions;yaw.onchange=show;poses();
</script></html>'''.replace('ROWS',payload).replace('NAMES',names)
(HERE/'gallery.html').write_text(page)
print('GALLERY_OK',len(rows),'actual screenshots')
