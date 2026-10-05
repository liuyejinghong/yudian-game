extends Node
const View = preload("res://scenes/art_preview_r1/preview_view_r1.gd")
var vp: SubViewport
var world: Node3D
var cam: Camera3D
var sun: DirectionalLight3D
var env: WorldEnvironment
var records: Array=[]
func collect(node: Node, bindings: Array):
	if node is AnimationPlayer: node.stop()
	if node.name=="TowRod": node.visible=false
	if node is MeshInstance3D:
		for i in node.mesh.get_surface_count():
			var material=node.mesh.surface_get_material(i)
			var role=material.resource_name if material else "MISSING"
			if not role in ["body_light","frame_dark","rubber","accent_warm"]:
				push_error("unknown material role: "+role);get_tree().quit(1);return
			var m=load("res://assets/materials/art-r1/"+role+".tres")
			node.set_surface_override_material(i,m)
			bindings.append({"node":str(node.get_path()),"surface":i,"role":role})
	for child in node.get_children(): collect(child,bindings)
func _ready():
	if DisplayServer.get_name()=="headless":printerr("real window required");get_tree().quit(1);return
	var args=OS.get_cmdline_user_args()
	if args.size()!=1:printerr("capture requires a new output directory");get_tree().quit(1);return
	var outdir=args[0]
	if DirAccess.dir_exists_absolute(outdir):printerr("capture directory exists");get_tree().quit(1);return
	DirAccess.make_dir_recursive_absolute(outdir)
	vp=SubViewport.new();View.configure_viewport(vp);add_child(vp)
	world=Node3D.new();vp.add_child(world)
	cam=View.build_camera();world.add_child(cam)
	sun=View.build_light();world.add_child(sun)
	env=View.build_environment();world.add_child(env);View.request_shadow_atlas()
	var ground=MeshInstance3D.new();var plane=PlaneMesh.new();plane.size=Vector2(20,20)
	ground.mesh=plane;ground.set_surface_override_material(0,load("res://assets/materials/art-r1/soil_mars.tres"));world.add_child(ground)
	var image_view=TextureRect.new();image_view.texture=vp.get_texture();image_view.expand_mode=TextureRect.EXPAND_IGNORE_SIZE
	image_view.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(image_view)
	var files=["u01-prev","u01-hf"]
	for file in files:
		var scene=load("res://models/"+file+".glb") as PackedScene
		if scene==null:printerr("GLB load failed");get_tree().quit(1);return
		var model=scene.instantiate();world.add_child(model)
		var bindings:Array=[];collect(model,bindings)
		var cargo=model.find_child("CargoBox",true,false)
		var payload=model.find_child("RackPayloadDemo",true,false)
		for view in ["normal","close"]:
			View.place_camera(cam,view)
			for yaw in [0,90]:
				model.rotation_degrees.y=yaw
				for loaded in ([false,true] if cargo or payload else [false]):
					if cargo:cargo.visible=loaded
					if payload:payload.visible=loaded
					await RenderingServer.frame_post_draw
					await RenderingServer.frame_post_draw
					var filename=file+"_"+view+"_"+str(yaw)+"_"+("loaded" if loaded else "empty")+".png"
					var img=vp.get_texture().get_image()
					if img.get_width()!=1920 or img.get_height()!=1200 or img.save_png(outdir+"/"+filename)!=OK:
						printerr("image save failed");get_tree().quit(1);return
					records.append({"file":filename,"sample":file,"source_sha256":FileAccess.get_sha256("res://models/"+file+".glb"),"geometry_pose":"idle static trial","camera":view,"yaw":yaw,"loaded_demo":loaded,"bindings":bindings,"actual_view":View.snapshot(vp,cam,sun,env,view)})
		model.queue_free();await get_tree().process_frame
	var f=FileAccess.open(outdir+"/captures.json",FileAccess.WRITE);f.store_string(JSON.stringify(records,"  "));f.close()
	print("ART_DUAL_CAPTURE_OK images=",records.size());get_tree().quit(0)
