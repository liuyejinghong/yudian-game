extends SceneTree
const STATES = ["idle", "move", "work", "charge", "disabled", "towed", "maintenance"]
const ROLES = ["body_light", "frame_dark", "accent_warm", "rubber", "solar_face", "soil_mars"]
const LIMITS={
	"wangshan":[[-.65,0,-1.15],[.65,1.85,.84]],
	"zhulei":[[-.9,0,-1.6],[.9,1.52,.84]],
	"repair":[[-1.85,0,-1.9],[1.7,2.32,1.6]],
	"lander":[[-1.65,0,-2.02],[1.65,2.60,1.65]],
	"storage":[[-2.25,0,-3.02],[2.25,2.20,2.35]],
	"charger":[[-.9,0,-1.4],[1.85,1.74,1.1]],
	"crate":[[-.32,0,-.35],[.32,.38,.35]],
	"recovery":[[-.1,-.04,-.175],[.1,.04,.175]],
	"terrain-original":[[-4.5,0,-4.5],[4.5,1.8,4.5]],
	"terrain-flat":[[-4.5,0,-4.5],[4.5,1.8,4.5]],
	"terrain-dug":[[-4.5,0,-4.5],[4.5,1.8,4.5]],
}
const SOCKETS={
	"wangshan":{"TowFront":[[0,.24,-.8],[0,0,-1]],"TowRear":[[0,.24,.8],[0,0,1]],"Charge":[[.48,.55,-.32],[1,0,0]],"Service":[[0,.75,.65],[0,0,1]],"Sensor":[]},
	"zhulei":{"TowFront":[[0,.24,-.8],[0,0,-1]],"TowRear":[[0,.24,.8],[0,0,1]],"Charge":[[-.48,.55,.34],[-1,0,0]],"Service":[[0,.69,.695],[0,0,1]],"Work":[]},
	"repair":{"Bay":[[0,.04,0],[0,0,-1]],"PowerIn":[[-1.6,.18,1.255],[0,0,1]],"Service":[[-1.385,.9,.9],[1,0,0]]},
	"lander":{"Cargo":[[0,.86,-.83],[0,0,-1]],"Service":[[0,1.3,1.1125],[0,0,1]],"Handling":[[0,.7,1],[0,0,1]]},
	"storage":{"Input":[[0,.2,-1.6],[0,0,-1]],"Output":[[0,.2,1.78],[0,0,1]],"Service":[[0,.85,1.7425],[0,0,1]]},
	"charger":{"Dock":[[.65,.57,-.35],[-1,0,0]],"PowerIn":[[1.055,.18,-.66],[0,0,-1]]},
	"recovery":{"EndA":[[0,0,-.175],[0,0,-1]],"EndB":[[0,0,.175],[0,0,1]]},
	"crate":{},"terrain-original":{},"terrain-flat":{},"terrain-dug":{},
}
func vector(a: Array)->Vector3:return Vector3(a[0],a[1],a[2])
func check_sockets(key: String,base: Dictionary,sample: Node3D) -> bool:
	var expected: Dictionary=SOCKETS[key];var found: Array=[]
	for row in base.sockets:
		var name: String=row.node.get_file().trim_prefix("Socket_")
		assert(expected.has(name) and name not in found,"unexpected/duplicate socket "+name);found.append(name)
		if not expected[name].is_empty():
			assert(vector(row.position).distance_to(vector(expected[name][0]))<.0001,"socket position "+name)
			assert(vector(row.forward).distance_to(vector(expected[name][1]))<.0001 and vector(row.up).distance_to(Vector3.UP)<.0001,"socket direction "+name)
		else:
			var n=sample.get_node(row.node)
			assert(n.position.distance_to(Vector3(0,0,-.245 if name=="Sensor" else -.16))<.0001,"dynamic socket local position "+name)
			assert((-n.basis.z).distance_to(Vector3.FORWARD)<.0001 and n.basis.y.distance_to(Vector3.UP)<.0001,"dynamic socket local direction "+name)
	assert(found.size()==expected.size(),"missing frozen socket "+key)
	return true
var rows: Array = []
func _initialize(): call_deferred("run")
static func v3(v: Vector3) -> Array: return [v.x, v.y, v.z]
func preset(state: String, cargo: String, time: float) -> Dictionary:
	return {"state":state, "phase":"completed", "phase_t":0.0, "cargo":cargo, "reason":"none", "time_s":time}
func pose(node: Node3D) -> Dictionary:
	var out: Dictionary = {".":[node.transform,node.visible]}
	for n in node.find_children("*", "Node3D", true, false): out[str(node.get_path_to(n))] = [n.transform,n.visible]
	return out
