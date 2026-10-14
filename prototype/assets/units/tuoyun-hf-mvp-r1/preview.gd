extends Node3D
## Tutorial HF geometry candidate. Root, sockets and simulation stay external.
## Five clips are exact native tracks copied from the verified tuoyun-r1 GLB.
## Every source Node3D is reset before deterministic absolute-time sampling;
## invalid presets/dependencies return an error before touching the legal pose.

const STATE_NAMES: Array[String] = ["idle", "move", "work", "charge", "disabled", "towed", "maintenance"]
const PHASE_NAMES: Array[String] = ["completed"]
const CARGO_NAMES: Array[String] = ["empty", "loaded"]
const REASON_NAMES: Array[String] = ["none", "return_charge", "no_power", "mechanical", "both"]
const ANIM_PLAYER_PATH := "Model/AnimationPlayer"
const GEOMETRY_PREFIX := "Model/tuoyun-r1/Model"
const CARGO_PATH := GEOMETRY_PREFIX + "/CargoBox"
const TOW_ROD_PATH := GEOMETRY_PREFIX + "/TowRod"
const CLIP_DURATION_S := 1.0
const _CLIP_BY_STATE := {
	"move": "move", "work": "work", "charge": "charge", "disabled": "disabled",
	"maintenance": "maintenance", "towed": "disabled",
}

var _anim: AnimationPlayer
var _rest := {}
var _rest_captured := false


func apply_preview(preset: Dictionary) -> String:
	var err := _validate(preset)
	if err != "":
		return err
	var player := get_node_or_null(ANIM_PLAYER_PATH) as AnimationPlayer
	var cargo := get_node_or_null(CARGO_PATH) as Node3D
	var rod := get_node_or_null(TOW_ROD_PATH) as Node3D
	if player == null:
		return "missing animation player node: %s" % ANIM_PLAYER_PATH
	if cargo == null:
		return "missing cargo node: %s" % CARGO_PATH
	if rod == null:
		return "missing verified tow rod node: %s" % TOW_ROD_PATH
	var animation_root := player.get_node_or_null(player.root_node)
	if animation_root == null:
		return "missing animation root: %s" % player.root_node
	for clip in ["move", "work", "charge", "disabled", "maintenance"]:
		if not player.has_animation(clip):
			return "missing verified source clip: " + clip
		var animation := player.get_animation(clip)
		for track in animation.get_track_count():
			var target := NodePath(animation.track_get_path(track).get_concatenated_names())
			if animation_root.get_node_or_null(target) == null:
				return "missing animation target: %s/%s" % [clip, target]
	_anim = player
	if not _rest_captured:
		_capture_rest()
	_reset_pose()
	_apply_pose(preset, cargo, rod)
	return ""


func _validate(preset: Dictionary) -> String:
	for key in ["state", "phase", "phase_t", "cargo", "reason", "time_s"]:
		if not preset.has(key):
			return "preset missing key: %s" % key
	if preset.size() != 6:
		return "preset must contain exactly the six contract keys"
	for key in ["state", "phase", "cargo", "reason"]:
		if not (preset[key] is String):
			return "preset %s must be a String, got %s" % [key, type_string(typeof(preset[key]))]
	for key in ["phase_t", "time_s"]:
		var raw: Variant = preset[key]
		if not (raw is float or raw is int):
			return "preset %s must be a number, got %s" % [key, type_string(typeof(raw))]
		if not is_finite(float(raw)):
			return "preset %s must be finite, got: %s" % [key, str(raw)]
	var state := String(preset["state"])
	var phase := String(preset["phase"])
	if not STATE_NAMES.has(state):
		if state == "offline":
			return "offline is rejected; the disabled mapping is F02 display-only"
		return "unknown state: %s" % state
	if not PHASE_NAMES.has(phase):
		return "U01 supports phases %s, got: %s" % [PHASE_NAMES, phase]
	var phase_t := float(preset["phase_t"])
	if phase_t < 0.0 or phase_t > 1.0:
		return "phase_t must be finite in [0,1], got: %s" % str(phase_t)
	if phase_t != 0.0:
		return "phase_t must be 0 for completed"
	var cargo := String(preset["cargo"])
	if not CARGO_NAMES.has(cargo):
		return "U01 supports cargo modes %s, got: %s" % [CARGO_NAMES, cargo]
	var reason := String(preset["reason"])
	if not REASON_NAMES.has(reason):
		return "unknown reason: %s" % reason
	var reason_err := _reason_error(state, reason)
	if reason_err != "":
		return reason_err
	var time_s := float(preset["time_s"])
	if time_s < 0.0:
		return "time_s must be finite and non-negative, got: %s" % str(time_s)
	return ""


func _reason_error(state: String, reason: String) -> String:
	match reason:
		"none":
			return ""
		"return_charge":
			if state == "move":
				return ""
			return "reason return_charge only applies to move, got state %s" % state
		_:
			if state in ["disabled", "towed", "maintenance"]:
				return ""
			return "reason %s only applies to disabled/towed/maintenance, got state %s" % [reason, state]


func _capture_rest() -> void:
	var prefix := get_node_or_null(GEOMETRY_PREFIX) as Node3D
	_rest[prefix] = prefix.transform
	for node in prefix.find_children("*", "Node3D", true, false):
		_rest[node] = node.transform
	_rest_captured = true


func _reset_pose() -> void:
	_anim.stop()
	for node: Node3D in _rest:
		node.transform = _rest[node]
		node.visible = true


func _apply_pose(preset: Dictionary, cargo: Node3D, rod: Node3D) -> void:
	var state := String(preset["state"])
	var time_s := float(preset["time_s"])
	cargo.visible = String(preset["cargo"]) == "loaded"
	rod.visible = state == "towed"
	var clip: String = _CLIP_BY_STATE.get(state, "")
	if clip == "":
		return
	_anim.play(clip)
	var t := fmod(time_s, CLIP_DURATION_S) if state == "move" else minf(time_s, CLIP_DURATION_S)
	if state == "towed":
		t = CLIP_DURATION_S
	_anim.seek(t, true)
	_anim.pause()
