extends Node
const ROLES=["body_light","frame_dark","rubber","accent_warm","solar_face","soil_mars"]
func _ready():
    if DisplayServer.get_name()=="headless":
        printerr("M01_BOARD requires actual window renderer");get_tree().quit(1);return
    var output=OS.get_user_data_dir()+"/art-preview-evidence/m01-independent-20261004"
    if DirAccess.dir_exists_absolute(output):
        printerr("M01_BOARD output exists");get_tree().quit(1);return
    if DirAccess.make_dir_recursive_absolute(output)!=OK:
        printerr("M01_BOARD output cannot be created");get_tree().quit(1);return
    var view=SubViewport.new()
    view.size=Vector2i(1920,1200)
    view.own_world_3d=true
    view.msaa_3d=Viewport.MSAA_4X
    view.screen_space_aa=Viewport.SCREEN_SPACE_AA_DISABLED
    view.use_taa=false
    view.scaling_3d_scale=1.0
    view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
    add_child(view)
    var display=TextureRect.new()
    display.texture=view.get_texture()
    display.expand_mode=TextureRect.EXPAND_IGNORE_SIZE
    display.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    display.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    add_child(display)
    var world=Node3D.new();view.add_child(world)
    var env=Environment.new()
    env.background_mode=Environment.BG_COLOR
    env.background_color=Color("AEB8BC")
    env.background_energy_multiplier=1.0
    env.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
    env.ambient_light_color=Color("D5DBDF")
    env.ambient_light_energy=.6
    env.ambient_light_sky_contribution=0.0
    env.reflected_light_source=Environment.REFLECTION_SOURCE_DISABLED
    env.tonemap_mode=Environment.TONE_MAPPER_LINEAR
    env.tonemap_exposure=1.0
    var environment=WorldEnvironment.new();environment.environment=env;world.add_child(environment)
    var sun=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-55,-30,0)
    sun.light_color=Color.WHITE;sun.light_energy=1.0;sun.shadow_enabled=true;sun.directional_shadow_max_distance=80.0
    world.add_child(sun)
    RenderingServer.directional_shadow_atlas_set_size(4096,true)
    var ground=MeshInstance3D.new();var plane=PlaneMesh.new();plane.size=Vector2(20,20);ground.mesh=plane
    ground.set_surface_override_material(0,load("res://materials/soil_mars.tres"));world.add_child(ground)
    for i in ROLES.size():
        var center=Vector3(float(i%3-1)*1.2,.3,-.8 if i<3 else .8)
        var mat=load("res://materials/"+ROLES[i]+".tres")
        var sphere=SphereMesh.new();sphere.radius=.3;sphere.height=.6
        var cube=BoxMesh.new();cube.size=Vector3(.6,.6,.6)
        for pair in [[sphere,-.35],[cube,.35]]:
            var part=MeshInstance3D.new();part.mesh=pair[0];part.position=center+Vector3(0,0,pair[1])
            part.set_surface_override_material(0,mat);world.add_child(part)
    var camera=Camera3D.new();world.add_child(camera)
    camera.position=Vector3(4,4,6);camera.look_at(Vector3(0,.4,0),Vector3.UP)
    camera.fov=50;camera.keep_aspect=Camera3D.KEEP_HEIGHT;camera.near=.05;camera.far=200;camera.current=true
    await RenderingServer.frame_post_draw
    await RenderingServer.frame_post_draw
    var image=view.get_texture().get_image()
    if image==null or image.get_size()!=Vector2i(1920,1200):
        printerr("M01_BOARD image size/error");get_tree().quit(1);return
    var err=image.save_png(output+"/board.png")
    var data={"source":"Godot independent runtime preview","engine":Engine.get_version_info(),"display_server":DisplayServer.get_name(),"adapter":RenderingServer.get_video_adapter_name(),"viewport":[1920,1200],"camera":{"position":[4,4,6],"look_at":[0,.4,0],"fov":50,"keep_height":true},"roles":ROLES,"sphere_cube_offset_z":[-.35,.35],"light_rotation":[-55,-30,0],"light_energy":1.0,"ambient_energy":.6,"exposure":1.0,"candidate_colors":true,"owner_visual":"NOT_RUN","png_error":err}
    if RenderingServer.has_method("get_current_rendering_method"):
        data.rendering_method=RenderingServer.call("get_current_rendering_method")
    var f=FileAccess.open(output+"/board.json",FileAccess.WRITE);f.store_string(JSON.stringify(data,"  "));f.close()
    print("M01_BOARD_CAPTURE_OK ",output," actual_window=true PNG1920x1200")
    get_tree().quit(0 if err==OK else 1)