func same(a: Dictionary, b: Dictionary) -> bool:
	if a.keys() != b.keys(): return false
	for key in a:
		if not a[key][0].is_equal_approx(b[key][0]) or a[key][1] != b[key][1]: return false
	return true
static func bounds(node: Node3D,only: String="") -> Dictionary:
	var lo = Vector3(INF,INF,INF); var hi = -lo
	for m in node.find_children("*", "MeshInstance3D", true, false):
		if not m.is_visible_in_tree() or only != "" and m.name != only: continue
		var xf: Transform3D = node.global_transform.affine_inverse()*m.global_transform
		for i in m.mesh.get_surface_count():
			for vertex in m.mesh.surface_get_arrays(i)[Mesh.ARRAY_VERTEX]:
				var p: Vector3 = xf*vertex
				assert(is_finite(p.x) and is_finite(p.y) and is_finite(p.z))
				lo=lo.min(p);hi=hi.max(p)
	assert(is_finite(lo.x))
	return {"min":v3(lo),"max":v3(hi)}
func audit(node: Node3D) -> Dictionary:
	var meshes: Array=[];var sockets: Array=[];var clips: Dictionary={};var triangles=0
	for m in node.find_children("*", "MeshInstance3D", true, false):
		assert(m.material_override == null and m.material_overlay == null)
		var surfaces: Array=[]
		for i in m.mesh.get_surface_count():
			var role: String=m.mesh.surface_get_material(i).resource_name
			assert(role in ROLES,"unknown material role "+role)
			var arrays=m.mesh.surface_get_arrays(i)
			var count=arrays[Mesh.ARRAY_INDEX].size() if arrays[Mesh.ARRAY_INDEX] != null and not arrays[Mesh.ARRAY_INDEX].is_empty() else arrays[Mesh.ARRAY_VERTEX].size()
			triangles+=count/3
			surfaces.append({"surface":i,"role":role,"triangles":count/3})
		meshes.append({"node":str(node.get_path_to(m)),"surfaces":surfaces})
	for n in node.find_children("Socket_*", "Node3D", true, false):
		# Candidate attachment's markers are separate from the actual Model contract.
		if str(node.get_path_to(n)).begins_with("TowDemo/"): continue
		var xf: Transform3D=node.global_transform.affine_inverse()*n.global_transform
		sockets.append({"node":str(node.get_path_to(n)),"position":v3(xf.origin),"forward":v3(-xf.basis.z.normalized()),"up":v3(xf.basis.y.normalized())})
	for player in node.get_node("Model").find_children("*", "AnimationPlayer", true, false):
		for name in player.get_animation_list():
			if name == "RESET": continue
			var animation=player.get_animation(name);var tracks: Array=[]
			for t in animation.get_track_count():tracks.append(str(animation.track_get_path(t)))
			clips[name]={"duration_s":animation.length,"tracks":tracks}
	return {"meshes":meshes,"sockets":sockets,"clips":clips,"triangles_with_candidate_attachment":triangles}
func part(sample: Node3D,name: String)->Node3D:
	var found=sample.get_node("Model").find_children(name,"Node3D",true,false)
	assert(found.size()==1,"one named source part required "+name)
	return found[0]
func rotation(sample: Node3D,name: String,degrees: Vector3) -> bool:
	var expected=Basis.from_euler(degrees*PI/180.0)
	assert(part(sample,name).basis.is_equal_approx(expected),"source pose target "+name+str(degrees)+str(part(sample,name).rotation_degrees))
	return true
func size_check(sample: Node3D,name: String,expected: Vector3) -> bool:
	assert(part(sample,name)!=null)
	var box=bounds(sample,name)
	assert((vector(box.max)-vector(box.min)).distance_to(expected)<.0001,"frozen shape size "+name+str(box))
	return true
func check_sizes(key: String,sample: Node3D) -> bool:
	match key:
		"wangshan":
			assert(size_check(sample,"BodyShell",Vector3(.90,.22,1.22)))
			assert(size_check(sample,"FrameBed",Vector3(.90,.14,1.25)))
		"repair":assert(size_check(sample,"Foundation",Vector3(3.4,.04,3.2)))
		"lander":assert(size_check(sample,"CoreBus",Vector3(1.60,1.60,1.40)))
		"crate":
			var box=bounds(sample)
			assert((vector(box.max)-vector(box.min)).distance_to(Vector3(.64,.38,.70))<.0001,"crate frozen dimensions")
	return true
