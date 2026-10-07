extends SceneTree
func _initialize():
 _capture.call_deferred()
func _frame(scene, name, eye, target, lens, normal=false):
 scene.camera.look_at_from_position(eye,target,Vector3.UP)
 scene.camera.fov=rad_to_deg(2*atan(36.0*1100/1500/(2*lens)))
 root.debug_draw=Viewport.DEBUG_DRAW_NORMAL_BUFFER if normal else Viewport.DEBUG_DRAW_DISABLED
 for i in range(15):
  await process_frame
 await RenderingServer.frame_post_draw
 assert(root.get_texture().get_image().save_png("OUTPUT_DIRECTORY/"+name+".png")==OK)
func _capture():
 root.size=Vector2i(1500,1100)
 var scene=load("res://lookdev.tscn").instantiate()
 root.add_child(scene)
 await process_frame
 scene.note.visible=false
 var results={"method":"same Blender camera eye/target/lens and 1500x1100 viewport; lighting comparison is approximate across engines", "source_unchanged":true}
 for mesh in scene.hero.find_children("*","MeshInstance3D",true,false):
  var material=mesh.get_active_material(0)
  var gpu=material.normal_texture.get_image()
  if gpu.is_compressed(): gpu.decompress()
  var source=Image.load_from_file("res://hifi/mars-outcrop-hifi-r2_normal.png")
  var max_error=0.0
  for i in range(2048):
   var p=Vector2i((i*197)%4096,(i*503)%4096)
   var a=source.get_pixelv(p);var b=gpu.get_pixelv(p)
   max_error=max(max_error,abs(a.r-b.r),abs(a.g-b.g),abs(a.b-b.b))
  results["normal_scale"]=material.normal_scale
  results["gpu_size"]=[gpu.get_width(),gpu.get_height()]
  results["gpu_format"]=gpu.get_format()
  results["source_format"]=source.get_format()
  results["sampled_rgb_max_error"]=max_error
  results["sampled_pixels"]=2048
  break
 await _frame(scene,"bed-camera-repeat",Vector3(4.3,1.8,4),Vector3(4.55,1.08,1.02),72)
 await _frame(scene,"rear-camera-repeat",Vector3(1.9,2.6,-5.4),Vector3(5.62,1.03,-.69),56)
 await _frame(scene,"bed-normal-repeat",Vector3(4.3,1.8,4),Vector3(4.55,1.08,1.02),72,true)
 for node in scene.get_children():
  if node is DirectionalLight3D:
   node.basis=Basis(Vector3(0.6957721980440041,0.4694715627858908,0.5435968176547656),Vector3(0.3482041577695088,0.44147379642946344,-0.826955108562843),Vector3(-0.6282156579877981,0.7646550456261504,0.14369324360396274))
   node.light_energy=2.8
   node.light_color=Color.WHITE
 var env=scene.get_node("WorldEnvironment").environment
 env.ambient_light_color=Color(.52,.58,.66)
 env.ambient_light_energy=.55
 env.fog_enabled=false
 await _frame(scene,"bed-camera-light-axis",Vector3(4.3,1.8,4),Vector3(4.55,1.08,1.02),72)
 await _frame(scene,"rear-camera-light-axis",Vector3(1.9,2.6,-5.4),Vector3(5.62,1.03,-.69),56)
 var file=FileAccess.open("OUTPUT_DIRECTORY/diagnostic-axis.json",FileAccess.WRITE)
 file.store_string(JSON.stringify(results,"\t")+"\n")
 print("HIFI_CAMERA_DIAGNOSTIC_OK ",results)
 quit()
