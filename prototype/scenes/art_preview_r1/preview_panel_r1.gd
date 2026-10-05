extends PanelContainer
## ART-PREVIEW-01 slice B: interactive GUI panel. Lives in a CanvasLayer of the
## scene root, OUTSIDE the capture SubViewport. Exposes exactly the contract
## controls (camera / yaw / state / phase / phase_t / cargo / reason / time /
## labels / Reset) and funnels every value through the same validation the CLI
## uses (owner calls _typed_params on the assembled strings).
##
## Empty or illegal input keeps the last legal pose: the owner reports the
## error, the offending control reverts, the pose does not change.

var controller: Object = null
var is_board := false

var _camera: OptionButton
var _yaw: OptionButton
var _state: OptionButton
var _phase: OptionButton
var _phase_t: HSlider
var _phase_t_value: Label
var _cargo: OptionButton
var _reason: OptionButton
var _time: LineEdit
var _labels: OptionButton
var _reset: Button
var _status: Label
var _error: Label
var _title: Label

const YAW_VALUES := [0, 90, 180]


## opts: {title, cameras, states, phases, cargos, reasons, board}
func setup(opts: Dictionary, owner_controller: Object) -> void:
	controller = owner_controller
	is_board = bool(opts.get("board", false))
	custom_minimum_size = Vector2(360, 0)
	var vbox := VBoxContainer.new()
	vbox.name = "Controls"
	add_child(vbox)

	_title = _add_label(vbox, String(opts.get("title", "")))
	_title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART

	_camera = _add_option(vbox, "相机 camera", opts.get("cameras", ["normal", "close"]))
	_yaw = _add_option(vbox, "朝向 yaw", ["0", "90", "180"])
	_state = _add_option(vbox, "状态 state", opts.get("states", []))
	_phase = _add_option(vbox, "阶段 phase", opts.get("phases", []))
	_phase_t = _add_slider(vbox, "进度 phase_t")
	_cargo = _add_option(vbox, "货箱 cargo", opts.get("cargos", []))
	_reason = _add_option(vbox, "原因 reason", opts.get("reasons", []))

	var time_row := HBoxContainer.new()
	vbox.add_child(_add_caption("时间 time (秒, 回车应用)"))
	_time = LineEdit.new()
	_time.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var apply_time := Button.new()
	apply_time.text = "应用"
	time_row.add_child(_time)
	time_row.add_child(apply_time)
	vbox.add_child(time_row)
	_time.placeholder_text = "0"
	_time.text = "0"
	_time.text_submitted.connect(_on_time_submitted)
	apply_time.pressed.connect(_on_time_submitted.bind(""))

	_labels = _add_option(vbox, "标注 labels", ["evidence", "blind"])
	_reset = Button.new()
	_reset.text = "Reset 恢复默认"
	_reset.pressed.connect(func() -> void: controller.gui_reset_pressed())
	vbox.add_child(_reset)
	_status = _add_label(vbox, "")
	_error = _add_label(vbox, "")
	_error.add_theme_color_override("font_color", Color(1.0, 0.4, 0.3))
	_error.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART

	_wire(_camera, "camera")
	_wire(_yaw, "yaw")
	_wire(_state, "state")
	_wire(_phase, "phase")
	_phase_t.value_changed.connect(func(_v: float) -> void:
		_phase_t_value.text = "%.2f" % _phase_t.value
		controller.gui_selection_changed("phase_t"))
	_wire(_cargo, "cargo")
	_wire(_reason, "reason")
	_labels.item_selected.connect(func(_i: int) -> void:
		controller.gui_selection_changed("labels"))

	if is_board:
		# Board mode freezes the object state and camera; labels/Reset stay
		# usable. Only Button-derived controls have `disabled`; LineEdit and
		# HSlider use `editable` (ClassDB probe 2026-10-04).
		for control in [_camera, _yaw, _state, _phase, _cargo, _reason, apply_time]:
			(control as BaseButton).disabled = true
		_time.editable = false
		_phase_t.editable = false


func _wire(button: OptionButton, key: String) -> void:
	button.item_selected.connect(func(_i: int) -> void: controller.gui_selection_changed(key))


func _add_caption(text: String) -> Label:
	var label := Label.new()
	label.text = text
	return label


func _add_label(vbox: VBoxContainer, text: String) -> Label:
	var label := _add_caption(text)
	vbox.add_child(label)
	return label