func check_poses(key: String,sample: Node3D) -> bool:
	for state in ["idle","charge","disabled","maintenance","work"]:
		assert(sample.apply_preview(preset(state,"empty",1.0))=="")
		match key:
			"wangshan":
				assert(rotation(sample,"SensorYaw",Vector3(0,35 if state=="work" else 0,0)))
				assert(rotation(sample,"SensorPitch",Vector3(-35 if state=="disabled" else 0,0,0)))
				assert(rotation(sample,"ChargeCap",Vector3(0,0,60 if state=="charge" else 0)))
				assert(rotation(sample,"ServiceLid",Vector3(70 if state=="maintenance" else 0,0,0)))
			"zhulei":
				assert(rotation(sample,"Shoulder",Vector3(-20 if state=="work" else 85 if state=="disabled" else 55,0,0)))
				assert(rotation(sample,"Elbow",Vector3(5 if state=="work" else -155,0,0)))
				assert(rotation(sample,"ChargePivot",Vector3(0,0,-60 if state=="charge" else 0)))
				assert(rotation(sample,"HoodLid",Vector3(70 if state=="maintenance" else 0,0,0)))
				for name in ["SupportSlideL","SupportSlideR"]:
					var y=sample.global_transform.affine_inverse()*part(sample,name).global_position
					assert(absf(y.y-(.03 if state=="work" else .30))<.0001,"foot target "+name)
			"repair":
				assert(rotation(sample,"StopGate",Vector3(0 if state=="disabled" else -90,0,0)))
				assert(rotation(sample,"ServiceCover",Vector3(0,0,70 if state=="maintenance" else 0)))
				var position=sample.global_transform.affine_inverse()*part(sample,"ToolSlide").global_position
				assert(position.distance_to(Vector3(.85 if state=="work" else 1.05,1.60,-.35))<.0001,"tool slide target "+state+str(position))
			"storage":
				assert(rotation(sample,"GatePivot",Vector3(0 if state=="disabled" else 90,0,0)))
				assert(rotation(sample,"CoverPivot",Vector3(-70 if state=="maintenance" else 0,0,0)))
			"charger":
				assert(rotation(sample,"StatusPaddlePivot",Vector3(0 if state=="work" else -90,0,0)))
				assert(rotation(sample,"StopGatePivot",Vector3(0 if state=="disabled" else -90,0,0)))
				assert(rotation(sample,"ServicePanelPivot",Vector3(0,0,70 if state=="maintenance" else 0)))
			"lander":
				assert(rotation(sample,"CargoLid",Vector3(90 if state=="work" else 0,0,0)))
				assert(rotation(sample,"RearServiceLid",Vector3(-70 if state=="maintenance" else 0,0,0)))
				assert(rotation(sample,"StopPlate",Vector3(0 if state=="disabled" else -90,0,0)))
	if key=="charger":
		for time in [.5,1.5]:
			assert(sample.apply_preview(preset("work","empty",time))=="")
			assert(rotation(sample,"StatusPaddlePivot",Vector3(15 if time==.5 else -15,0,0)))
	assert(sample.apply_preview(preset("idle","empty",0))=="")
	return true
