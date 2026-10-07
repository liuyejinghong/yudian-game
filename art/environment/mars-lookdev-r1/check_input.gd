extends SceneTree

func _initialize() -> void:
	_check.call_deferred()

func _check() -> void:
	if not OS.get_environment("YUDIAN_MARS_OUTPUT").is_empty():
		push_error("clear YUDIAN_MARS_OUTPUT for input check")
		quit(1)
		return
	var scene: Node3D = load("res://lookdev.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	var views := [[KEY_3,"horizon"],[KEY_2,"near"],[KEY_4,"reverse"],[KEY_1,"normal"]]
	if scene.hifi:
		views.append_array([[KEY_5,"detail"],[KEY_6,"ground"]])
	for row in views:
		_key(scene,row[0])
		assert(scene.view == row[1])
	_key(scene,KEY_1)
	_key(scene,KEY_L)
	assert(scene.low and root.scaling_3d_scale < 1.0)
	_key(scene,KEY_L)
	assert(not scene.low and root.scaling_3d_scale == 1.0)
	_key(scene,KEY_H)
	assert(not scene.note.visible)
	_key(scene,KEY_H)
	assert(scene.note.visible)
	var pose: Transform3D = scene.camera.transform
	var motion := InputEventMouseMotion.new()
	motion.relative = Vector2(40,0)
	scene._unhandled_input(motion)
	assert(scene.camera.transform == pose)
	var button := InputEventMouseButton.new()
	button.button_index = MOUSE_BUTTON_RIGHT
	button.pressed = true
	scene._unhandled_input(button)
	scene._unhandled_input(motion)
	assert(scene.camera.transform != pose)
	button.pressed = false
	scene._unhandled_input(button)
	_key(scene,KEY_1)
	assert(scene.camera.transform.is_equal_approx(pose))
	print("MARS_LOOKDEV_INPUT_OK views=",views.size()," low_toggle=true label_toggle=true drag_reset=true")
	quit()

func _key(scene: Node3D, code: int) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = true
	scene._unhandled_input(event)
