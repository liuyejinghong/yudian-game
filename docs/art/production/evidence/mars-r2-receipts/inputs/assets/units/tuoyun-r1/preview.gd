extends Node3D
## ART-U01-STATE-B private wrapper for the tuoyun-r1 hauling sample.
## Contract: docs/art/production/requirements/preview-r1.md + ART-U01 ticket.
## Root SampleRoot carries the real GLB instance "Model" and exposes
## apply_preview(preset: Dictionary) -> String ("" on success). The full
## six-key preset is validated before any object is touched; an illegal
## request returns a non-empty error and keeps the previous legal pose.
## Every apply fully resets all source Node3D transforms/visibility and stops
## all animations, then seeks the requested absolute time and pauses, so the
## same input always yields the same pose, nothing accumulates and the root
## never moves. No substitute geometry, no mesh-wide material covers.

const STATE_NAMES: Array[String] = ["idle", "move", "work", "charge", "disabled", "towed", "maintenance"]
const PHASE_NAMES: Array[String] = ["completed"]  # U01 only; packed/installed/deploying are F01
const CARGO_NAMES: Array[String] = ["empty", "loaded"]
const REASON_NAMES: Array[String] = ["none", "return_charge", "no_power", "mechanical", "both"]

# Real imported paths: the glTF root node "tuoyun-r1" sits under the instanced
# scene root "Model" and the importer places the AnimationPlayer beside it.
const ANIM_PLAYER_PATH := "Model/AnimationPlayer"
const GEOMETRY_PREFIX := "Model/tuoyun-r1/Model"
const CARGO_PATH := GEOMETRY_PREFIX + "/CargoBox"
const TOW_ROD_PATH := GEOMETRY_PREFIX + "/TowRod"

const CLIP_DURATION_S := 1.0
# Real imported clips; towed reuses the disabled clip pinned to its end pose
# (static transport indication, declared static in the manifest).
const _CLIP_BY_STATE := {
	"move": "move",
	"work": "work",
	"charge": "charge",
	"disabled": "disabled",
	"maintenance": "maintenance",
	"towed": "disabled",
}

var _anim: AnimationPlayer
var _rest := {}  # Node3D -> rest Transform3D, captured before the first apply
var _rest_captured := false


func apply_preview(preset: Dictionary) -> String:
	var err := _validate(preset)
	if err != "":
		return err
	_anim = get_node_or_null(ANIM_PLAYER_PATH)
	if _anim == null:
		return "missing animation player node: %s" % ANIM_PLAYER_PATH
	var cargo: Node3D = get_node_or_null(CARGO_PATH)
	var rod: Node3D = get_node_or_null(TOW_ROD_PATH)
	if cargo == null:
		return "missing cargo node: %s" % CARGO_PATH
	if rod == null:
		return "missing tow rod node: %s" % TOW_ROD_PATH
	for clip in ["move", "work", "charge", "disabled", "maintenance"]:
		if not _anim.has_animation(clip):
			return "missing source clip: " + clip
	if not _rest_captured:
		_capture_rest()
	# Validation and node resolution complete; apply atomically.
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
	if phase != "deploying" and phase_t != 0.0:
		return "phase_t is only meaningful for deploying, must be 0 for %s" % phase
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
	for node: Node3D in _geometry_nodes():
		_rest[node] = node.transform
	_rest_captured = true


func _geometry_nodes() -> Array[Node3D]:
	var found: Array[Node3D] = []
	var prefix := get_node_or_null(GEOMETRY_PREFIX)
	if prefix != null:
		for node in prefix.find_children("*", "Node3D", true, false):
			found.append(node)
		if prefix is Node3D:
			found.push_front(prefix)
	return found


## Full reset: stop every animation first, then restore every tracked source
## transform and set all nodes visible; rod/cargo visibility is re-applied
## from the preset below (the GLB extras visible flag is not adopted by the
## Godot importer, so the rod must be hidden explicitly here).
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
		return  # idle: rest pose
	_anim.play(clip)
	var t := fmod(time_s, CLIP_DURATION_S) if state == "move" else minf(time_s, CLIP_DURATION_S)
	if state == "towed":
		t = CLIP_DURATION_S
	_anim.seek(t, true)
	_anim.pause()
