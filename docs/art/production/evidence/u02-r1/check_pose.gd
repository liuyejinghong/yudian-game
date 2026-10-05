extends SceneTree
func _initialize():call_deferred("run")
func meshes(node:Node,out:Array):
	if node is MeshInstance3D:out.append(node)
	for child in node.get_children():meshes(child,out)
func box(node:MeshInstance3D)->AABB:
	var points:Array[Vector3]=[]
	for i in node.mesh.get_surface_count():
		for v in node.mesh.surface_get_arrays(i)[Mesh.ARRAY_VERTEX]:points.append(node.global_transform*v)
	assert(not points.is_empty())
	var result=AABB(points[0],Vector3.ZERO)
	for point in points:result=result.expand(point)
	return result
func gap(a:AABB,b:AABB)->float:
	var d=(b.position-a.end).max(a.position-b.end)
	return max(d.x,max(d.y,d.z))
func separation(a:MeshInstance3D,b:MeshInstance3D)->float:
	var broad=gap(box(a),box(b))
	if broad>1e-6:return broad
	# Angled shafts can overlap an AABB while their enclosing oriented boxes remain apart.
	var axes_a=[a.global_basis.x.normalized(),a.global_basis.y.normalized(),a.global_basis.z.normalized()]
	var axes_b=[b.global_basis.x.normalized(),b.global_basis.y.normalized(),b.global_basis.z.normalized()]
	var half_a=a.mesh.get_aabb().size*.5*a.global_basis.get_scale().abs()
	var half_b=b.mesh.get_aabb().size*.5*b.global_basis.get_scale().abs()
	var delta=(b.global_transform*b.mesh.get_aabb().get_center())-(a.global_transform*a.mesh.get_aabb().get_center())
	var axes=axes_a+axes_b
	for x in axes_a:
		for y in axes_b:
			var cross=x.cross(y)
			if cross.length_squared()>1e-12:axes.append(cross.normalized())
	var best=-INF
	for axis in axes:
		var radius=0.0
		for j in range(3):radius+=abs(axis.dot(axes_a[j]))*half_a[j]+abs(axis.dot(axes_b[j]))*half_b[j]
		best=max(best,abs(delta.dot(axis))-radius)
	return best
func part(root_node:Node,name:String)->Node3D:
	var found=root_node.find_child(name,true,false) as Node3D
	assert(found,"Missing frozen part "+name)
	return found
func pose(model:Node,t:float,maintenance:bool):
	part(model,"Shoulder").rotation.x=deg_to_rad(55.0 if maintenance else lerp(55.0,-20.0,t))
	part(model,"Elbow").rotation.x=deg_to_rad(-155.0 if maintenance else lerp(-155.0,5.0,t))
	part(model,"HoodLid").rotation.x=deg_to_rad(70.0*t if maintenance else 0.0)
	for side in ["L","R"]:part(model,"SupportSlide"+side).position.y=.30 if maintenance else lerp(.30,.03,t)
func run():
	var args=OS.get_cmdline_user_args();assert(args.size()==1)
	var model=load("res://models/u02-idle.glb").instantiate();root.add_child(model)
	var all:Array=[];meshes(model,all)
	var wheels=all.filter(func(m):return str(m.name).begins_with("Wheel_"));assert(wheels.size()==6)
	var new_parts=all.filter(func(m):return not m.is_ancestor_of(wheels[0]) and not str(m.get_path()).contains("/Wheels/"))
	var fixed_names=["FrameBed","BodyShell","RearFloor","RearWallL","RearWallR","RearWallFront","RearWallBack","ProtectedModule","TowFrontMount","TowFrontEar","TowRearMount"]
	var fixed:Array=[]
	for name in fixed_names:fixed.append(part(model,name))
	var moving:Array=[]
	for name in ["UpperArm","LowerArm","WorkPad"]:moving.append(part(model,name))
	var records:Array=[];var minimum=INF
	for maintenance in [false,true]:
		var steps=70 if maintenance else 60
		for i in range(steps+1):
			var t=float(i)/steps;pose(model,t,maintenance)
			for m in new_parts:
				var bounds=box(m)
				assert(bounds.position.y>=-1e-6,"below ground "+str(m.name))
				assert(bounds.position.x>=-.900001 and bounds.end.x<=.900001 and bounds.position.z>=-1.600001 and bounds.end.z<=.840001 and bounds.end.y<=1.520001,"outside demo envelope "+str(m.name))
				for wheel in wheels:
					var d=gap(bounds,box(wheel));assert(d>1e-6,"new part cuts wheel: "+str(m.name));minimum=min(minimum,d)
			for m in moving:
				for f in fixed:
					var d=separation(m,f);assert(d>1e-6,"tool/arm cuts body: "+str(m.name)+" / "+str(f.name));minimum=min(minimum,d)
			for f in fixed:
				var d=separation(part(model,"HoodLidPanel"),f);assert(d>1e-6,"lid cuts fixed part "+str(f.name));minimum=min(minimum,d)
			var lane_gap=separation(part(model,"UpperArm"),part(model,"LowerArm"));assert(lane_gap>1e-6);minimum=min(minimum,lane_gap)
			records.append({"demo":"maintenance" if maintenance else "work","t":t,"shoulder_degrees":rad_to_deg(part(model,"Shoulder").rotation.x),"elbow_degrees":rad_to_deg(part(model,"Elbow").rotation.x)})
		var reference=load("res://models/u02-maintenance-demo.glb" if maintenance else "res://models/u02-work-demo.glb").instantiate();root.add_child(reference)
		for name in ["Shoulder","Elbow","HoodLid","SupportSlideL","SupportSlideR","Socket_Work"]:
			assert(part(model,name).global_transform.is_equal_approx(part(reference,name).global_transform),"interpolation endpoint differs from exported snapshot "+name)
		reference.free()
	var out=FileAccess.open(args[0],FileAccess.WRITE);assert(out)
	out.store_string(JSON.stringify({"source_sha256":FileAccess.get_sha256("res://models/u02-idle.glb"),"samples":records.size(),"minimum_separating_axis_gap_m":minimum,"rows":records,"scope":"candidate joint interpolation on imported geometry, sampled AABB for new parts vs wheels; OBB SAT resolves ambiguous shafts/pad/lid vs body and arm lanes; intentional assembly contact excluded; no source animation or continuous sweep proof"},"  "));out.close()
	print("U02_POSE_OK samples=",records.size()," min_axis_gap=",minimum);quit(0)
