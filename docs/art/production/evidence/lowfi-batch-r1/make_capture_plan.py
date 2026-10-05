"""Fixed views and explicit source-animation poses, no per-asset camera fitting."""
import json
from pathlib import Path
from prepare_preview import HERE,PROJECT,NEW,OLD

def preset(state='idle',cargo='empty',time=0,phase='completed',phase_t=0,reason='none'):
    return dict(state=state,phase=phase,phase_t=phase_t,cargo=cargo,reason=reason,time_s=time)
def run():
    rows=[]
    def add(key,label,p,baseline=False):
        scene='res://assets/lowfi-batch-r1/'+key+'.tscn' if key in NEW else 'res://assets/%s/%s/preview.tscn'%OLD[key]
        views=[dict(camera='normal',yaw=y) for y in [0,90,180]]
        views += [dict(camera='close',yaw=y) for y in ([0,90,180] if baseline else [90,180])]
        scope='Godot source-animation art preview; gameplay/owner acceptance NOT_RUN'
        if key.startswith('terrain-'):scope='Godot standalone terrain shape reference; not T3 data/system, no mining/save rules'
        if key=='recovery' or p['state']=='towed':scope+='; recovery attachment is visual candidate, no approved towing capacity/ability'
        rows.append(dict(asset=key,label=label,scene=scene,preset=p,views=views,scope=scope))
    for key in NEW:
        add(key,'idle_empty',preset(),True)
        if key in ['zhulei','wangshan']:
            for s in ['move','work']:
                for t in [.25,.75]:add(key,s+'_'+str(t),preset(s,time=t))
            for s in ['charge','disabled','towed','maintenance']:
                add(key,s,preset(s,time=1,reason='both' if s=='disabled' else 'none'))
        elif key in ['repair','charger']:
            for t in ([.5,1.5] if key=='charger' else [.25,.75]):add(key,'work_'+str(t),preset('work',time=t))
            for s in ['disabled','maintenance']:add(key,s,preset(s,time=1))
        elif key=='storage':
            add(key,'idle_loaded',preset(cargo='loaded'))
            add(key,'disabled_loaded',preset('disabled','loaded',1))
            add(key,'maintenance_loaded',preset('maintenance','loaded',1))
        elif key=='lander':
            for cargo in ['empty','loaded']:add(key,'work_'+cargo,preset('work',cargo,1))
            add(key,'disabled',preset('disabled',time=1))
            add(key,'maintenance',preset('maintenance',time=1))
    for key in OLD:
        add(key,'idle_empty',preset(),True)
        if key=='tuoyun':
            add(key,'idle_loaded',preset(cargo='loaded'))
            add(key,'maintenance',preset('maintenance',time=1))
        elif key=='solar':
            for phase,t in [('packed',0),('installed',0),('deploying',.5)]:
                add(key,phase,preset(phase=phase,phase_t=t))
            add(key,'work',preset('work',time=.25))
        else:
            for s in ['work','disabled','maintenance']:add(key,s,preset(s,time=.5 if s=='work' else 1))
    text=json.dumps(rows,indent=2)+'\n'
    (HERE/'capture-plan.json').write_text(text);(PROJECT/'capture-plan.json').write_text(text)
    (PROJECT/'capture.tscn').write_text('[gd_scene load_steps=2 format=3]\n[ext_resource type="Script" path="res://capture.gd" id="1"]\n[node name="LowFiBatchCapture" type="Node"]\nscript=ExtResource("1")\n')
    (PROJECT/'relation.tscn').write_text('[gd_scene load_steps=2 format=3]\n[ext_resource type="Script" path="res://relation.gd" id="1"]\n[node name="LowFiBatchRelation" type="Node"]\nscript=ExtResource("1")\n')
    print('CAPTURE_PLAN',len(rows),'poses',sum(len(r['views']) for r in rows),'images')
if __name__=='__main__':run()
