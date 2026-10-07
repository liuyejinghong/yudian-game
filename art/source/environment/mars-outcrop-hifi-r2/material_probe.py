"""512 proof that current Principled colour/roughness fields reach the real bake."""
import bpy,sys,json
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import pipeline as P
TEST=HERE/'stages/10-bake-test';bpy.ops.wm.open_mainfile(filepath=str(TEST/'prepared.blend'))
high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
projection=json.loads((TEST/'test.json').read_text())['projection'];images={};record={}
for variant in ('attribute_baseline','complete_material'):
    if variant=='complete_material':P.geologic_finish(high)
    for role in ('base_color','roughness'):
        name=variant+'-'+role;im=P.new_image(name,512,role)
        P.bake_pair(high,low,im,role,projection);P.save_map(im,TEST/(name+'.png'));images[name]=im
mask=P.U.island_mask(low,512).astype(bool)
for role in ('base_color','roughness'):
    values=[]
    for variant in ('attribute_baseline','complete_material'):
        im=images[variant+'-'+role];a=np.empty(512*512*4,np.float32);im.pixels.foreach_get(a)
        values.append(a.reshape(512,512,4)[mask,:3])
    delta=values[1]-values[0]
    record[role]={'pixels':len(delta),'mean_absolute_difference':float(np.abs(delta).mean()),
        'maximum_absolute_difference':float(np.abs(delta).max()),'changed_pixel_fraction':float((np.max(np.abs(delta),axis=1)>1e-4).mean())}
    assert record[role]['mean_absolute_difference']>1e-4
record['method']='Actual selected-to-active EMIT baking of current Principled input from_socket; same UV/projection, before and after geologic_finish; Surface link restored after each bake.'
P.write_json(TEST/'material-input-proof.json',record);print('RESULT_JSON='+json.dumps(record))
