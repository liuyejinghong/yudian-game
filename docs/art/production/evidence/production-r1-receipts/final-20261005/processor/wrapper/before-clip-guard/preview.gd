extends Node3D
## Pure preview poses from the actual imported source; no gameplay rules.
const STATES := ["idle", "move", "work", "charge", "disabled", "towed", "maintenance"]
const PHASES := ["completed"]
const PART := "Model/processor-r1/Model/"
var _baseline: Dictionary = {}
var _player: AnimationPlayer

func _validate(p: Dictionary) -> String:
	for key in ["state", "phase", "phase_t", "cargo", "reason", "time_s"]:
		if not p.has(key):
			return "missing " + key
	if p.size() != 6:
		return "exactly six preset keys required"
	for key in ["state", "phase", "cargo", "reason"]:
		if not p[key] is String:
			return "invalid string " + key
	for key in ["phase_t", "time_s"]:
		if not (p[key] is int or p[key] is float) or not is_finite(float(p[key])):
			return "invalid finite number " + key
	if p.state not in STATES or p.phase not in PHASES or p.cargo != "empty":
		return "unsupported state, phase or cargo"
	if p.phase_t < 0 or p.phase_t > 1 or p.time_s < 0:
		return "time range invalid"
	if p.phase != "deploying" and p.phase_t != 0:
		return "phase_t only applies to deploying"
	if p.reason not in ["none", "return_charge", "no_power", "mechanical", "both"]:
		return "unknown reason"
	if p.reason == "return_charge" and p.state != "move":
		return "return_charge requires move"
	if p.reason in ["no_power", "mechanical", "both"] and p.state not in ["disabled", "towed", "maintenance"]:
		return "fault reason requires disabled, towed or maintenance"
	return ""

func _setup() -> String:
	if not _baseline.is_empty():
		return ""
	_player = get_node_or_null("Model/AnimationPlayer") as AnimationPlayer
	if _player == null:
		return "source AnimationPlayer missing"
	for clip in ["work", "disabled", "maintenance"]:
		if not _player.has_animation(clip):
			return "source clip missing " + clip
	for part in ["PressRam", "FeedGate", "StopGate", "ServiceCover", "InputCrate", "OutputCrate"]:
		if get_node_or_null(PART + part) == null:
			return "source part missing " + part
	for n in find_children("*", "Node3D", true, false):
		_baseline[n] = [n.transform, n.visible]
	return ""

func _seek(clip: String, t: float) -> void:
	# Imported Godot clips contain immutable defaults for other source tracks.
	# Preserve only already-evaluated source poses, never calculate new motion here.
	var affected: Array = {"work": ["PressRam", "FeedGate"], "disabled": ["StopGate"], "maintenance": ["ServiceCover"]}[clip]
	var keep: Dictionary = {}
	for n in _baseline:
		if str(get_path_to(n)).trim_prefix(PART) not in affected:
			keep[n] = n.transform
	_player.play(clip, 0)
	_player.seek(t, true)
	_player.pause()
	for n in keep:
		n.transform = keep[n]

func apply_preview(p: Dictionary) -> String:
	var err := _validate(p)
	if err != "":
		return err
	err = _setup()
	if err != "":
		return err
	_player.stop()
	for n in _baseline:
		n.transform = _baseline[n][0]
		n.visible = _baseline[n][1]
	if p.state == "work":
		_seek("work", fmod(float(p.time_s), 2.0))
	elif p.state in ["disabled", "maintenance"]:
		_seek(p.state, minf(float(p.time_s), 1.0))
	return ""
