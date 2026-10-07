import bpy,json,struct,zlib
from pathlib import Path
p=Path(__file__).resolve().parent
def png_first(path):
 raw=path.read_bytes();off=8;data=b'';meta=None
 while off<len(raw):
  n,kind=struct.unpack_from('>I4s',raw,off);chunk=raw[off+8:off+8+n];off+=12+n
  if kind==b'IHDR':meta=struct.unpack('>IIBBBBB',chunk)
  if kind==b'IDAT':data+=chunk
 row=zlib.decompress(data);depth=meta[2];channels={2:3,6:4}[meta[3]];size=depth//8
 assert row[0] in (0,1,2,3,4)
 # First pixel of first row has zero left/above neighbours for every PNG filter.
 values=[int.from_bytes(row[1+i*size:1+(i+1)*size],'big')/(2**depth-1) for i in range(channels)]
 return {'bits':depth,'encoded_rgb':values[:3]}
def encode(v):return 12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055
linear=(.22,.195,.171);encoded=tuple(encode(v) for v in linear);result=[]
for floating in (False,True):
 for label,value in [('raw_linear',linear),('encoded_srgb',encoded)]:
  im=bpy.data.images.new(str(floating)+label,width=4,height=4,float_buffer=floating,alpha=False);im.colorspace_settings.name='sRGB'
  im.pixels[:]=list((*value,1))*16;im.update();path=p/(str(floating)+'-'+label+'.png');im.filepath_raw=str(path);im.file_format='PNG';im.save()
  test=png_first(path);test.update(float_buffer=floating,input_kind=label,input=list(value));result.append(test)
for row in result:
 if row['input_kind']=='encoded_srgb':assert max(abs(a-b) for a,b in zip(row['encoded_rgb'],encoded))<.004,row
report={'status':'PASS','desired_linear':linear,'required_encoded_pixel_input':encoded,'results':result}
(p/'color-roundtrip.json').write_text(json.dumps(report,indent=2)+'\n');print('RESULT_JSON='+json.dumps(report))
