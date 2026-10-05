extends Node
## ART-PREVIEW-01 slice B, TEST-ONLY: drives the real interactive GUI with
## programmatic input (signal emission on the actual controls) against the
## bundled probe sample. This is NOT human/native-OS input and must never be
## presented as such; the master's CUA pass remains the interactive check.
##
## Covered: initial defaults, empty/illegal time keeps the pose and reports an
## error, repeat state selection is idempotent, illegal reason pairing keeps
## the pose and reverts, Reset restores defaults, labels blind/evidence toggle
## the overlay, yaw turns only the object (light/camera stay put).

var _results: Array = []


func _check(name: String, ok: bool, detail: String = "") -> bool:
	_results.append({"name": name, "ok": ok, "detail": detail})
	print("%s%s :: %s" % ["PASS " if ok else "FAIL ", name, detail])
	return ok


func _ready() -> void:
	var preview := (load("res://scenes/art_preview_r1/PreviewR1.tscn") as PackedScene).instantiate()
	preview.gui_probe_mode = true
	preview.params = {
		"self_test": false, "mode": "manifest",
		"manifest_path": "res://scenes/art_preview_r1/probe_manifest_r1.json",
		"camera": "normal", "yaw": 0, "state": "idle", "phase": "completed",
		"phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0,
		"labels": "evidence", "capture_dir": "",
	}
	add_child(preview)
	await get_tree().process_frame
	_run_cases(preview)
	var failed := _results.filter(func(r): return not r["ok"])
	var marker := "PASS" if failed.is_empty() else "FAIL"
	print("ART_PREVIEW_R1_GUIPROBE_%s %d/%d" % [marker, _results.size() - failed.size(), _results.size()])
	get_tree().quit(0 if failed.is_empty() else 1)


func _run_cases(preview: Node) -> void:
	var panel = preview._panel
	var overlay = preview._overlay
	var world: SubViewport = preview._world
	var entity := world.get_node("EntityRoot")
	var body := world.get_node("EntityRoot/VisualRoot/SampleRoot/Model/Body") as Node3D
	var home_y := 0.2  # GLB rest position of the probe body

	if not _check("gui_initial_defaults", panel.option_text("state") == "idle"
			and panel.option_text("phase") == "completed" and panel.option_text("cargo") == "empty"
			and panel.option_text("reason") == "none" and panel.option_text("camera") == "normal"
			and panel.option_text("yaw") == "0" and panel.option_text("labels") == "evidence"
			and absf(body.position.y - home_y) < 1e-6,
			"reopen must start at the contract defaults"):
		return
	_check("gui_window_title", String(get_window().title).contains("ART-PREVIEW-R1"),
			str(get_window().title))
	_check("gui_panel_outside_viewport", not panel.is_ancestor_of(world)
			and world.get_parent() == preview, "controls live outside the SubViewport")
	_check("gui_overlay_inside_viewport", overlay.get_parent() == world)

	# empty time input: pose kept, error reported
	panel.time_line().text = ""
	preview.gui_time_submitted()
	_check("gui_empty_time_rejected", panel._error.text != "" and absf(body.position.y - home_y) < 1e-6,
			panel._error.text)
	# illegal time inputs: same behavior
	for bad in ["abc", "-1", "nan"]:
		panel.time_line().text = bad
		preview.gui_time_submitted()
		if not _check("gui_bad_time_%s" % bad, panel._error.text != ""
				and absf(body.position.y - home_y) < 1e-6, panel._error.text):
			return
	_check("gui_time_reverted", panel.time_line().text == "0", panel.time_line().text)

	# work state first (t=0 keeps the body at home), then time 0.5 raises it
	_select(panel._state, 2)  # work
	_check("gui_state_work_selected", panel._error.text == ""
			and absf(body.position.y - home_y) < 1e-6, str(body.position))
	panel.time_line().text = "0.5"
	preview.gui_time_submitted()
	var work_pose := body.transform
	_check("gui_work_time_applied", panel._error.text == ""
			and absf(body.position.y - home_y - 0.05) < 1e-6, str(body.position))

	# repeat the same state selection: idempotent, no stacked motion
	_select(panel._state, 2)  # work
	_check("gui_state_repeat_idempotent", body.transform == work_pose and panel._error.text == "",
			str(body.transform))
	_select(panel._state, 2)
	_check("gui_state_repeat_twice", body.transform == work_pose)

	# illegal reason pairing for work: error, control reverted, pose kept
	_select(panel._reason, 2)  # no_power
	_check("gui_reason_pair_rejected", panel._error.text != ""
			and panel.option_text("reason") == "none" and body.transform == work_pose,
			"%s | reason=%s" % [panel._error.text, panel.option_text("reason")])

	# blind hides the overlay, evidence shows it
	_select(panel._labels, 1)  # blind
	_check("gui_blind_hides_overlay", overlay.labels_mode == "blind"
			and not overlay._info_label.visible)
	_select(panel._labels, 0)  # evidence
	_check("gui_evidence_shows_overlay", overlay.labels_mode == "evidence"
			and overlay._info_label.visible)

	# yaw turns only the object
	var sun_rot: Vector3 = (world.get_node("KeyLight") as DirectionalLight3D).rotation_degrees
	var cam_pos: Vector3 = world.get_node("PreviewCamera").position
	_select(panel._yaw, 1)  # 90
	_check("gui_yaw_90", absf(entity.rotation.y - deg_to_rad(90.0)) < 1e-6
			and world.get_node("KeyLight").rotation_degrees == sun_rot
			and world.get_node("PreviewCamera").position == cam_pos,
			str(entity.rotation.y))
	_select(panel._camera, 1)  # close
	_check("gui_camera_close", preview._camera_mode_used == "close"
			and world.get_node("PreviewCamera").position == Vector3(3, 3, 4))

	# Build a fully non-default state before Reset: deploying/phase_t .5,
	# loaded cargo, work with time .5.
	_select(panel._phase, 2)  # deploying
	_check("gui_phase_deploying", panel._error.text == ""
			and panel._phase_t.editable, panel._error.text)
	panel._phase_t.set_value(0.5)
	panel._phase_t.value_changed.emit(0.5)
	_select(panel._cargo, 1)  # loaded
	panel.time_line().text = "0.5"
	preview.gui_time_submitted()
	var cargo := world.get_node("EntityRoot/VisualRoot/SampleRoot/Model/Socket_Cargo/Cargo") as Node3D
	if not _check("gui_nondefault_state", panel._error.text == "" and cargo.visible
			and absf(body.position.y - home_y + 0.025) < 1e-6,
			"%s | body=%s cargo=%s" % [panel._error.text, body.position, cargo.visible]):
		return

	# Reset restores defaults everywhere
	preview.gui_reset_pressed()
	_check("gui_reset_defaults", panel.option_text("state") == "idle"
			and panel.option_text("camera") == "normal" and panel.option_text("yaw") == "0"
			and panel.option_text("labels") == "evidence" and panel.time_line().text == "0"
			and panel.option_text("phase") == "completed"
			and absf(float(panel.selection_values()["phase-t"])) < 1e-9
			and panel.option_text("cargo") == "empty"
			and absf(entity.rotation.y) < 1e-6 and absf(body.position.y - home_y) < 1e-6
			and not cargo.visible,
			"%s | body=%s cargo=%s" % [panel._error.text, body.position, cargo.visible])
	_check("gui_reset_camera", preview._camera_mode_used == "normal"
			and world.get_node("PreviewCamera").position == Vector3(18.3848, 28, 18.3848))


func _select(button: OptionButton, index: int) -> void:
	button.select(index)
	button.item_selected.emit(index)
