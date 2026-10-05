extends Node
func snapshot(w: Node) -> Array:
	var out: Array=[]
	for n in w.find_children("*","Node3D",true,false): out.append([n.transform,n.visible])
	return out
func _ready():
	var out_dir=OS.get_cmdline_user_args()[0]
	var host=load("res://host.gd").new()
	add_child(host);host._build_scene_scaffold({})
	var records: Array=[]
	var objects: Array=[]
	var cache={}
	for item in [["tuoyun",Vector3(-5,0,0),"work",1.],["solar",Vector3.ZERO,"work",.25],["processor",Vector3(5,0,0),"work",.5]]:
		var d=host._load_manifest("res://manifests/"+item[0]+"-r1.json")
		if not d.ok:printerr(d.error);get_tree().quit(1);return
		var inst=host._instantiate_sample(d.data)
		if not inst.ok:printerr(inst.error);get_tree().quit(1);return
		var w=inst.wrapper
		var err=host._bind_materials(w,d.data,cache)
		if err!="":printerr(err);get_tree().quit(1);return
		err=host._check_bounds(w,d.data)
		if err!="":printerr(err);get_tree().quit(1);return
		var preset={"state":item[2],"phase":"completed","phase_t":0.,"cargo":"empty","reason":"none","time_s":item[3]}
		err=w.apply_preview(preset)
		if err!="":printerr(err);get_tree().quit(1);return
		var slot=Node3D.new();host._entity_root().get_node("VisualRoot").add_child(slot)
		w.reparent(slot,false);slot.position=item[1]
		objects.append([w,snapshot(w)])
		records.append({"id":d.data.id,"glb_sha256":d.data.glb_sha256,"offset_m":[item[1].x,item[1].y,item[1].z],"preset":preset})
	host.ViewR1.place_camera(host._cam,"normal")
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	for pair in objects:
		if snapshot(pair[0])!=pair[1]:printerr("paused source pose changed across two rendered frames");get_tree().quit(1);return
	DirAccess.make_dir_recursive_absolute(out_dir)
	var image=host._world.get_texture().get_image()
	if image.get_width()!=1920 or image.get_height()!=1200 or image.save_png(out_dir+"/normal.png")!=OK:get_tree().quit(1);return
	# Presentation view only; frozen normal comparison is the previous image.
	host._cam.fov=50;host._cam.look_at_from_position(Vector3(9,9,13),Vector3.ZERO,Vector3.UP)
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	image=host._world.get_texture().get_image()
	if image.save_png(out_dir+"/presentation.png")!=OK:get_tree().quit(1);return
	var report={"samples":records,"paused_two_rendered_frames":"PASS","renderer":RenderingServer.get_current_rendering_method(),"adapter":RenderingServer.get_video_adapter_name(),"normal":{"position":[18.3848,28,18.3848],"target":[0,0,0],"fov":60},"presentation":{"position":[9,9,13],"target":[0,0,0],"fov":50,"purpose":"presentation only, not frozen acceptance view"}}
	var f=FileAccess.open(out_dir+"/gallery.json",FileAccess.WRITE);f.store_string(JSON.stringify(report,"  "));f.close()
	print("GALLERY_OK samples=3 paused_two_rendered_frames=PASS");get_tree().quit(0)
