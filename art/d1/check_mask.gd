extends SceneTree

var material: ShaderMaterial
var camera: Camera3D
var checks := 0
var failures := 0
var output: String
const A := Vector2(-8.4375,-0.9375) # column23,row31
const B := Vector2(26.25,16.875) # column60,row50, away from current work
const C := Vector2(-6.5625,-0.9375) # unchanged neighbour within work radius
const D := Vector2(12,-10)

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	output = OS.get_environment("YUDIAN_MASK_OUTPUT")
	if output.is_empty() or DirAccess.dir_exists_absolute(output):
		printerr("set YUDIAN_MASK_OUTPUT to a new directory")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(output)
	root.size = Vector2i(768,768)
	var world := Node3D.new()
	root.add_child(world)
	var environment := WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color.BLACK
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color.WHITE
	environment.environment.ambient_light_energy = 1.0
	world.add_child(environment)
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 68
	world.add_child(camera)
	camera.look_at_from_position(Vector3(0,10,0),Vector3.ZERO,Vector3(0,0,-1))
	camera.current = true
	var mesh := MeshInstance3D.new()
	mesh.mesh = PlaneMesh.new()
	mesh.mesh.size = Vector2(64,64)
	material = ShaderMaterial.new()
	material.shader = load(get_script().resource_path.get_base_dir().path_join("surface.gdshader"))
	mesh.material_override = material
	world.add_child(mesh)
	var mask := Image.create(65,65,false,Image.FORMAT_R8)
	mask.fill(Color.BLACK)
	for texel in [Vector2i(23,31),Vector2i(60,50),Vector2i(0,0),Vector2i(64,64)]:
		mask.set_pixelv(texel,Color.WHITE)
	material.set_shader_parameter("region_radius",2.0)
	var natural := await _frame(0,A,false)
	var gravel := await _frame(1,A,false)
	var compacted := await _frame(2,A,false)
	_check(_at(gravel,A).distance_to(_at(natural,A)) > .1,"legacy working differs from natural")
	_check(_at(compacted,A).distance_to(_at(natural,A)) > .1,"legacy compacted differs from natural")
	material.set_shader_parameter("committed_mask",ImageTexture.create_from_image(mask))
	var history := await _frame(0,A,true)
	_check(_same(_at(history,A),_at(compacted,A)),"mask texel center is compacted")
	_check(_same(_at(history,C),_at(natural,C)),"unchanged neighbour is natural")
	var distant_reference := await _frame(2,B,false)
	_check(_same(_at(history,B),_at(distant_reference,B)),"non-symmetric distant node is compacted")
	var work := await _frame(1,A,true)
	_check(_same(_at(work,A),_at(compacted,A)),"work never covers committed node")
	_check(_same(_at(work,C),_at(gravel,C)),"working covers only uncommitted neighbour")
	_check(_same(_at(work,B),_at(history,B)),"distant history survives another work region")
	var stage_two := await _frame(2,D,true)
	_check(_same(_at(stage_two,D),_at(natural,D)),"enabled mask ignores stage2 for uncommitted region")
	_check(_same(_at(stage_two,B),_at(history,B)),"history survives stage2 elsewhere")
	for point in [Vector2(-29.8,-29.8),Vector2(29.8,29.8)]:
		var reference := await _frame(2,point,false)
		_check(_same(_at(history,point),_at(reference,point)),"world endpoint texel " + str(point))
	_check(_same(_at(history,Vector2(-31,-31)),_at(natural,Vector2(-31,-31))),"no mask bleed outside world")
	_check(work.save_png(output.path_join("synthetic-mask-working.png")) == OK,"save synthetic render")
	var record := {"scope":"synthetic plane/mask shader check; not world authority, live task, or default application", "checks":checks,"failures":failures,"engine":Engine.get_version_info().string,"driver":RenderingServer.get_current_rendering_driver_name(),"display":DisplayServer.get_name(),"viewport_px":[root.size.x,root.size.y],"mask_size":[65,65],"marked_nodes":[[23,31],[60,50],[0,0],[64,64]],"world_origin":[-30,-30],"world_span":[60,60]}
	var file := FileAccess.open(output.path_join("mask-check.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(record,"\t") + "\n")
	print("A03_MASK_PASS %d/%d" % [checks-failures,checks])
	quit(1 if failures else 0)

func _frame(stage: int, center: Vector2, enabled: bool) -> Image:
	material.set_shader_parameter("stage",stage)
	material.set_shader_parameter("region_center",center)
	material.set_shader_parameter("use_committed_mask",enabled)
	await process_frame
	await RenderingServer.frame_post_draw
	return root.get_texture().get_image()

func _at(image: Image, point: Vector2) -> Vector3:
	var pixel := camera.unproject_position(Vector3(point.x,0,point.y)).round()
	var color := image.get_pixel(int(pixel.x),int(pixel.y))
	return Vector3(color.r,color.g,color.b)

func _same(a: Vector3,b: Vector3) -> bool:
	return a.distance_to(b) < .02

func _check(value: bool, name: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("A03_MASK_FAIL " + name)
