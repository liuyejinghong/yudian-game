extends Node3D
## Test-only probe sample wrapper for ART-PREVIEW-01 slice A.
## No game capability: poses are procedural functions of the requested preset,
## there is no AnimationPlayer, no simulation state and no authority write.
##
## Contract (requirements/preview-r1.md): root named SampleRoot carrying
## apply_preview(preset: Dictionary) -> String ("" on success), six preset keys
## only (state/phase/phase_t/cargo/reason/time_s), validate everything first,
## then reset-and-apply; illegal requests keep the previous legal pose; the
## root transform is never touched; identical input is idempotent.

const STATE_NAMES: Array[String] = ["idle", "move", "work", "charge", "disabled", "towed", "maintenance"]
const PHASE_NAMES: Array[String] = ["packed", "installed", "deploying", "completed"]
const CARGO_NAMES: Array[String] = ["empty", "loaded"]
const REASON_NAMES: Array[String] = ["none", "return_charge", "no_power", "mechanical", "both"]

const DEPLOY_SPAN_M := 0.05      # packed base sits 5 cm low, completed at 0
const MOVE_SPAN_M := 1.0         # traverse distance, matches manifest active bounds
const MOVE_DURATION_S := 2.0     # holds the end pose afterwards (non-loop)
const WORK_AMPLITUDE_M := 0.05
const WORK_PERIOD_S := 2.0
const TOWED_TILT_RAD := 0.0872665  # 5 deg, static transport pose

const _PHASE_STATIC_BASE_Y := {
	"packed": -DEPLOY_SPAN_M,
	"installed": 0.0,
	"deploying": 0.0,  # addressed by phase_t, see _phase_base_y
	"completed": 0.0,
}

# F01 phase x state matrix; the seven-state table in the manifest describes the
# completed baseline, everything non-completed is resolved through this matrix.
const _PHASE_STATE_MODES := {
	"packed": {"mode": "na", "reason": "probe packed; no state poses exist"},
	"installed": {"mode": "static", "reason": "pre-deploy static baseline"},
	"deploying": {"mode": "na", "reason": "deploy pose addressed by phase_t only"},
	"completed": {
		"idle": {"mode": "static", "reason": "rest"},
		"move": {"mode": "animated", "reason": "procedural traverse, test-only"},
		"work": {"mode": "animated", "reason": "procedural work bob, test-only"},
		"charge": {"mode": "static", "reason": "rest at pad"},
		"disabled": {"mode": "static", "reason": "inert"},
		"towed": {"mode": "static", "reason": "tilted transport pose"},
		"maintenance": {"mode": "static", "reason": "service pose"},
	},
}

var _body_home := Transform3D.IDENTITY
var _home_captured := false

@onready var _model: Node3D = $Model


func _resolve_nodes() -> Dictionary:
	var body := _model.get_node_or_null("Body") if _model != null else null
	var cargo := _model.get_node_or_null("Socket_Cargo/Cargo") if _model != null else null
	return {"body": body, "cargo": cargo}


func get_state_mode(phase: String, state: String) -> String:
	var per_phase: Dictionary = _PHASE_STATE_MODES.get(phase, {})
	if per_phase.has("mode"):
		return per_phase["mode"]
	var entry: Dictionary = per_phase.get(state, {})
	return entry.get("mode", "")


func get_na_reason(phase: String, state: String) -> String:
	var per_phase: Dictionary = _PHASE_STATE_MODES.get(phase, {})
	if per_phase.has("mode"):
		return per_phase["reason"]
	var entry: Dictionary = per_phase.get(state, {})
	return entry.get("reason", "")


func apply_preview(preset: Dictionary) -> String:
	var err := _validate(preset)
	if err != "":
		return err
	# Validation complete; from here the request is legal and applied atomically.
	if not _home_captured:
		var nodes := _resolve_nodes()
		_body_home = nodes["body"].transform
		_home_captured = true
	_reset_pose()
	_apply_pose(preset)
	return ""


func _validate(preset: Dictionary) -> String:
	for key in ["state", "phase", "phase_t", "cargo", "reason", "time_s"]:
		if not preset.has(key):
			return "preset missing key: %s" % key
	if preset.size() != 6:
		return "preset must contain exactly the six contract keys"
	# Type-check every value before any typed read; a wrong-typed preset must
	# return an error string and leave the previous legal pose untouched.
	for key in ["state", "phase", "cargo", "reason"]:
		if not (preset[key] is String):
			return "preset %s must be a String, got %s" % [key, type_string(typeof(preset[key]))]
	for key in ["phase_t", "time_s"]:
		var raw: Variant = preset[key]
		if not (raw is float or raw is int):
			return "preset %s must be a number, got %s" % [key, type_string(typeof(raw))]
		if not is_finite(float(raw)):
			return "preset %s must be finite, got %s" % [key, str(raw)]
	var state := String(preset["state"])
	var phase := String(preset["phase"])
	if not STATE_NAMES.has(state):
		if state == "offline":
			return "offline is rejected; disabled mapping is F02 display-only"
		return "unknown state: %s" % state
	if not PHASE_NAMES.has(phase):
		return "probe supports phases %s, got: %s" % [PHASE_NAMES, phase]
	var phase_t := float(preset["phase_t"])
	if phase_t < 0.0 or phase_t > 1.0:
		return "phase_t must be finite in [0,1], got: %s" % str(phase_t)
	if phase != "deploying" and phase_t != 0.0:
		return "phase_t is only meaningful for deploying, must be 0 for %s" % phase
	var cargo := String(preset["cargo"])
	if not CARGO_NAMES.has(cargo):
		return "probe supports cargo modes %s, got: %s" % [CARGO_NAMES, cargo]
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


func _reset_pose() -> void:
	var nodes := _resolve_nodes()
	var body: Node3D = nodes["body"]
	var cargo: Node3D = nodes["cargo"]
	body.transform = _body_home
	body.visible = true
	cargo.visible = false


func _apply_pose(preset: Dictionary) -> void:
	var nodes := _resolve_nodes()
	var body: Node3D = nodes["body"]
	var phase := String(preset["phase"])
	var state := String(preset["state"])
	var phase_t := float(preset["phase_t"])
	var time_s := float(preset["time_s"])

	nodes["cargo"].visible = String(preset["cargo"]) == "loaded"
	# Every phase starts from its own static/phase_t baseline; non-completed
	# phases hold that baseline for all states (the manifest marks them static
	# or na), so no run-cycle motion may leak into them.
	body.position = _body_home.origin + Vector3(0.0, _phase_base_y(phase, phase_t), 0.0)
	if phase != "completed":
		return
	match state:
		"move":
			body.position.x += MOVE_SPAN_M * clampf(time_s / MOVE_DURATION_S, 0.0, 1.0)
		"work":
			body.position.y += WORK_AMPLITUDE_M * sin(TAU * time_s / WORK_PERIOD_S)
		"towed":
			body.rotation.z = TOWED_TILT_RAD


func _phase_base_y(phase: String, phase_t: float) -> float:
	if phase == "deploying":
		return lerpf(-DEPLOY_SPAN_M, 0.0, phase_t)
	return _PHASE_STATIC_BASE_Y[phase]
