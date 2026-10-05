extends Node
const View = preload("res://scenes/art_preview_r1/preview_view_r1.gd")
const ROLES = ["body_light","frame_dark","rubber","accent_warm","solar_face","soil_mars"]
var records: Array=[]
static func bind(node: Node, rows: Array) -> bool:
	if node is MeshInstance3D:
		assert(node.material_override == null and node.material_overlay == null)
		for i in node.mesh.get_surface_count():
			var role: String=node.mesh.surface_get_material(i).resource_name
			assert(role in ROLES,"unknown source material "+role)
			var material=load("res://assets/materials/art-r1/"+role+".tres")
			assert(material is StandardMaterial3D)
			node.set_surface_override_material(i,material)
			rows.append({"node":str(node.get_path()),"surface":i,"role":role})
	for child in node.get_children():assert(bind(child,rows))
	return true
func _ready():
	assert(DisplayServer.get_name() != "headless","real window required")
	var args=OS.get_cmdline_user_args();assert(args.size()==2)
	var plan=JSON.parse_string(FileAccess.get_file_as_string(args[0]));assert(plan is Array)
	var outdir=args[1];assert(not DirAccess.dir_exists_absolute(outdir))
	assert(DirAccess.make_dir_recursive_absolute(outdir)==OK)
	var vp=SubViewport.new();View.configure_viewport(vp);add_child(vp)
	var world=Node3D.new();vp.add_child(world)
	var cam=View.build_camera();world.add_child(cam)
	var sun=View.build_light();world.add_child(sun)
	var env=View.build_environment();world.add_child(env);View.request_shadow_atlas()
	var ground=MeshInstance3D.new();var plane=PlaneMesh.new();plane.size=Vector2(20,20)
	ground.mesh=plane;ground.set_surface_override_material(0,load("res://assets/materials/art-r1/soil_mars.tres"));world.add_child(ground)
	var screen=TextureRect.new();screen.texture=vp.get_texture();screen.expand_mode=TextureRect.EXPAND_IGNORE_SIZE
	screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(screen)
	for entry in plan:
		var packed=load(entry.scene) as PackedScene;assert(packed != null)
		var entity=Node3D.new();entity.name="EntityRoot";world.add_child(entity)
		var visual=Node3D.new();visual.name="VisualRoot";entity.add_child(visual)
		var sample=packed.instantiate() as Node3D;visual.add_child(sample)
		if entry.asset == "recovery":entity.position.y=.04
		assert(sample.apply_preview(entry.preset)=="","preset failure "+str(entry))
		assert(sample.transform==Transform3D.IDENTITY and visual.transform==Transform3D.IDENTITY)
		var bindings: Array=[];assert(bind(sample,bindings))
		var source: String=sample.get_node("Model").scene_file_path
		var secondary=sample.get_node_or_null("TowDemo")
		for view in entry.views:
			View.place_camera(cam,view.camera);entity.rotation_degrees.y=view.yaw
			await RenderingServer.frame_post_draw;await RenderingServer.frame_post_draw
			var filename=entry.asset+"_"+entry.label+"_"+view.camera+"_"+str(view.yaw)+".png"
			assert(vp.get_texture().get_image().save_png(outdir+"/"+filename)==OK)
			records.append({"file":filename,"png_sha256":FileAccess.get_sha256(outdir+"/"+filename),"asset":entry.asset,"label":entry.label,"preset":entry.preset,"source":source,"source_sha256":FileAccess.get_sha256(source),"candidate_attachment_sha256":FileAccess.get_sha256(secondary.scene_file_path) if secondary else null,"binding_count":bindings.size(),"camera":view.camera,"yaw":view.yaw,"entity_offset_y":entity.position.y,"actual_view":View.snapshot(vp,cam,sun,env,view.camera),"scope":entry.scope})
		entity.queue_free();await get_tree().process_frame
	var file=FileAccess.open(outdir+"/captures.json",FileAccess.WRITE);assert(file != null)
	file.store_string(JSON.stringify(records,"  "));file.close()
	print("LF_CAPTURE_OK images=",records.size());get_tree().quit(0)
