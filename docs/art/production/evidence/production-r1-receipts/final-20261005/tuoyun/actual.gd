extends SceneTree
func _initialize(): call_deferred("run")
func snapshot(w: Node) -> Array:
	var result: Array = []
	for n in w.find_children("*", "Node3D", true, false):
		result.append([n.transform, n.visible])
	return result
func ext_json(x: Dictionary) -> Dictionary:
	return {"min":[x.min.x,x.min.y,x.min.z],"max":[x.max.x,x.max.y,x.max.z]}
func run():
	var host=load("res://independent/host.gd").new()
	root.add_child(host);host._build_scene_scaffold({})
	var loaded=host._load_manifest("res://manifest.json")
	if not loaded.ok:printerr(loaded.error);quit(1);return
	var d:Dictionary=loaded.data
	var inst=host._instantiate_sample(d)
	if not inst.ok:printerr(inst.error);quit(1);return
	var w=inst.wrapper
	var bind=host._bind_materials(w,d,{})
	if bind!="":printerr(bind);quit(1);return
	var report:Dictionary={"cases":0,"phases":{},"negative_hold":0,"failures":[]}
	var low=Vector3(INF,INF,INF);var high=Vector3(-INF,-INF,-INF)
	for phase in d.phases:
		for pt in ([0.,.25,.5,.75,1.] if phase=="deploying" else [0.]):
			for state in d.states:
				for cargo in d.cargo_modes:
					for t in [0.,.25,.5,.75,1.,1.5,2.25]:
						var p={"state":state,"phase":phase,"phase_t":pt,"cargo":cargo,"reason":"none","time_s":t}
						var a=host._apply_preset(w,p,d)
						if a.error!="":printerr(a.error);quit(1);return
						var before=snapshot(w)
						w.apply_preview(p)
						if before!=snapshot(w):report.failures.append("repeat changes pose "+str(p))
						var ext=host._visible_vertex_extents(w)
						low=low.min(ext.min);high=high.max(ext.max)
						var error=host._check_active_containment(w,d)
						if error!="":report.failures.append(error+str(p))
						if not w.transform.is_equal_approx(Transform3D.IDENTITY):report.failures.append("root moved")
						if state=="idle" and t==0:
							report.phases[phase+str(pt)]=ext_json(ext)
						report.cases+=1
	var legal={"state":"maintenance","phase":"completed","phase_t":0.,"cargo":"empty","reason":"none","time_s":1.}
	w.apply_preview(legal)
	var held=snapshot(w)
	var bads:Array=[]
	for pair in [["state","offline"],["phase","bad"],["cargo","bad"],["reason","return_charge"],["time_s",NAN],["time_s",-1.],["phase_t",.5],["time_s","1"],["phase",7]]:
		var bad=legal.duplicate();bad[pair[0]]=pair[1];bads.append(bad)
	var missing=legal.duplicate();missing.erase("state");bads.append(missing)
	for bad in bads:
		if w.apply_preview(bad)=="" or snapshot(w)!=held:report.failures.append("illegal input changed held pose: "+str(bad))
		else:report.negative_hold+=1
	report.reverse_time = 0
	for t in [0.25,2.25,0.75,0.0]:
		var preset={"state":"work","phase":"completed","phase_t":0.,"cargo":"empty","reason":"none","time_s":t}
		w.apply_preview(preset);var expected=snapshot(w)
		var later=preset.duplicate();later.time_s=100.0;w.apply_preview(later)
		w.apply_preview(preset)
		if snapshot(w)!=expected:report.failures.append("reverse time did not restore exact source pose")
		else:report.reverse_time+=1
	report.reason_pose = 0
	for state in ["disabled","towed","maintenance"]:
		var preset={"state":state,"phase":"completed","phase_t":0.,"cargo":"empty","reason":"none","time_s":1.}
		w.apply_preview(preset);var expected=snapshot(w)
		for reason in ["no_power","mechanical","both"]:
			preset.reason=reason
			if w.apply_preview(preset)!="" or snapshot(w)!=expected:report.failures.append("fault reason rejected or changed mechanical pose")
			else:report.reason_pose+=1
	var player=w.get_node("Model/AnimationPlayer")
	var library=player.get_animation_library("")
	var work_clip=library.get_animation("work")
	w.apply_preview(legal);held=snapshot(w)
	library.remove_animation("work")
	report.missing_clip_holds = w.apply_preview(legal)!="" and snapshot(w)==held
	if not report.missing_clip_holds:report.failures.append("missing clip failed to keep legal pose")
	library.add_animation("work",work_clip)
	report.active_measured={"min":[low.x,low.y,low.z],"max":[high.x,high.y,high.z]}
	var file=FileAccess.open("user://actual.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "));file.close()
	print("U01_ACTUAL_MATRIX cases=",report.cases," negative_hold=",report.negative_hold," failures=",report.failures.size())
	quit(0 if report.failures.is_empty() else 1)
