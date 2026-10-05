extends SceneTree
const Validator=preload("res://validate.gd")
var records: Array=[]
func _initialize():call_deferred("run")
func preset(state: String,time: float,cargo="empty")->Dictionary:
	return {"state":state,"phase":"completed","phase_t":0,"cargo":cargo,"reason":"none","time_s":time}
func sample(key: String)->Node3D:
	var path="res://assets/units/tuoyun-r1/preview.tscn" if key=="tuoyun" else "res://assets/lowfi-batch-r1/"+key+".tscn"
	var n=load(path).instantiate() as Node3D;root.add_child(n);return n
func box(n: Node3D,name: String="",space: Node3D=null)->Dictionary:
	if space==null:return Validator.bounds(n,name)
	var found=n.find_children(name,"MeshInstance3D",true,false);assert(found.size()==1)
	var mesh=found[0];var xf=space.global_transform.affine_inverse()*mesh.global_transform
	var low=Vector3(INF,INF,INF);var high=-low
	for i in mesh.mesh.get_surface_count():
		for vertex in mesh.mesh.surface_get_arrays(i)[Mesh.ARRAY_VERTEX]:
			var point=xf*vertex;low=low.min(point);high=high.max(point)
	return {"min":[low.x,low.y,low.z],"max":[high.x,high.y,high.z]}
func gap(a: Dictionary,b: Dictionary)->float:
	var best=-INF
	for i in 3:best=maxf(best,maxf(a.min[i]-b.max[i],b.min[i]-a.max[i]))
	return best
func separated(label: String,a: Dictionary,b: Dictionary)->bool:
	var distance=gap(a,b);assert(distance>0,"selected non-joint overlap "+label+str(distance))
	records.append({"case":label,"a":a,"b":b,"separating_axis_gap_m":distance});return true
func run():
	var head=sample("wangshan")
	for state in ["work","disabled"]:
		for t in [0.0,.25,.5,.75,1.0]:
			assert(head.apply_preview(preset(state,t))=="")
			for name in ["HeadForkBridge","HeadForkPostL","HeadForkPostR"]:
				assert(separated("U03 "+state+str(t)+" Head/"+name,box(head,"SensorHead",head.find_child("SensorYaw",true,false)),box(head,name,head.find_child("SensorYaw",true,false))))
	var rack=sample("storage")
	for t in [0.0,.25,.5,.75,1.0]:
		assert(rack.apply_preview(preset("disabled",t,"loaded"))=="")
		var crates=rack.get_node("Model").find_children("Crate_*","MeshInstance3D",true,false)
		assert(crates.size()==12)
		for crate in crates:assert(separated("F03 gate/payload "+str(t)+crate.name,box(rack,"GatePanel"),box(rack,crate.name)))
	var lander=sample("lander")
	assert(lander.apply_preview(preset("work",1,"loaded"))=="")
	var payload=lander.get_node("Model").find_child("LanderPayloadDemo",true,false).find_children("*","MeshInstance3D",true,false)
	assert(payload.size()==1)
	assert(separated("F06 payload/CoreBus",box(lander,payload[0].name),box(lander,"CoreBus")))
	var repair=sample("repair");var rover=sample("tuoyun")
	assert(rover.apply_preview(preset("maintenance",1))=="")
	for t in [0.0,.25,.5,.75,1.0,1.5,2.0]:
		assert(repair.apply_preview(preset("work",t))=="")
		assert(separated("F05 ToolBlock/U01 "+str(t),box(repair,"ToolBlock"),box(rover)))
	var charger=sample("charger")
	assert(rover.apply_preview(preset("charge",1))=="")
	for t in [0.0,.5,1.0,1.5,2.0]:
		assert(charger.apply_preview(preset("work",t))=="")
		for name in ["StatusPaddlePanel","StopGatePanel"]:
			assert(separated("F04 "+name+"/U01 "+str(t),box(charger,name),box(rover)))
	var file=FileAccess.open(OS.get_cmdline_user_args()[0],FileAccess.WRITE);assert(file!=null)
	file.store_string(JSON.stringify({"scope":"finite source poses and selected non-joint actual-vertex boxes; not collision or continuous-motion certification","records":records},"  "));file.close()
	print("LF_CLEARANCE_OK cases=",records.size());quit(0)
