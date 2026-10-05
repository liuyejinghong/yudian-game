"""Write manifests from imported source geometry, never from candidate design numbers."""
import hashlib,json
from prepare_preview import ROOT,HERE,NEW

STATES=['idle','move','work','charge','disabled','towed','maintenance']
IDS=dict(wangshan='ART-U03',repair='ART-F05',lander='ART-F06',zhulei='ART-U02',storage='ART-F03',charger='ART-F04',crate='ART-P01-CRATE',recovery='ART-P01-RECOVERY',**{'terrain-original':'ART-T-REF-ORIGINAL','terrain-flat':'ART-T-REF-FLAT','terrain-dug':'ART-T-REF-DUG'})
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run():
    out=ROOT/'art/manifests/lowfi-batch-r1';out.mkdir(parents=True,exist_ok=True)
    source_checks={r['asset']:r for r in json.loads((HERE/'source-checks.json').read_text())}
    for key in NEW:
        a=json.loads((HERE/(key+'-audit.json')).read_text())
        robot=key in ['zhulei','wangshan'];cargo=key in ['storage','lander']
        group='new' if key in ['wangshan','repair','lander'] else 'existing'
        source=ROOT/'art/source/lowfi-batch-r1'/group/key
        blends=list(source.glob('*.blend'));assert len(blends)==1,(key,blends)
        glb=ROOT/'prototype/assets/lowfi-batch-r1/models'/(key+'.glb')
        assert sha(glb)==a['glb_sha256']
        assert sha(source/(key+'.glb'))==sha(glb)
        bindings=[dict(node=m['node'],surface=s['surface'],role=s['role']) for m in a['meshes'] for s in m['surfaces']]
        triangles=sum(s['triangles'] for m in a['meshes'] if m['node'].startswith('Model/') for s in m['surfaces'])
        states={}
        for state in STATES:
            clip='disabled' if robot and state=='towed' else state
            exists=clip in a['clips'];moves=exists and any(c['clip']==clip and c['changed_targets'] for c in source_checks[key]['clips']);loop=exists and (state=='move' or state=='work' and key!='lander')
            mode='animated' if moves else 'static' if exists or state=='idle' or key=='storage' and state=='work' else 'na'
            if robot and state=='towed':mode='static'
            reason='Blender源局部动作；不计算游戏规则' if exists else '静态候选轮廓' if state=='idle' else '固定设施／道具或地表参考不适用该运行态'
            if key=='storage' and state=='work':reason='开放取放面；静态等待技术取放接口，不自动搬箱'
            if robot and state=='towed':reason='停机定格与独立候选杆，不批准拖救能力或载荷'
            states[state]=dict(mode=mode,reason=reason,clip=clip if exists else None,duration_s=a['clips'][clip]['duration_s'] if exists else 0,loop=loop)
        files=[blends[0],source/(key+'.glb')]
        generators=[source.parent/('build_samples.py' if group=='new' else 'build_'+('terrain' if key.startswith('terrain-') else key)+'.py')]
        assert generators[0].is_file()
        manifest=dict(id=IDS[key],revision=1,unit='meter',up='+Y',forward='-Z',preview_scene=f'res://assets/lowfi-batch-r1/{key}.tscn',glb=a['glb'],glb_sha256=a['glb_sha256'],bounds={'static':a['static'],'loaded':a['loaded'],'active':a['active_sampled']},bounds_scope='实际顶点与有限动作采样；不作为物理碰撞或导航包络认证',material_bindings=bindings,sockets=a['sockets'],states=states,phases=['completed'],cargo_modes=['empty','loaded'] if cargo else ['empty'],source={'source_path':str(blends[0].relative_to(ROOT)),'tool_version':'Blender 4.5.14 LTS','original':True,'inheritance':'现役原创模型继承及本批新增；具体见源报告','hash':sha(blends[0]),'generators':[str(p.relative_to(ROOT)) for p in generators],'files':[dict(path=str(p.relative_to(ROOT)),sha256=sha(p)) for p in files+generators]},triangles=triangles,triangles_with_candidate_attachment=a['triangles_with_candidate_attachment'],textures=[],source_clips=a['clips'],source_clip_changes=source_checks[key]['clips'],scope=a['scope'])
        if key.startswith('terrain-') or key=='recovery':manifest['scope']+='; standalone shape reference/candidate, formal terrain or towing contract BLOCKED'
        setting=glb.with_suffix('.glb.import')
        assert '"optimizer/enabled": false' in setting.read_text()
        manifest['godot_import']={'path':str(setting.relative_to(ROOT)),'sha256':sha(setting),'animation_player':'PATH:AnimationPlayer','optimizer_enabled':False,'reason':'保留源动作转折点；Godot实际重导入端点通过'}
        manifest['phase_state_modes']={'completed':{s:{'mode':v['mode'],'reason':v['reason']} for s,v in states.items()}}
        (out/(key+'.json')).write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
        print('MANIFEST',key,triangles,'triangles')
if __name__=='__main__':run()
