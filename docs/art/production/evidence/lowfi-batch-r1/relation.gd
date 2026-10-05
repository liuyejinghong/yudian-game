extends Node
const View=preload("res://scenes/art_preview_r1/preview_view_r1.gd")
const Capture=preload("res://capture.gd")
var rows: Array=[]
func p(state="idle",cargo="empty",time=0.0,phase="completed",phase_t=0.0)->Dictionary:
	return {"state":state,"phase":phase,"phase_t":phase_t,"cargo":cargo,"reason":"none","time_s":time}
func put(world: Node3D,key: String,pos: Vector3,preset: Dictionary) -> bool:
	var old={"tuoyun":"units/tuoyun-r1","solar":"facilities/solar-r1","processor":"facilities/processor-r1"}
	var path="res://assets/"+old[key]+"/preview.tscn" if old.has(key) else "res://assets/lowfi-batch-r1/"+key+".tscn"
	var scene=load(path) as PackedScene;assert(scene != null)
	var entity=Node3D.new();entity.name=key.replace("-","_")+str(rows.size());entity.position=pos;world.add_child(entity)
	var visual=Node3D.new();visual.name="VisualRoot";entity.add_child(visual)
	var sample=scene.instantiate() as Node3D;visual.add_child(sample)
	assert(sample.apply_preview(preset)=="")
	assert(sample.transform==Transform3D.IDENTITY and visual.transform==Transform3D.IDENTITY)
	var bindings: Array=[];assert(Capture.bind(sample,bindings))
	var source: String=sample.get_node("Model").scene_file_path
	rows.append({"asset":key,"position":[pos.x,pos.y,pos.z],"preset":preset,"source_sha256":FileAccess.get_sha256(source),"binding_count":bindings.size()})
	return true
func _ready():
	assert(DisplayServer.get_name() != "headless")
	var args=OS.get_cmdline_user_args();assert(args.size()==1)
	var outdir=args[0];assert(not DirAccess.dir_exists_absolute(outdir))
	assert(DirAccess.make_dir_recursive_absolute(outdir)==OK)
	var vp=SubViewport.new();View.configure_viewport(vp);add_child(vp)
	var world=Node3D.new();vp.add_child(world)
	var cam=View.build_camera();world.add_child(cam);View.place_camera(cam,"normal")
	var sun=View.build_light();world.add_child(sun)
	var env=View.build_environment();world.add_child(env);View.request_shadow_atlas()
	var ground=MeshInstance3D.new();var plane=PlaneMesh.new();plane.size=Vector2(26,26)
	ground.mesh=plane;ground.set_surface_override_material(0,load("res://assets/materials/art-r1/soil_mars.tres"));world.add_child(ground)
	var screen=TextureRect.new();screen.texture=vp.get_texture();screen.expand_mode=TextureRect.EXPAND_IGNORE_SIZE
	screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(screen)
	assert(put(world,"solar",Vector3(-5.3,0,-5.3),p()))
	assert(put(world,"solar",Vector3(.2,0,-5.3),p("idle","empty",0,"deploying",.5)))
	assert(put(world,"processor",Vector3(5.8,0,-5.3),p("work","empty",.5)))
	assert(put(world,"storage",Vector3(-5.7,0,0),p("work","loaded",.5)))
	assert(put(world,"tuoyun",Vector3(-5.7,0,-2.4),p("work","loaded",1)))
	assert(put(world,"repair",Vector3(0,0,.3),p("work","empty",.5)))
	assert(put(world,"tuoyun",Vector3(0,.04,.3),p("maintenance","empty",1)))
	assert(put(world,"charger",Vector3(4.2,0,.5),p("work","empty",.5)))
	assert(put(world,"tuoyun",Vector3(4.2,.02,.5),p("charge","empty",1)))
	assert(put(world,"lander",Vector3(5.5,0,5.3),p("work","loaded",1)))
	assert(put(world,"wangshan",Vector3(.6,0,5.6),p("work","empty",.75)))
	assert(put(world,"zhulei",Vector3(.6,0,8.2),p("work","empty",.75)))
	assert(put(world,"tuoyun",Vector3(5.5,0,9),p()))
	assert(put(world,"zhulei",Vector3(5.5,0,10.95),p("towed","empty",1)))
	var stages: Array=[]
	for key in ["terrain-original","terrain-flat","terrain-dug"]:
		var before=rows.size();assert(put(world,key,Vector3(-5,0,7.5),p()))
		await RenderingServer.frame_post_draw;await RenderingServer.frame_post_draw
		var file=key+"_normal.png";assert(vp.get_texture().get_image().save_png(outdir+"/"+file)==OK)
		stages.append({"file":file,"png_sha256":FileAccess.get_sha256(outdir+"/"+file),"layout":rows.duplicate(true),"actual_view":View.snapshot(vp,cam,sun,env,"normal")})
		var terrain=world.get_child(world.get_child_count()-1);terrain.queue_free();rows.resize(before);await get_tree().process_frame
	var record=FileAccess.open(outdir+"/relation.json",FileAccess.WRITE);assert(record != null)
	record.store_string(JSON.stringify({"ground_size_m":[26,26],"scope":"Godot independent art arrangement; terrain/recovery are visual candidates, no gameplay, power, mining, inventory, maintenance or owner acceptance","stages":stages},"  "));record.close()
	print("LF_RELATION_OK images=",stages.size());get_tree().quit(0)