func run():
	var args=OS.get_cmdline_user_args();assert(args.size()==3)
	var key=args[0];var scene=load(args[1]) as PackedScene;assert(scene != null)
	var sample=scene.instantiate() as Node3D;root.add_child(sample)
	assert(sample.apply_preview(preset("idle","empty",0.0)) == "")
	var base=audit(sample);var static_box=bounds(sample)
	if SOCKETS.has(key):assert(check_sockets(key,base,sample))
	assert(check_sizes(key,sample))
	if key in ["wangshan","zhulei","repair","lander","storage","charger"]:assert(check_poses(key,sample))
	var cargo_modes=["empty","loaded"] if key in ["storage","lander","tuoyun"] else ["empty"]
	var active_lo=Vector3(INF,INF,INF);var active_hi=-active_lo
	for cargo in cargo_modes:
		for state in STATES:
			for time in [0.0,.25,.5,.75,1.0,1.5,2.0]:
				var p=preset(state,cargo,time)
				assert(sample.apply_preview(p) == "",str(p))
				if key in ["storage","lander"]:
					var payload=sample.get_node("Model").find_child("RackPayloadDemo" if key=="storage" else "LanderPayloadDemo",true,false)
					var crates=payload.find_children("*","MeshInstance3D",true,false)
					assert(crates.size()==(12 if key=="storage" else 1),"source payload count "+key)
					for crate in crates:assert(crate.is_visible_in_tree()==(cargo=="loaded"),"payload visibility "+key)
				var expected=pose(sample);var box=bounds(sample)
				assert(sample.transform.is_equal_approx(Transform3D.IDENTITY))
				assert(sample.get_node("Model").transform.is_equal_approx(Transform3D.IDENTITY))
				var minimum=Vector3(box.min[0],box.min[1],box.min[2]);var maximum=Vector3(box.max[0],box.max[1],box.max[2])
				assert(minimum.y >= (-.040001 if key == "recovery" else -.0001),"below ground "+key+str(p))
				if LIMITS.has(key):
					assert(minimum.x>=LIMITS[key][0][0]-.0001 and minimum.z>=LIMITS[key][0][2]-.0001 and maximum.x<=LIMITS[key][1][0]+.0001 and maximum.y<=LIMITS[key][1][1]+.0001 and maximum.z<=LIMITS[key][1][2]+.0001,"frozen envelope conflict "+key+str(p)+str(box))
				active_lo=active_lo.min(minimum);active_hi=active_hi.max(maximum)
				# Compare a history-driven pose to a genuinely fresh source instance.
				var fresh=scene.instantiate() as Node3D;root.add_child(fresh)
				assert(fresh.apply_preview(p) == "" and same(expected,pose(fresh)),"state residue "+str(p));fresh.free()
				assert(sample.apply_preview(p) == "" and same(expected,pose(sample)),"absolute time accumulated")
				rows.append({"state":state,"cargo":cargo,"time_s":time,"bounds":box})
	assert(sample.apply_preview(preset("idle",cargo_modes[-1],0.0)) == "")
	var loaded_box=bounds(sample)
	var prior=pose(sample);var invalid: Array=[]
	var bad=preset("offline","empty",1.0);invalid.append(bad)
	bad=preset("idle","empty",1.0);bad.erase("time_s");invalid.append(bad)
	bad=preset("idle","empty",1.0);bad.extra=1;invalid.append(bad)
	bad=preset("idle","empty",NAN);invalid.append(bad)
	bad=preset("idle","empty",-1.0);invalid.append(bad)
	bad=preset("idle","empty",1.0);bad.phase_t=1.1;invalid.append(bad)
	bad=preset("idle","empty",1.0);bad.state=1;invalid.append(bad)
	bad=preset("idle","bogus",1.0);invalid.append(bad)
	bad=preset("idle","empty",1.0);bad.reason="mechanical";invalid.append(bad)
	bad=preset("idle","empty",1.0);bad.reason="return_charge";invalid.append(bad)
	for p in invalid:assert(sample.apply_preview(p) != "" and same(prior,pose(sample)),"invalid input mutated pose")
	var source_failures=0
	if key in ["wangshan","zhulei","repair","lander","storage","charger"]:
		for fault in ["missing_model","missing_clip"]:
			var broken=scene.instantiate() as Node3D;root.add_child(broken)
			assert(broken.apply_preview(preset("maintenance","empty",1))=="")
			if fault=="missing_model":broken.get_node("Model").name="UnavailableModel"
			else:
				var player=broken.get_node("Model").find_children("*","AnimationPlayer",true,false)[0]
				player.remove_animation_library("")
			var before=pose(broken)
			assert(broken.apply_preview(preset("idle","empty",0))!="" and same(before,pose(broken)),"missing source fallback "+fault)
			broken.free();source_failures+=1
	var changes: Dictionary={}
	for state in ["move","work"]:
		if not base.clips.has(state):continue
		var times=[.5,1.5] if key=="charger" and state=="work" else [.25,.75]
		assert(sample.apply_preview(preset(state,"empty",times[0])) == "")
		var first=pose(sample)
		assert(sample.apply_preview(preset(state,"empty",times[1])) == "")
		assert(not same(first,pose(sample)),"source clip has no sampled motion "+key+"/"+state)
		changes[state]={"times":times,"different":true}
	base.merge({"source_endpoint_check":"PASS" if key in ["wangshan","zhulei","repair","lander","storage","charger"] else "na: static prop/reference", "frozen_socket_check":"PASS","candidate_envelope_check":"PASS","asset":key,"glb":sample.get_node("Model").scene_file_path,"glb_sha256":FileAccess.get_sha256(sample.get_node("Model").scene_file_path),"engine":Engine.get_version_info(),"static":static_box,"loaded":loaded_box,"active_sampled":{"min":v3(active_lo),"max":v3(active_hi)},"pose_cases":rows,"illegal_preserved":invalid.size(),"source_faults_preserved":source_failures,"motion_comparison":changes,"scope":"pure source-animation preview and candidate attachment; formal gameplay/owner acceptance NOT_RUN"})
	var file=FileAccess.open(args[2],FileAccess.WRITE);assert(file != null)
	file.store_string(JSON.stringify(base,"  "));file.close()
	print("LF_STATE_OK ",key," cases=",rows.size()," illegal=",invalid.size());quit(0)
