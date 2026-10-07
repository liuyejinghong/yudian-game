"""Read-only explanation of the source identity check; no saves or exports."""
import bpy,json
from pathlib import Path
HERE=Path(__file__).resolve().parents[1]
script=HERE/'sanitize_delivery_metadata.py';namespace={'__file__':str(script),'__name__':'metadata_identity_helpers'}
exec(script.read_text().split("for asset,folder in [")[0],namespace)
snapshot=namespace['source_visual_identity'];results={};usage={}
for kind,path in [('before',HERE/'stages/metadata-before-portable-paths/mars-outcrop-hifi-r2.blend'),('after',HERE/'mars-outcrop-hifi-r2.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(path));results[kind]=snapshot()
    usage[kind]={m.name:{'users':m.users,'fake_user':m.use_fake_user,
        'objects':[o.name for o in bpy.data.objects if o.type=='MESH' and m.name in [slot.name for slot in o.data.materials if slot]]}
        for m in bpy.data.materials}
diff={}
for group,before in results['before'].items():
    after=results['after'][group]
    diff[group]={'missing':sorted(set(before)-set(after)),'added':sorted(set(after)-set(before)),
       'changed':{key:{'before':value,'after':after[key]} for key,value in before.items() if key in after and value!=after[key]}}
(HERE/'stages/metadata-source-diff.json').write_text(json.dumps(diff,indent=2))
(HERE/'stages/metadata-material-usage.json').write_text(json.dumps(usage,indent=2))
print('SOURCE_METADATA_DIFF='+json.dumps(diff),flush=True)
