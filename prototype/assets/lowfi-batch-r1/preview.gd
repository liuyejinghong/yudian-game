extends Node3D
## Private low-fidelity source-animation preview; never changes game facts.
@export var asset_key: String = ""
const STATES := ["idle", "move", "work", "charge", "disabled", "towed", "maintenance"]
const REQUIRED := {
	"wangshan": ["move", "work", "charge", "disabled", "maintenance"],
	"zhulei": ["move", "work", "charge", "disabled", "maintenance"],
	"repair": ["work", "disabled", "maintenance"],
	"lander": ["work", "disabled", "maintenance"],
	"storage": ["disabled", "maintenance"],
	"charger": ["work", "disabled", "maintenance"],
	"crate": [], "recovery": [],
	"terrain-original": [], "terrain-flat": [], "terrain-dug": [],
}
var _baseline: Dictionary = {}
var _player: AnimationPlayer

func _validate(p: Dictionary) -> String:
	for key in ["state", "phase", "phase_t", "cargo", "reason", "time_s"]:
		if not p.has(key): return "missing " + key
	if p.size() != 6: return "exactly six preset keys required"
	for key in ["state", "phase", "cargo", "reason"]:
		if not p[key] is String: return "invalid string " + key
	for key in ["phase_t", "time_s"]:
		if not (p[key] is int or p[key] is float) or not is_finite(float(p[key])):
			return "invalid finite number " + key
	if not REQUIRED.has(asset_key): return "unknown asset " + asset_key
	if p.state not in STATES or p.phase != "completed" or p.phase_t != 0 or p.time_s < 0:
		return "unsupported state, phase or time range"
	var cargo_modes := ["empty", "loaded"] if asset_key in ["storage", "lander"] else ["empty"]
	if p.cargo not in cargo_modes: return "unsupported cargo"
	if p.reason not in ["none", "return_charge", "no_power", "mechanical", "both"]: return "unknown reason"
	if p.reason == "return_charge" and p.state != "move": return "return_charge requires move"
	if p.reason in ["no_power", "mechanical", "both"] and p.state not in ["disabled", "towed", "maintenance"]:
		return "fault reason requires disabled, towed or maintenance"
	return ""

func _setup() -> String:
	var model := get_node_or_null("Model")
	if model == null: return "actual Model missing"
	var players := model.find_children("*", "AnimationPlayer", true, false)
	if not REQUIRED[asset_key].is_empty():
		if players.size() != 1: return "one source AnimationPlayer required"
		_player = players[0] as AnimationPlayer
		for clip in REQUIRED[asset_key]:
			if not _player.has_animation(clip): return "source clip missing " + clip
			if _player.get_animation(clip).length <= 0: return "source animation has no duration " + clip
	if asset_key in ["storage", "lander"]:
		var name := "RackPayloadDemo" if asset_key == "storage" else "LanderPayloadDemo"
		if model.find_child(name, true, false) == null: return "source payload missing " + name
	if asset_key in ["zhulei", "wangshan"] and get_node_or_null("TowDemo") == null:
		return "candidate TowDemo missing"
	if _baseline.is_empty():
		for n in find_children("*", "Node3D", true, false): _baseline[n] = [n.transform, n.visible]
	return ""

func apply_preview(p: Dictionary) -> String:
	var error := _validate(p)
	if error != "": return error
	error = _setup()
	if error != "": return error
	# Validation and all required source lookups finish before any mutation.
	if _player: _player.stop()
	for n in _baseline:
		n.transform = _baseline[n][0]
		n.visible = _baseline[n][1]
	var state: String = p.state
	var robot := asset_key in ["zhulei", "wangshan"]
	var clip: String = "disabled" if robot and state == "towed" else state
	if clip in REQUIRED[asset_key]:
		var animation := _player.get_animation(clip)
		var duration := animation.length
		var looping := clip == "move" or clip == "work" and asset_key != "lander"
		var t := fmod(float(p.time_s), duration) if looping else minf(float(p.time_s), duration)
		if state == "towed": t = duration
		_player.play(clip, 0)
		_player.seek(t, true)
		_player.pause()
	if asset_key in ["storage", "lander"]:
		get_node("Model").find_child("RackPayloadDemo" if asset_key == "storage" else "LanderPayloadDemo", true, false).visible = p.cargo == "loaded"
	if robot: get_node("TowDemo").visible = state == "towed"
	return ""