func _add_option(vbox: VBoxContainer, caption: String, values: Array) -> OptionButton:
	vbox.add_child(_add_caption(caption))
	var button := OptionButton.new()
	for v: Variant in values:
		button.add_item(String(v))
	vbox.add_child(button)
	return button


func _add_slider(vbox: VBoxContainer, caption: String) -> HSlider:
	vbox.add_child(_add_caption(caption))
	var row := HBoxContainer.new()
	var slider := HSlider.new()
	slider.min_value = 0.0
	slider.max_value = 1.0
	slider.step = 0.01
	slider.custom_minimum_size = Vector2(220, 20)
	slider.value = 0.0
	_phase_t_value = Label.new()
	_phase_t_value.text = "0.00"
	row.add_child(slider)
	row.add_child(_phase_t_value)
	vbox.add_child(row)
	slider.editable = false
	return slider


func _on_time_submitted(_text: String) -> void:
	controller.gui_time_submitted()


## Current control values as CLI-style strings, ready for _typed_params.
func selection_values() -> Dictionary:
	return {
		"camera": _camera.get_item_text(_camera.selected),
		"yaw": _yaw.get_item_text(_yaw.selected),
		"state": _state.get_item_text(_state.selected),
		"phase": _phase.get_item_text(_phase.selected),
		"phase-t": "%.2f" % _phase_t.value,
		"cargo": _cargo.get_item_text(_cargo.selected),
		"reason": _reason.get_item_text(_reason.selected),
		"time": _time.text,
		"labels": _labels.get_item_text(_labels.selected),
	}


## Apply a validated preset dict (used for Reset and initial state).
## The slider is set with set_value_no_signal so back-filling defaults cannot
## re-enter the controller's value_changed handler mid-reset.
func set_selection(p: Dictionary) -> void:
	_camera.select(_index_of(_camera, String(p["camera"])))
	_yaw.select(_index_of(_yaw, str(int(p["yaw"]))))
	_state.select(_index_of(_state, String(p["state"])))
	_phase.select(_index_of(_phase, String(p["phase"])))
	_phase_t.set_value_no_signal(float(p["phase_t"]))
	_phase_t.editable = String(p["phase"]) == "deploying"
	_phase_t_value.text = "%.2f" % float(p["phase_t"])
	_cargo.select(_index_of(_cargo, String(p["cargo"])))
	_reason.select(_index_of(_reason, String(p["reason"])))
	_time.text = _format_number(float(p["time_s"]))
	_labels.select(_index_of(_labels, String(p["labels"])))


func set_time_text(text: String) -> void:
	_time.text = text


func time_line() -> LineEdit:
	return _time


func set_phase_t_enabled(enabled: bool) -> void:
	_phase_t.editable = enabled


func set_error(msg: String) -> void:
	_error.text = "" if msg == "" else "错误: %s" % msg


func set_status(msg: String) -> void:
	_status.text = msg


func set_title(msg: String) -> void:
	_title.text = msg


## Restore a control to the last legal value after a rejected change.
func revert_option(key: String, value_text: String) -> void:
	match key:
		"camera":
			_camera.select(_index_of(_camera, value_text))
		"yaw":
			_yaw.select(_index_of(_yaw, value_text))
		"state":
			_state.select(_index_of(_state, value_text))
		"phase":
			_phase.select(_index_of(_phase, value_text))
			_phase_t.editable = value_text == "deploying"
		"cargo":
			_cargo.select(_index_of(_cargo, value_text))
		"reason":
			_reason.select(_index_of(_reason, value_text))
		"labels":
			_labels.select(_index_of(_labels, value_text))


func revert_time(text: String) -> void:
	_time.text = text


func option_text(key: String) -> String:
	var button: OptionButton
	match key:
		"camera": button = _camera
		"yaw": button = _yaw
		"state": button = _state
		"phase": button = _phase
		"cargo": button = _cargo
		"reason": button = _reason
		"labels": button = _labels
		_:
			return ""
	return button.get_item_text(button.selected)


static func _format_number(value: float) -> String:
	if is_equal_approx(value, float(int(value))):
		return str(int(value))
	return String.num(value, 6)


func _index_of(button: OptionButton, text: String) -> int:
	for i in button.item_count:
		if button.get_item_text(i) == text:
			return i
	return 0
