extends SceneTree
func _initialize():call_deferred("run")
func meshes(node:Node,out:Array):
	if node is MeshInstance3D:out.append(node)
	for child in node.get_children():meshes(child,out)
func world_box(node:MeshInstance3D)->AABB:
	var points:Array[Vector3]=[]
	for i in node.mesh.get_surface_count():
		for v in node.mesh.surface_get_arrays(i)[Mesh.ARRAY_VERTEX]:points.append(node.global_transform*v)
	assert(not points.is_empty())
	var box=AABB(points[0],Vector3.ZERO)
	for p in points:box=box.expand(p)
	return box
func gap(a:AABB,b:AABB)->float:
	var d=(b.position-a.end).max(a.position-b.end)
	return max(d.x,max(d.y,d.z))
func run():
	var args=OS.get_cmdline_user_args();assert(args.size()==1)
	var station=load("res://models/charger.glb").instantiate();root.add_child(station)
	var robot=load("res://models/u01-old.glb").instantiate();root.add_child(robot);robot.position.y=.02
	var actor=robot.find_child("AnimationPlayer",true,false) as AnimationPlayer
	var cap=robot.find_child("ChargeCap",true,false) as MeshInstance3D
	assert(actor and cap and actor.has_animation("charge"))
	var facility_meshes:Array=[];var unit_meshes:Array=[];meshes(station,facility_meshes);meshes(robot,unit_meshes)
	var min_gap=INF;var old_clashes=0;var rows:Array=[]
	# This virtual box reproduces the rejected design, not a delivered F04 model.
	var rejected_head=AABB(Vector3(.52,.51,-.43),Vector3(.06,.12,.16))
	actor.play("charge")
	for i in range(61):
		actor.seek(float(i)/60.0,true);actor.pause()
		var cap_box=world_box(cap)
		if gap(cap_box,rejected_head)<0:old_clashes+=1
		for part in facility_meshes:
			if str(part.name) in ["Foundation","BayMark"]:continue
			var fixed=world_box(part)
			for unit in unit_meshes:
				var distance=gap(fixed,world_box(unit));assert(distance>1e-6,"F04 clash: "+str(part.name)+" / "+str(unit.name))
				min_gap=min(min_gap,distance)
		rows.append({"time_s":float(i)/60.0,"cap_min":[cap_box.position.x,cap_box.position.y,cap_box.position.z],"cap_max":[cap_box.end.x,cap_box.end.y,cap_box.end.z]})
	assert(cap.transform.basis.x.distance_to(Vector3(.5,.8660254,0))<1e-5,"charge pose was not applied")
	assert(old_clashes>0,"rejected candidate clash was not reproduced")
	var out=FileAccess.open(args[0],FileAccess.WRITE);assert(out)
	out.store_string(JSON.stringify({"station_sha256":FileAccess.get_sha256("res://models/charger.glb"),"u01_sha256":FileAccess.get_sha256("res://models/u01-old.glb"),"demo_offset_y":.02,"charge_samples":61,"nonground_mesh_pairs_per_sample":(facility_meshes.size()-2)*unit_meshes.size(),"minimum_separating_axis_gap_m":min_gap,"rejected_virtual_head_clash_samples":old_clashes,"rows":rows,"scope":"candidate static bay + U01 charge lid sweep; no physical insertion/gameplay"},"  "));out.close()
	print("F04_CLEARANCE_OK samples=61 min_gap=",min_gap," rejected_head_clashes=",old_clashes);quit(0)
