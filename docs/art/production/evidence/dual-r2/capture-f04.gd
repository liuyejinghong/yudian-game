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
	var group=Node3D.new();world.add_child(group)
	var station=load("res://models/charger.glb").instantiate();group.add_child(station)
	var unit=load("res://models/u01-old.glb").instantiate();group.add_child(unit);unit.position.y=.02
	var bindings:Array=[];collect(group,bindings)
	var cargo=unit.find_child("CargoBox",true,false);assert(cargo);cargo.visible=false
	var actor=unit.find_child("AnimationPlayer",true,false) as AnimationPlayer
	assert(actor and actor.has_animation("charge"))
	for view in ["normal","close"]:
		View.place_camera(cam,view)
		for yaw in [0,90]:
			group.rotation_degrees.y=yaw
			for pose in ["empty","idle_demo","charge_demo"]:
				unit.visible=pose!="empty"
				actor.play("charge");actor.seek(1.0 if pose=="charge_demo" else 0.0,true);actor.pause()
				await RenderingServer.frame_post_draw
				await RenderingServer.frame_post_draw
				var filename="charger_"+view+"_"+str(yaw)+"_"+pose+".png"
				var img=vp.get_texture().get_image()
				assert(img.get_width()==1920 and img.get_height()==1200 and img.save_png(outdir+"/"+filename)==OK)
				records.append({"file":filename,"sample":"F04 static waiting head + U01 demo","source_sha256":FileAccess.get_sha256("res://models/charger.glb"),"demo_sha256":FileAccess.get_sha256("res://models/u01-old.glb"),"pose":pose,"unit_offset_y":.02,"camera":view,"yaw":yaw,"bindings":bindings,"actual_view":View.snapshot(vp,cam,sun,env,view)})
	var f=FileAccess.open(outdir+"/captures.json",FileAccess.WRITE);f.store_string(JSON.stringify(records,"  "));f.close()
	print("ART_DUAL_CAPTURE_OK images=",records.size());get_tree().quit(0)
