extends Node3D
## ART-PREVIEW-01: independent preview entry — manifest loading, per-surface
## material binding, the six-role material trial board (slice A) plus the
## slice B view stack: real 1920x1200 SubViewport (MSAA4 / scale 1 / no FXAA /
## no TAA / no upscaling), frozen cameras / light / environment, yaw, the
## interactive GUI, evidence/blind annotation overlay and deterministic PNG+JSON
## capture.
##
## Contract: requirements/preview-r1.md (frozen). Local GDScript only; no
## project configuration, shared material or game-system changes.

const ViewR1 := preload("res://scenes/art_preview_r1/preview_view_r1.gd")
const OverlayScript := preload("res://scenes/art_preview_r1/preview_overlay_r1.gd")
const PanelScript := preload("res://scenes/art_preview_r1/preview_panel_r1.gd")
const Capture := preload("res://scenes/art_preview_r1/preview_capture_r1.gd")

const MATERIAL_DIR := "res://assets/materials/art-r1"
const LOG_TAG := "[ART_PREVIEW_R1]"

const STATE_NAMES := ["idle", "move", "work", "charge", "disabled", "towed", "maintenance"]
const F01_PHASES := ["packed", "installed", "deploying", "completed"]
const CARGO_NAMES := ["empty", "loaded"]
const REASON_NAMES := ["none", "return_charge", "no_power", "mechanical", "both"]
const ROLE_NAMES := ["body_light", "frame_dark", "rubber", "accent_warm", "solar_face", "soil_mars"]

# Frozen r1 material table (materials/art-r1 README): sRGB albedo as bytes/255.
const ROLE_TABLE := {
	"body_light": {"albedo": [0.847059, 0.862745, 0.839216], "roughness": 0.65, "metallic": 0.0},
	"frame_dark": {"albedo": [0.219608, 0.270588, 0.294118], "roughness": 0.75, "metallic": 0.2},
	"rubber": {"albedo": [0.141176, 0.160784, 0.168627], "roughness": 0.9, "metallic": 0.0},
	"accent_warm": {"albedo": [0.843137, 0.482353, 0.239216], "roughness": 0.6, "metallic": 0.0},
	"solar_face": {"albedo": [0.12549, 0.231373, 0.321569], "roughness": 0.35, "metallic": 0.1},
	"soil_mars": {"albedo": [0.596078, 0.407843, 0.301961], "roughness": 0.95, "metallic": 0.0},
}

# Trial board: 6 groups, group centers X=-1.2/0/1.2, Z=-0.8/+0.8; the group X
# stays at the center, sphere offset -0.35 m and cube +0.35 m in Z so the 0.6 m
# bodies never intersect neighboring columns (master-checked real board layout).
const BOARD_GROUPS := [
	["body_light", -1.2, -0.8], ["frame_dark", 0.0, -0.8], ["rubber", 1.2, -0.8],
	["accent_warm", -1.2, 0.8], ["solar_face", 0.0, 0.8], ["soil_mars", 1.2, 0.8],
]
const BOARD_GROUP_OFFSET := 0.35
const BOARD_BODY_Y := 0.3

const VALUE_KEYS := ["manifest", "camera", "yaw", "state", "phase", "phase-t",
		"cargo", "reason", "time", "labels", "capture-dir"]
const FLAG_KEYS := ["material-board", "self-test"]

var params := {}
var _st_results: Array = []
var _st_base_manifest: Dictionary = {}
var _tmp_dir := ""

# --- slice B state ---
## Set by the test-only GUI probe before the scene enters the tree: build the
## world + GUI from the preset params without any CLI parsing or auto-quit.
var gui_probe_mode := false
var _world: SubViewport
var _cam: Camera3D
var _sun: DirectionalLight3D
var _wenv: WorldEnvironment
var _overlay  # preview_overlay_r1.gd instance (Control)
var _panel  # preview_panel_r1.gd instance (PanelContainer)
var _camera_mode_used := ""
var _wrapper: Node
var _data := {}
var _applied := {}
var _last_legal_text := {}


func _ready() -> void:
	print("%s engine=%s adapter=%s" % [LOG_TAG, Engine.get_version_info()["string"],
			RenderingServer.get_video_adapter_name()])
	if gui_probe_mode:
		_prepare_interactive()
		return
	var parsed := parse_args(OS.get_cmdline_user_args())
	if parsed["error"] != "":
		printerr("%s CLI ERROR: %s" % [LOG_TAG, parsed["error"]])
		get_tree().quit(1)
		return
	params = parsed["params"]
	if params["self_test"]:
		var ok: bool = _self_test()
		get_tree().quit(0 if ok else 1)
		return
	if params["capture_dir"] != "":
		if DisplayServer.get_name() == "headless":
			printerr("%s CAPTURE REFUSED: capture needs a real rendered window; headless would produce no usable image (exit 1, nothing written)" % LOG_TAG)
			get_tree().quit(1)
			return
		await _run_capture()
		return
	if DisplayServer.get_name() == "headless":
		get_tree().quit(_run_once())
		return
	_prepare_interactive()


# ---------------------------------------------------------------- CLI parsing

func parse_args(argv: Array) -> Dictionary:
	var values := {}
	var flags := {}
	var i := 0
	while i < argv.size():
		var arg := String(argv[i])
		if not arg.begins_with("--"):
			return _cli_err("unexpected argument, only --key value forms are accepted: %s" % arg)
		var key := arg.substr(2)
		if key in FLAG_KEYS:
			if flags.has(key):
				return _cli_err("duplicate CLI key: --%s" % key)
			flags[key] = true
			i += 1
			continue
		if not (key in VALUE_KEYS):
			return _cli_err("unknown CLI key: --%s" % key)
		if values.has(key):
			return _cli_err("duplicate CLI key: --%s" % key)
		if i + 1 >= argv.size() or String(argv[i + 1]) == "" or String(argv[i + 1]).begins_with("--"):
			return _cli_err("missing value for --%s" % key)
		values[key] = String(argv[i + 1])
		i += 2

	if values.has("manifest") and flags.has("material-board"):
		return _cli_err("--manifest and --material-board are mutually exclusive, choose one")
	if not values.has("manifest") and not flags.has("material-board"):
		return _cli_err("choose exactly one of --manifest <path> or --material-board")

	var typed := _typed_params(values)
	if typed["error"] != "":
		return {"error": typed["error"], "params": {}}
	var p: Dictionary = typed["params"]
	p["self_test"] = flags.has("self-test")
	p["mode"] = "board" if flags.has("material-board") else "manifest"
	p["manifest_path"] = values.get("manifest", "")
	p["capture_dir"] = values.get("capture-dir", "")

	if p["mode"] == "board":
		for key in [["state", "idle"], ["phase", "completed"], ["cargo", "empty"], ["reason", "none"]]:
			if values.has(key[0]) and values[key[0]] != key[1]:
				return _cli_err("material-board mode only allows the default object state, got --%s %s" % [key[0], values[key[0]]])
		if p["phase_t"] != 0.0 or p["time_s"] != 0.0:
			return _cli_err("material-board mode only allows phase-t 0 and time 0")

	return {"error": "", "params": p}


## Value validation shared verbatim by the CLI (parse_args) and the GUI panel:
## the GUI assembles the same string values and must pass through this single
## function, so both entry points reject exactly the same inputs.
func _typed_params(values: Dictionary) -> Dictionary:
	var p := {}
	p["camera"] = values.get("camera", "normal")
	if not (p["camera"] in ["normal", "close"]):
		return _cli_err("--camera must be normal|close, got: %s" % p["camera"])

	var yaw_text: String = values.get("yaw", "0")
	if not yaw_text.is_valid_int():
		return _cli_err("--yaw must be an integer, got: %s" % yaw_text)
	p["yaw"] = int(yaw_text)
	if not (p["yaw"] in [0, 90, 180]):
		return _cli_err("--yaw must be 0|90|180, got: %d" % p["yaw"])

	p["state"] = values.get("state", "idle")
	if p["state"] == "offline":
		return _cli_err("offline is rejected; the disabled mapping is F02 display-only")
	if not (p["state"] in STATE_NAMES):
		return _cli_err("unknown state (exact case): %s" % p["state"])

	p["phase"] = values.get("phase", "completed")
	if not (p["phase"] in F01_PHASES):
		return _cli_err("unknown phase: %s" % p["phase"])

	var probe := _cli_number(values, "phase-t", "0")
	if probe["error"] != "":
		return probe
	p["phase_t"] = probe["value"]
	if p["phase_t"] < 0.0 or p["phase_t"] > 1.0:
		return _cli_err("--phase-t must be in [0,1], got: %s" % str(p["phase_t"]))
	if p["phase"] != "deploying" and p["phase_t"] != 0.0:
		return _cli_err("--phase-t is only meaningful for deploying and must be 0 for %s" % p["phase"])

	p["cargo"] = values.get("cargo", "empty")
	if not (p["cargo"] in CARGO_NAMES):
		return _cli_err("--cargo must be empty|loaded, got: %s" % p["cargo"])

	p["reason"] = values.get("reason", "none")
	if not (p["reason"] in REASON_NAMES):
		return _cli_err("unknown reason: %s" % p["reason"])
	var reason_err := _reason_pairing_error(p["state"], p["reason"])
	if reason_err != "":
		return _cli_err(reason_err)

	probe = _cli_number(values, "time", "0")
	if probe["error"] != "":
		return probe
	p["time_s"] = probe["value"]
	if p["time_s"] < 0.0:
		return _cli_err("--time must be non-negative, got: %s" % str(p["time_s"]))

	p["labels"] = values.get("labels", "evidence")
	if not (p["labels"] in ["evidence", "blind"]):
		return _cli_err("--labels must be evidence|blind, got: %s" % p["labels"])

	return {"error": "", "params": p}


func _cli_number(values: Dictionary, key: String, default_text: String) -> Dictionary:
	var text: String = values.get(key, default_text)
	if not text.is_valid_float():
		return {"error": "--%s is not a valid number: %s" % [key, text], "value": 0.0}
	var v := text.to_float()
	if not is_finite(v):
		return {"error": "--%s must be finite, got: %s" % [key, text], "value": 0.0}
	return {"error": "", "value": v}


func _reason_pairing_error(state: String, reason: String) -> String:
	match reason:
		"none":
			return ""
		"return_charge":
			return "" if state == "move" else "reason return_charge only applies to move, got state %s" % state
		_:
			if state in ["disabled", "towed", "maintenance"]:
				return ""
			return "reason %s only applies to disabled/towed/maintenance, got state %s" % [reason, state]


func _cli_err(msg: String) -> Dictionary:
	return {"error": msg, "params": {}}


# ------------------------------------------------------------------ manifest

func _load_manifest(path: String) -> Dictionary:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return _m_err(path, "cannot open manifest (%s)" % error_string(FileAccess.get_open_error()))
	var text := f.get_as_text()
	f.close()
	if text.strip_edges().is_empty():
		return _m_err(path, "manifest file is empty")
	var parsed: Variant = JSON.parse_string(text)
	if parsed == null:
		return _m_err(path, "manifest is not valid JSON")
	if not (parsed is Dictionary):
		return _m_err(path, "manifest root is not a JSON object")
	var d: Dictionary = parsed

	for key in ["id", "preview_scene", "glb", "glb_sha256"]:
		if not (d.get(key) is String) or (d[key] as String).is_empty():
			return _m_err(path, "missing or empty string field: %s" % key)
	if not _is_num(d.get("revision")) or float(d["revision"]) < 1.0 or fmod(float(d["revision"]), 1.0) != 0.0:
		return _m_err(path, "revision must be a positive integer")
	if d.get("unit") != "meter":
		return _m_err(path, "unit must be \"meter\"")
	if d.get("up") != "+Y":
		return _m_err(path, "up must be \"+Y\"")
	if d.get("forward") != "-Z":
		return _m_err(path, "forward must be \"-Z\"")
	if not (d["preview_scene"] as String).begins_with("res://"):
		return _m_err(path, "preview_scene must be a res:// path")
	if not (d["glb"] as String).begins_with("res://"):
		return _m_err(path, "glb must be a res:// path")
	var sha := String(d["glb_sha256"])
	if sha.length() != 64 or not _is_hex(sha):
		return _m_err(path, "glb_sha256 must be 64 hex characters")

	var bounds: Variant = d.get("bounds")
	if not (bounds is Dictionary):
		return _m_err(path, "bounds must be an object")
	for k in ["static", "loaded", "active"]:
		var box: Variant = bounds.get(k)
		if not (box is Dictionary) or not (box.get("min") is Array) or not (box.get("max") is Array):
			return _m_err(path, "bounds.%s needs min/max arrays" % k)
		for e in ["min", "max"]:
			var arr: Array = box[e]
			if arr.size() != 3:
				return _m_err(path, "bounds.%s.%s must have 3 components" % [k, e])
			for c in arr:
				if not _is_num(c):
					return _m_err(path, "bounds.%s.%s has a non-finite component" % [k, e])

	var bindings: Variant = d.get("material_bindings")
	if not (bindings is Array) or bindings.is_empty():
		return _m_err(path, "material_bindings must be a non-empty array")
	var seen := {}
	for b: Variant in bindings:
		if not (b is Dictionary):
			return _m_err(path, "material_bindings entries must be objects")
		var node := String(b.get("node", ""))
		if node.is_empty():
			return _m_err(path, "material_bindings entry missing node")
		if not _is_num(b.get("surface")) or float(b["surface"]) < 0.0 or fmod(float(b["surface"]), 1.0) != 0.0:
			return _m_err(path, "material_bindings entry has non-integer surface: %s" % node)
		if not (String(b.get("role", "")) in ROLE_NAMES):
			return _m_err(path, "material_bindings entry has unknown role: %s" % String(b.get("role", "")))
		var dup_key := "%s|%d" % [node, int(b["surface"])]
		if seen.has(dup_key):
			return _m_err(path, "duplicate material binding: %s surface %d" % [node, int(b["surface"])])
		seen[dup_key] = true

	var sockets: Variant = d.get("sockets")
	if not (sockets is Array):
		return _m_err(path, "sockets must be an array")
	for s: Variant in sockets:
		if not (s is Dictionary) or String(s.get("node", "")).is_empty():
			return _m_err(path, "sockets entries need a node")
		for e in ["position", "forward", "up"]:
			var arr: Variant = s.get(e)
			if not (arr is Array) or arr.size() != 3:
				return _m_err(path, "socket %s missing %s[3]" % [String(s.get("node", "")), e])
			for c in arr:
				if not _is_num(c):
					return _m_err(path, "socket %s has non-finite %s" % [String(s.get("node", "")), e])

	var states: Variant = d.get("states")
	if not (states is Dictionary):
		return _m_err(path, "states must be an object")
	if states.size() != STATE_NAMES.size():
		return _m_err(path, "states must contain exactly the seven contract states")
	for sname in STATE_NAMES:
		var st: Variant = states.get(sname)
		if not (st is Dictionary):
			return _m_err(path, "states missing entry: %s" % sname)
		if not (String(st.get("mode", "")) in ["static", "animated", "na"]):
			return _m_err(path, "state %s has invalid mode" % sname)
		if not (st.get("reason") is String):
			return _m_err(path, "state %s reason must be a string" % sname)
		var clip: Variant = st.get("clip")
		if not (clip == null or (clip is String and not (clip as String).is_empty())):
			return _m_err(path, "state %s clip must be null or a non-empty string" % sname)
		if not _is_num(st.get("duration_s")) or float(st["duration_s"]) < 0.0:
			return _m_err(path, "state %s duration_s must be a number >= 0" % sname)
		if clip == null and float(st["duration_s"]) != 0.0:
			return _m_err(path, "state %s has no clip, duration_s must be 0" % sname)
		if not (st.get("loop") is bool):
			return _m_err(path, "state %s loop must be a boolean" % sname)

	for key in [["phases", F01_PHASES], ["cargo_modes", CARGO_NAMES]]:
		var arr: Variant = d.get(key[0])
		if not (arr is Array) or arr.is_empty():
			return _m_err(path, "%s must be a non-empty array" % key[0])
		for e in arr:
			if not (String(e) in key[1]):
				return _m_err(path, "%s has unsupported entry: %s" % [key[0], str(e)])

	if d.has("phase_state_modes"):
		var psm: Variant = d["phase_state_modes"]
		if not (psm is Dictionary):
			return _m_err(path, "phase_state_modes must be an object")
		for phase in d["phases"]:
			var row: Variant = psm.get(phase)
			if not (row is Dictionary):
				return _m_err(path, "phase_state_modes missing phase: %s" % str(phase))
			for sname in STATE_NAMES:
				var cell: Variant = row.get(sname)
				if not (cell is Dictionary) or not (String(cell.get("mode", "")) in ["static", "animated", "na"]):
					return _m_err(path, "phase_state_modes %s/%s needs a valid mode" % [phase, sname])
				if not (cell.get("reason") is String):
					return _m_err(path, "phase_state_modes %s/%s needs a reason" % [phase, sname])

	var source: Variant = d.get("source")
	if not (source is Dictionary):
		return _m_err(path, "source must be an object")
	for key in ["generator", "source_path", "tool_version", "hash"]:
		if not (source.get(key) is String) or (source[key] as String).is_empty():
			return _m_err(path, "source missing field: %s" % key)
	if not (source.get("original") is bool):
		return _m_err(path, "source.original must be a boolean")
	# Optional source files are consumed by capture; reject malformed entries
	# before building a view or writing evidence. Older manifests may omit it.
	if source.has("files"):
		if not (source["files"] is Array):
			return _m_err(path, "source.files must be an array")
		for entry: Variant in source["files"]:
			if not (entry is Dictionary):
				return _m_err(path, "source.files entries must be objects")
			if not (entry.get("path") is String) or (entry["path"] as String).strip_edges().is_empty():
				return _m_err(path, "source.files path must be a non-empty string")
			if not (entry.get("sha256") is String):
				return _m_err(path, "source.files sha256 must be a string")
			var file_sha: String = entry["sha256"]
			if file_sha.length() != 64 or not _is_hex(file_sha):
				return _m_err(path, "source.files sha256 must contain 64 hex characters")

	if not _is_num(d.get("triangles")) or float(d["triangles"]) < 0.0:
		return _m_err(path, "triangles must be a number >= 0")
	if not (d.get("textures") is Array):
		return _m_err(path, "textures must be an array")

	var actual_sha := _sha256_file(String(d["glb"]))
	if actual_sha.is_empty():
		return _m_err(path, "GLB unreadable: %s" % String(d["glb"]))
	if actual_sha != sha:
		return _m_err(path, "GLB sha256 mismatch: manifest %s vs actual %s" % [sha, actual_sha])

	return {"ok": true, "error": "", "data": d}


func _m_err(path: String, msg: String) -> Dictionary:
	return {"ok": false, "error": "manifest %s: %s" % [path, msg], "data": {}}


func _is_num(v: Variant) -> bool:
	return (v is float or v is int) and is_finite(float(v))


func _is_hex(text: String) -> bool:
	for c in text.to_lower():
		if not ((c >= "0" and c <= "9") or (c >= "a" and c <= "f")):
			return false
	return true


func _sha256_file(path: String) -> String:
	var bytes := FileAccess.get_file_as_bytes(path)
	if bytes.is_empty() and FileAccess.get_open_error() != OK:
		return ""
	var ctx := HashingContext.new()
	ctx.start(HashingContext.HASH_SHA256)
	ctx.update(bytes)
	return ctx.finish().hex_encode()


# ------------------------------------------------------------ sample loading

func _instantiate_sample(data: Dictionary) -> Dictionary:
	var loaded_res: Variant = load(String(data["preview_scene"]))
	if loaded_res == null:
		return _s_err("preview_scene failed to load: %s" % data["preview_scene"])
	if not (loaded_res is PackedScene):
		return _s_err("preview_scene is not a scene: %s (loaded %s)" % [
				data["preview_scene"], loaded_res.get_class()])
	var scene: PackedScene = loaded_res
	var root: Node = scene.instantiate()
	if root == null:
		return _s_err("preview_scene instantiated null: %s" % data["preview_scene"])
	var root_err := _validate_wrapper_root(root)
	if root_err != "":
		root.free()
		return _s_err(root_err)
	if not root.has_method("apply_preview"):
		root.free()
		return _s_err("wrapper root has no apply_preview method")
	if root.get_node_or_null("Model") == null:
		root.free()
		return _s_err("wrapper missing Model node")
	var origin_err := _model_origin_error(root, String(data["glb"]))
	if origin_err != "":
		root.free()
		return _s_err(origin_err)
	for s: Variant in data["sockets"]:
		if root.get_node_or_null(String(s["node"])) == null:
			root.free()
			return _s_err("socket node missing: %s" % String(s["node"]))
	_entity_root().get_node("VisualRoot").add_child(root)
	return {"ok": true, "error": "", "wrapper": root}


func _validate_wrapper_root(root: Node) -> String:
	if not (root is Node3D):
		return "wrapper root must be a Node3D, got %s" % root.get_class()
	var root_name := String(root.name)
	if root_name != "SampleRoot":
		return "sample root must be named SampleRoot, got: %s" % root_name
	if (root as Node3D).transform != Transform3D.IDENTITY:
		return "SampleRoot must have an identity transform, got: %s" % str((root as Node3D).transform)
	return ""


func _model_origin_error(wrapper: Node, glb_res: String) -> String:
	var model := wrapper.get_node_or_null("Model")
	if model == null:
		return "wrapper missing Model node"
	var origin := String(model.scene_file_path)
	if origin.is_empty():
		return "cannot verify Model GLB origin: scene_file_path is empty (expected %s)" % glb_res
	if origin != glb_res:
		return "Model actual GLB (%s) does not match manifest glb (%s)" % [origin, glb_res]
	return ""


func _s_err(msg: String) -> Dictionary:
	return {"ok": false, "error": msg, "wrapper": null}


func _bind_materials(wrapper: Node, data: Dictionary, cache: Dictionary) -> String:
	# A whole-mesh cover would shadow every per-surface shared resource; reject
	# before binding anything so a rejected sample is not left half-wired.
	for mi in wrapper.find_children("*", "MeshInstance3D", true, false):
		var mesh_inst := mi as MeshInstance3D
		for prop in ["material_override", "material_overlay"]:
			if mesh_inst.get(prop) != null:
				return "%s is set on %s; whole-mesh material covers are not allowed" % [prop, mesh_inst.name]
	var covered := {}
	for b: Variant in data["material_bindings"]:
		var node_path := String(b["node"])
		var mi := wrapper.get_node_or_null(node_path)
		if mi == null:
			return "binding node not found: %s" % node_path
		if not (mi is MeshInstance3D):
			return "binding node is not a MeshInstance3D: %s" % node_path
		var mesh: Mesh = (mi as MeshInstance3D).mesh
		if mesh == null:
			return "binding node has no mesh: %s" % node_path
		var surface := int(b["surface"])
		if surface >= mesh.get_surface_count():
			return "binding surface out of range: %s[%d], mesh has %d surfaces" % [
					node_path, surface, mesh.get_surface_count()]
		var mat := _load_role_material(String(b["role"]), cache)
		if mat == null:
			return "role material unavailable: %s" % String(b["role"])
		if mat.next_pass != null:
			return "role material %s has next_pass set; chained passes would cover the shared resource" % String(b["role"])
		var dup_key := "%d|%d" % [mi.get_instance_id(), surface]
		if covered.has(dup_key):
			return "duplicate binding for surface: %s[%d]" % [node_path, surface]
		covered[dup_key] = true
		(mi as MeshInstance3D).set_surface_override_material(surface, mat)
		if (mi as MeshInstance3D).get_active_material(surface) != mat:
			return "surface override is not the active material: %s[%d]" % [node_path, surface]
	for mi in wrapper.find_children("*", "MeshInstance3D", true, false):
		var mesh_inst := mi as MeshInstance3D
		if mesh_inst.mesh == null:
			return "MeshInstance3D without mesh under sample: %s" % mesh_inst.name
		for s in mesh_inst.mesh.get_surface_count():
			if not covered.has("%d|%d" % [mesh_inst.get_instance_id(), s]):
				return "unbound surface: %s[%d] (node path %s)" % [mesh_inst.name, s,
						String(wrapper.get_path_to(mesh_inst))]
	return ""


func _load_role_material(role: String, cache: Dictionary) -> StandardMaterial3D:
	if cache.has(role):
		return cache[role]
	var path := "%s/%s.tres" % [MATERIAL_DIR, role]
	var mat: Variant = load(path)
	if mat == null:
		printerr("%s role material failed to load: %s" % [LOG_TAG, path])
		return null
	if not (mat is StandardMaterial3D):
		printerr("%s role material is not a StandardMaterial3D: %s" % [LOG_TAG, path])
		return null
	cache[role] = mat
	return mat


func _apply_wrapper_preset(wrapper: Node, preset: Dictionary) -> String:
	var ret: Variant = wrapper.call("apply_preview", preset)
	if not (ret is String):
		return "apply_preview must return a String, got %s" % type_string(typeof(ret))
	return String(ret)


func _visible_vertex_extents(wrapper: Node) -> Dictionary:
	# Project every visible mesh vertex into SampleRoot space; no rotated-AABB
	# corners, so moving parts cannot fake a conservative envelope.
	var mins := Vector3(INF, INF, INF)
	var maxs := Vector3(-INF, -INF, -INF)
	var found := false
	for mi in wrapper.find_children("*", "MeshInstance3D", true, false):
		var mesh_inst := mi as MeshInstance3D
		if not mesh_inst.is_visible_in_tree() or mesh_inst.mesh == null:
			continue
		var xf: Transform3D = wrapper.global_transform.affine_inverse() * mesh_inst.global_transform
		for s in mesh_inst.mesh.get_surface_count():
			var verts: PackedVector3Array = mesh_inst.mesh.surface_get_arrays(s)[Mesh.ARRAY_VERTEX]
			for v in verts:
				var point := xf * v
				mins = mins.min(point)
				maxs = maxs.max(point)
				found = true
	return {"found": found, "min": mins, "max": maxs}


func _extents_error(extents: Dictionary, box: Dictionary, label: String) -> String:
	if not extents["found"]:
		return "%s: no visible mesh vertices to measure" % label
	for i in 3:
		if float(box["min"][i]) > float(box["max"][i]):
			return "%s: declared min[%d] > max[%d]" % [label, i, i]
		var measured_min := (extents["min"] as Vector3)[i]
		var measured_max := (extents["max"] as Vector3)[i]
		if absf(measured_min - float(box["min"][i])) > 0.0001 \
				or absf(measured_max - float(box["max"][i])) > 0.0001:
			return "%s axis %d mismatch: measured [%s..%s] vs declared [%s..%s]" % [
					label, i, measured_min, measured_max, box["min"][i], box["max"][i]]
	return ""


func _check_bounds(wrapper: Node, data: Dictionary) -> String:
	var err := _apply_wrapper_preset(wrapper, {"state": "idle", "phase": "completed",
			"phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0})
	if err != "":
		return "bounds check: static pose apply failed: %s" % err
	var static_ext := _visible_vertex_extents(wrapper)
	if (data["cargo_modes"] as Array).has("loaded"):
		err = _apply_wrapper_preset(wrapper, {"state": "idle", "phase": "completed",
				"phase_t": 0.0, "cargo": "loaded", "reason": "none", "time_s": 0.0})
		if err != "":
			return "bounds check: loaded pose apply failed: %s" % err
	var loaded_ext := _visible_vertex_extents(wrapper)
	var static_err := _extents_error(static_ext, data["bounds"]["static"], "bounds.static")
	if static_err != "":
		return static_err
	var loaded_err := _extents_error(loaded_ext, data["bounds"]["loaded"], "bounds.loaded")
	if loaded_err != "":
		return loaded_err
	for i in 3:
		if float(data["bounds"]["static"]["min"][i]) < float(data["bounds"]["loaded"]["min"][i]) - 0.0001 \
				or float(data["bounds"]["static"]["max"][i]) > float(data["bounds"]["loaded"]["max"][i]) + 0.0001:
			return "bounds.static is not contained in bounds.loaded"
		if float(data["bounds"]["loaded"]["min"][i]) < float(data["bounds"]["active"]["min"][i]) - 0.0001 \
				or float(data["bounds"]["loaded"]["max"][i]) > float(data["bounds"]["active"]["max"][i]) + 0.0001:
			return "bounds.loaded is not contained in bounds.active"
		if float(data["bounds"]["active"]["min"][i]) > float(data["bounds"]["active"]["max"][i]):
			return "bounds.active: declared min[%d] > max[%d]" % [i, i]
	return ""


func _check_active_containment(wrapper: Node, data: Dictionary) -> String:
	# The currently displayed pose must fit the declared active envelope; the
	# full-envelope sweep across all states is the master's per-model check.
	var ext := _visible_vertex_extents(wrapper)
	if not ext["found"]:
		return "active containment: no visible mesh vertices to measure"
	for i in 3:
		if (ext["min"] as Vector3)[i] < float(data["bounds"]["active"]["min"][i]) - 0.0001 \
				or (ext["max"] as Vector3)[i] > float(data["bounds"]["active"]["max"][i]) + 0.0001:
			return "current pose exceeds bounds.active on axis %d: measured [%s..%s]" % [
					i, ext["min"], ext["max"]]
	return ""


func _capture_roots(wrapper: Node) -> Dictionary:
	var entity := _entity_root()
	var visual := entity.get_node_or_null("VisualRoot") as Node3D
	return {"entity": entity.transform, "visual": visual.transform,
			"sample": (wrapper as Node3D).transform}


func _verify_roots(wrapper: Node, before: Dictionary) -> String:
	var entity := _entity_root()
	var visual := entity.get_node_or_null("VisualRoot") as Node3D
	if entity == null or visual == null:
		return "EntityRoot/VisualRoot scaffold missing; root invariance cannot be verified"
	if entity.transform != before["entity"]:
		return "EntityRoot transform changed during the preview run"
	if visual.transform != before["visual"] or visual.transform != Transform3D.IDENTITY:
		return "VisualRoot must stay identity and unchanged"
	var sample := wrapper as Node3D
	if sample.transform != before["sample"] or sample.transform != Transform3D.IDENTITY:
		return "SampleRoot must stay identity and unchanged, got: %s" % str(sample.transform)
	return ""


# --------------------------------------------------------------- board/scene

## Slice B: all 3D content lives inside the fixed capture SubViewport (its own
## world), so the root window canvas only carries the GUI panel. The shadow
## atlas request is the contract's runtime RenderingServer call (no project
## setting is touched).
func _ensure_world() -> SubViewport:
	if _world != null:
		return _world
	var vp := SubViewport.new()
	vp.name = "PreviewWorld"
	ViewR1.configure_viewport(vp)
	add_child(vp)
	_wenv = ViewR1.build_environment()
	vp.add_child(_wenv)
	_sun = ViewR1.build_light()
	vp.add_child(_sun)
	_cam = ViewR1.build_camera()
	vp.add_child(_cam)
	ViewR1.request_shadow_atlas()
	_world = vp
	return _world


func _entity_root() -> Node3D:
	if _world == null:
		return null
	return _world.get_node_or_null("EntityRoot") as Node3D


## Yaw is owned by the preview tool: only EntityRoot rotates; the light and
## cameras never move and VisualRoot/SampleRoot stay identity.
func _set_yaw(yaw_deg: int) -> void:
	var entity := _entity_root()
	if entity != null:
		entity.rotation.y = deg_to_rad(float(yaw_deg))


func _build_ground(cache: Dictionary) -> String:
	var ground := MeshInstance3D.new()
	ground.name = "Ground"
	var plane := PlaneMesh.new()
	plane.size = Vector2(20, 20)
	ground.mesh = plane
	var mat := _load_role_material("soil_mars", cache)
	if mat == null:
		return "soil_mars material unavailable for ground"
	ground.set_surface_override_material(0, mat)
	_ensure_world().add_child(ground)
	return ""


func _build_board(cache: Dictionary) -> String:
	var board := Node3D.new()
	board.name = "TrialBoard"
	for g: Variant in BOARD_GROUPS:
		var mat := _load_role_material(String(g[0]), cache)
		if mat == null:
			return "board material unavailable: %s" % String(g[0])
		var sphere := MeshInstance3D.new()
		sphere.name = "%s_sphere" % g[0]
		var sm := SphereMesh.new()
		sm.radius = 0.3
		sm.height = 0.6
		sphere.mesh = sm
		sphere.position = Vector3(float(g[1]), BOARD_BODY_Y, float(g[2]) - BOARD_GROUP_OFFSET)
		sphere.set_surface_override_material(0, mat)
		board.add_child(sphere)
		var cube := MeshInstance3D.new()
		cube.name = "%s_cube" % g[0]
		var bm := BoxMesh.new()
		bm.size = Vector3(0.6, 0.6, 0.6)
		cube.mesh = bm
		cube.position = Vector3(float(g[1]), BOARD_BODY_Y, float(g[2]) + BOARD_GROUP_OFFSET)
		cube.set_surface_override_material(0, mat)
		board.add_child(cube)
	_ensure_world().add_child(board)
	return ""


func _build_scene_scaffold(cache: Dictionary) -> String:
	_ensure_world()
	var entity_root := Node3D.new()
	entity_root.name = "EntityRoot"
	var visual_root := Node3D.new()
	visual_root.name = "VisualRoot"
	entity_root.add_child(visual_root)
	_world.add_child(entity_root)
	return _build_ground(cache)


func _resolve_mode_cell(data: Dictionary, phase: String, state: String) -> Dictionary:
	# The verified manifest is the source of truth for modes; the wrapper only
	# owns apply_preview, so no extra methods may be called on it.
	if data.has("phase_state_modes"):
		return data["phase_state_modes"][phase][state]
	return data["states"][state]


func _apply_preset(wrapper: Node, p: Dictionary, data: Dictionary) -> Dictionary:
	var phase := String(p["phase"])
	if not (data["phases"] as Array).has(phase):
		return {"error": "phase %s is not declared by the manifest" % phase,
				"mode": "", "mode_reason": "", "na_reason": ""}
	var preset := {
		"state": p["state"], "phase": p["phase"], "phase_t": p["phase_t"],
		"cargo": p["cargo"], "reason": p["reason"], "time_s": p["time_s"],
	}
	var err := _apply_wrapper_preset(wrapper, preset)
	if err != "":
		return {"error": err, "mode": "", "mode_reason": "", "na_reason": ""}
	var cell := _resolve_mode_cell(data, phase, String(p["state"]))
	var mode := String(cell.get("mode", ""))
	var mode_reason := String(cell.get("reason", ""))
	return {"error": "", "mode": mode, "mode_reason": mode_reason,
			"na_reason": mode_reason if mode == "na" else ""}


## Slice A headless validation path: build + load + bind + apply + verify, then
## exit. Load/bind failures keep the A exit-code semantics (1, no PASS image).
func _prepare_sample(cache: Dictionary) -> Dictionary:
	_set_yaw(int(params["yaw"]))
	var res := _load_manifest(String(params["manifest_path"]))
	if not res["ok"]:
		return {"error": res["error"], "data": {}, "wrapper": null, "applied": {}}
	var inst := _instantiate_sample(res["data"])
	if not inst["ok"]:
		return {"error": inst["error"], "data": {}, "wrapper": null, "applied": {}}
	var wrapper: Node = inst["wrapper"]
	var roots := _capture_roots(wrapper)
	var err := _bind_materials(wrapper, res["data"], cache)
	var applied := {"error": "not applied", "mode": "", "mode_reason": "", "na_reason": ""}
	if err == "":
		err = _check_bounds(wrapper, res["data"])
	if err == "":
		applied = _apply_preset(wrapper, params, res["data"])
		if applied["error"] != "":
			err = applied["error"]
	if err == "":
		err = _check_active_containment(wrapper, res["data"])
	if err == "":
		err = _verify_roots(wrapper, roots)
	return {"error": err, "data": res["data"], "wrapper": wrapper, "applied": applied}


## Load sample/board content for the interactive GUI and the capture run.
## Always returns asset_id/revision keys; on error only "error" is meaningful.
func _prepare_content() -> Dictionary:
	var cache := {}
	var err := _build_scene_scaffold(cache)
	if err != "":
		return {"error": err, "data": {}, "wrapper": null, "applied": {}, "asset_id": "", "revision": null}
	if params["mode"] == "board":
		err = _build_board(cache)
		if err != "":
			return {"error": err, "data": {}, "wrapper": null, "applied": {}, "asset_id": "", "revision": null}
		_camera_mode_used = ViewR1.place_camera(_cam, "board")
		return {"error": "", "data": {}, "wrapper": null, "applied": {},
				"asset_id": "material_board", "revision": null}
	var prep := _prepare_sample(cache)
	if prep["error"] != "":
		return prep
	_camera_mode_used = ViewR1.place_camera(_cam, String(params["camera"]))
	prep["asset_id"] = String(prep["data"]["id"])
	prep["revision"] = int(prep["data"]["revision"])
	return prep


func _run_once() -> int:
	var cache := {}
	var err := _build_scene_scaffold(cache)
	if err != "":
		printerr("%s RUN ERROR: %s" % [LOG_TAG, err])
		return 1
	if params["mode"] == "board":
		err = _build_board(cache)
		if err != "":
			printerr("%s RUN ERROR: %s" % [LOG_TAG, err])
			return 1
		print("%s BOARD_OK roles=6 objects=12 state=default" % LOG_TAG)
		return 0
	var prep := _prepare_sample(cache)
	if prep["error"] != "":
		printerr("%s RUN ERROR: %s" % [LOG_TAG, prep["error"]])
		return 1
	var data: Dictionary = prep["data"]
	var applied: Dictionary = prep["applied"]
	print("%s SAMPLE_OK id=%s revision=%s bindings=%d applied state=%s phase=%s phase_t=%s cargo=%s reason=%s time_s=%s mode=%s na=%s" % [
			LOG_TAG, data["id"], data["revision"],
			(data["material_bindings"] as Array).size(),
			params["state"], params["phase"], params["phase_t"], params["cargo"],
			params["reason"], params["time_s"], applied["mode"],
			applied["na_reason"] if applied["na_reason"] != "" else "-"])
	return 0


# ------------------------------------------------------- slice B: interactive

func _prepare_interactive() -> void:
	_ensure_world()
	var prep := _prepare_content()
	_build_ui_layer(prep)
	if prep["error"] != "":
		_panel.set_error(prep["error"])
		_panel.set_status("加载/校验失败，窗口保持打开 load failed, window stays open")
		printerr("%s INTERACTIVE ERROR: %s" % [LOG_TAG, prep["error"]])
		get_window().title = "余电 ART-PREVIEW-R1 B · ERROR"
		return
	_wrapper = prep["wrapper"]
	_data = prep["data"]
	_applied = prep["applied"]
	_panel.set_selection(params)
	_last_legal_text = _panel.selection_values()
	_ensure_overlay()
	_update_overlay()
	_panel.set_error("")
	_update_status()
	_refresh_window_title()


func _build_ui_layer(prep: Dictionary) -> void:
	# Keep the status/error footer visible at startup. The private 3D viewport
	# stays 1920x1200 regardless of the external inspection window size.
	get_window().min_size = Vector2i(1000, 900)
	get_window().size = Vector2i(maxi(get_window().size.x, 1000), maxi(get_window().size.y, 900))
	var layer := CanvasLayer.new()
	layer.name = "UILayer"
	add_child(layer)
	var root := Control.new()
	root.name = "UIRoot"
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	layer.add_child(root)
	var margin := MarginContainer.new()
	margin.set_anchors_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 8)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_right", 8)
	margin.add_theme_constant_override("margin_bottom", 8)
	root.add_child(margin)
	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 8)
	margin.add_child(hbox)
	var frame := PanelContainer.new()
	frame.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	frame.size_flags_vertical = Control.SIZE_EXPAND_FILL
	hbox.add_child(frame)
	var view := TextureRect.new()
	view.texture = _world.get_texture()
	view.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	view.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	frame.add_child(view)
	var opts := {
		"title": "余电 ART-PREVIEW-R1 B（独立预览，非游戏）",
		"cameras": ["normal", "close"],
		"states": STATE_NAMES,
		"phases": F01_PHASES,
		"cargos": CARGO_NAMES,
		"reasons": REASON_NAMES,
		"board": params["mode"] == "board",
	}
	if not (prep["data"] as Dictionary).is_empty():
		opts["phases"] = prep["data"]["phases"]
		opts["cargos"] = prep["data"]["cargo_modes"]
		opts["title"] = "余电 ART-PREVIEW-R1 B · %s rev %s" % [prep["asset_id"], str(prep["revision"])]
	_panel = PanelScript.new()
	_panel.setup(opts, self)
	hbox.add_child(_panel)


func _ensure_overlay() -> void:
	if _overlay != null:
		return
	_overlay = OverlayScript.new()
	_world.add_child(_overlay)


func gui_selection_changed(key: String) -> void:
	_apply_gui_selection(key)


func gui_time_submitted() -> void:
	_apply_gui_selection("time")


func gui_reset_pressed() -> void:
	if _panel == null:
		return
	var defaults := {"camera": "normal", "yaw": 0, "state": "idle", "phase": "completed",
			"phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0, "labels": "evidence"}
	for k in defaults:
		params[k] = defaults[k]
	if _wrapper != null:
		_set_yaw(0)
		_camera_mode_used = ViewR1.place_camera(_cam, "normal")
		var applied := _apply_preset(_wrapper, params, _data)
		if applied["error"] != "":
			_panel.set_error(applied["error"])
			return
		_applied = applied
	_panel.set_selection(params)
	_last_legal_text = _panel.selection_values()
	_panel.set_phase_t_enabled(false)
	_panel.set_error("")
	_update_overlay()
	_update_status()
	_refresh_window_title()


## Every GUI change goes through the same _typed_params validation as the CLI;
## a rejected change keeps the last legal pose and reverts its control.
func _apply_gui_selection(changed_key: String) -> void:
	if _panel == null:
		return
	var values: Dictionary = _panel.selection_values()
	var typed := _typed_params(values)
	if typed["error"] != "":
		_reject_gui(changed_key, String(typed["error"]))
		return
	var p: Dictionary = typed["params"]
	if _wrapper != null:
		if not (_data["phases"] as Array).has(p["phase"]):
			_reject_gui(changed_key, "phase %s is not declared by the manifest" % p["phase"])
			return
		if not (_data["cargo_modes"] as Array).has(p["cargo"]):
			_reject_gui(changed_key, "cargo %s is not declared by the manifest" % p["cargo"])
			return
		var applied := _apply_preset(_wrapper, p, _data)
		if applied["error"] != "":
			_reject_gui(changed_key, applied["error"])
			return
		_applied = applied
		if changed_key == "yaw":
			_set_yaw(int(p["yaw"]))
		if changed_key == "camera":
			_camera_mode_used = ViewR1.place_camera(_cam, String(p["camera"]))
	for k in ["camera", "yaw", "state", "phase", "phase_t", "cargo", "reason", "time_s", "labels"]:
		params[k] = p[k]
	_last_legal_text = values
	_panel.set_phase_t_enabled(String(p["phase"]) == "deploying")
	_panel.set_error("")
	_update_overlay()
	_update_status()
	_refresh_window_title()


func _reject_gui(key: String, msg: String) -> void:
	if _last_legal_text.has(key):
		if key == "time":
			_panel.revert_time(String(_last_legal_text["time"]))
		else:
			_panel.revert_option(key, String(_last_legal_text[key]))
	_panel.set_error(msg)


func _update_status() -> void:
	if _panel == null:
		return
	if _wrapper == null:
		_panel.set_status("材质试板 material board · labels %s" % params["labels"])
		return
	_panel.set_status("state %s · phase %s · phase_t %s\ncargo %s · reason %s · time %s\ncamera %s · yaw %d · labels %s · mode %s" % [
			params["state"], params["phase"], String.num(float(params["phase_t"]), 3),
			params["cargo"], params["reason"], String.num(float(params["time_s"]), 3),
			_camera_mode_used, int(params["yaw"]), params["labels"], _applied.get("mode", "")])


func _refresh_window_title() -> void:
	var asset := "material_board" if _wrapper == null else String(_data["id"])
	get_window().title = "余电 ART-PREVIEW-R1 B · %s · %s · yaw %d" % [
			asset, String(params["state"]), int(params["yaw"])]


func _update_overlay() -> void:
	if _overlay == null:
		return
	_overlay.set_labels_mode(String(params["labels"]))
	_overlay.set_reason(String(params["reason"]))
	_overlay.set_info_text(_overlay_info_text())
	_overlay.set_socket_points(_socket_overlay_points())


func _overlay_info_text() -> String:
	var text := "余电 ART-PREVIEW-R1 · EVIDENCE\n"
	if _wrapper == null:
		text += "材质试板 material board · roles 6\n"
	else:
		text += "asset %s · rev %d\n" % [String(_data["id"]), int(_data["revision"])]
	text += "camera %s · yaw %d\n" % [_camera_mode_used, int(params["yaw"])]
	text += "state %s · phase %s · phase_t %s\n" % [params["state"], params["phase"],
			String.num(float(params["phase_t"]), 3)]
	text += "cargo %s · reason %s · time %s s" % [params["cargo"], params["reason"],
			String.num(float(params["time_s"]), 3)]
	if String(_applied.get("na_reason", "")) != "":
		text += "\nN/A: %s" % _applied["na_reason"]
	return text


func _socket_overlay_points() -> Array:
	var points := []
	if _wrapper == null or _cam == null:
		return points
	for s: Variant in _data.get("sockets", []):
		var node := _wrapper.get_node_or_null(String(s["node"])) as Node3D
		if node == null:
			continue
		var gp := node.global_transform.origin
		points.append({"position": _cam.unproject_position(gp),
				"visible": not _cam.is_position_behind(gp)})
	return points


func _roots_record() -> Dictionary:
	var entity := _entity_root()
	if entity == null:
		return {"note": "world not built"}
	var record := {
		"entity_rotation_y_deg": rad_to_deg(entity.rotation.y),
		"entity_position": [entity.position.x, entity.position.y, entity.position.z],
	}
	var visual := entity.get_node_or_null("VisualRoot") as Node3D
	record["visual_identity"] = visual != null and visual.transform == Transform3D.IDENTITY
	record["sample_identity"] = _wrapper == null or (_wrapper as Node3D).transform == Transform3D.IDENTITY
	if _wrapper == null:
		record["note"] = "board mode has no sample roots"
	return record


# ---------------------------------------------------------- slice B: capture

## One-shot deterministic capture: refuse collisions first, build content,
## settle exactly two frame_post_draw cycles, then grab the SubViewport image
## (the pose is an absolute function of the requested time, so no frame
## guessing is involved). Any failure exits 1 without a PASS marker.
func _run_capture() -> void:
	var dir := String(params["capture_dir"])
	var dir_err := Capture.capture_dir_error(dir)
	if dir_err != "":
		printerr("%s CAPTURE ERROR: %s" % [LOG_TAG, dir_err])
		get_tree().quit(1)
		return
	var prep := _prepare_content()
	if prep["error"] != "":
		printerr("%s RUN ERROR: %s" % [LOG_TAG, prep["error"]])
		get_tree().quit(1)
		return
	_wrapper = prep["wrapper"]
	_data = prep["data"]
	_applied = prep["applied"]
	var make_err := Capture.create_capture_dir(dir)
	if make_err != "":
		printerr("%s CAPTURE ERROR: %s" % [LOG_TAG, make_err])
		get_tree().quit(1)
		return
	_ensure_overlay()
	_update_overlay()
	get_window().title = "余电 ART-PREVIEW-R1 B · CAPTURE · %s" % String(prep["asset_id"])
	for i in 2:
		await RenderingServer.frame_post_draw
	var img: Image = _world.get_texture().get_image()
	if img == null or img.is_empty() \
			or img.get_width() != ViewR1.VIEWPORT_WIDTH or img.get_height() != ViewR1.VIEWPORT_HEIGHT:
		var got := "null" if img == null else "%dx%d" % [img.get_width(), img.get_height()]
		printerr("%s CAPTURE ERROR: viewport image not usable (got %s, want %dx%d); no evidence written" % [
				LOG_TAG, got, ViewR1.VIEWPORT_WIDTH, ViewR1.VIEWPORT_HEIGHT])
		get_tree().quit(1)
		return
	var base := Capture.base_name(params, String(prep["asset_id"]))
	var ctx := {
		"params": params, "asset_id": prep["asset_id"], "revision": prep["revision"],
		"data": _data, "applied": _applied,
		"view": ViewR1.snapshot(_world, _cam, _sun, _wenv, _camera_mode_used),
		"roots": _roots_record(),
		"project_dir": ProjectSettings.globalize_path("res://."),
		"png_name": base + ".png", "json_name": base + ".json",
		"manifest_path": String(params["manifest_path"]),
	}
	var record := Capture.build_record(self, ctx)
	var write_err := Capture.write_capture(dir, base, img, record)
	if write_err != "":
		printerr("%s CAPTURE ERROR: %s (no PASS marker is printed)" % [LOG_TAG, write_err])
		get_tree().quit(1)
		return
	print("%s CAPTURE_OK dir=%s png=%s json=%s size=%dx%d camera=%s yaw=%d state=%s phase=%s phase_t=%s cargo=%s reason=%s time_s=%s labels=%s mode=%s" % [
			LOG_TAG, dir, base + ".png", base + ".json",
			img.get_width(), img.get_height(), _camera_mode_used, int(params["yaw"]),
			params["state"], params["phase"], params["phase_t"], params["cargo"],
			params["reason"], params["time_s"], params["labels"], _applied.get("mode", "")])
	get_tree().quit(0)


# ------------------------------------------------------------------ self-test

func _check(name: String, ok: bool, detail: String = "") -> bool:
	_st_results.append({"name": name, "ok": ok, "detail": detail})
	print("%s %s%s" % ["PASS" if ok else "FAIL", name, "" if ok else " :: " + detail])
	return ok


func _self_test() -> bool:
	_cli_cases()
	var cache := {}
	_check("scaffold", _build_scene_scaffold(cache) == "")
	_board_cases(cache)
	var res := _load_manifest(String(params["manifest_path"]))
	if not _check("manifest_load", res["ok"], String(res["error"])):
		_finish_self_test()
		return false
	_st_base_manifest = res["data"]
	_check("manifest_is_probe", _st_base_manifest["id"] == "probe_r1",
			"self-test is authored against the bundled probe manifest (see README)")
	_sample_cases(res["data"], cache)
	_wrapper_cases(res["data"], cache)
	_negative_manifest_cases()
	_b_cases()
	_finish_self_test()
	return _st_results.all(func(r: Dictionary) -> bool: return bool(r["ok"]))


func _finish_self_test() -> void:
	_cleanup_tmp()
	var failed := _st_results.filter(func(r): return not r["ok"])
	var marker := "PASS" if failed.is_empty() else "FAIL"
	print("ART_PREVIEW_R1_SELFTEST_%s %d/%d" % [marker, _st_results.size() - failed.size(), _st_results.size()])
	if not failed.is_empty():
		for r in failed:
			printerr("%s FAILED CHECK %s :: %s" % [LOG_TAG, r["name"], r["detail"]])


func _cli_cases() -> void:
	var manifest := String(params["manifest_path"])
	var cases := [
		[[], "choose exactly one"],
		[["--manifest"], "missing value"],
		[["--manifest", ""], "missing value"],
		[["--manifest", manifest, "--manifest", manifest], "duplicate CLI key"],
		[["--manifest", manifest, "--material-board"], "mutually exclusive"],
		[["--bogus"], "unknown CLI key"],
		[["--state", "offline", "--manifest", manifest], "offline is rejected"],
		[["--state", "Idle", "--manifest", manifest], "unknown state"],
		[["--time", "-1", "--manifest", manifest], "non-negative"],
		[["--time", "nan", "--manifest", manifest], "not a valid number"],
		[["--time", "inf", "--manifest", manifest], "not a valid number"],
		[["--time", "abc", "--manifest", manifest], "not a valid number"],
		[["--phase-t", "1.5", "--manifest", manifest], "must be in [0,1]"],
		[["--phase-t", "0.5", "--phase", "completed", "--manifest", manifest], "only meaningful for deploying"],
		[["--reason", "return_charge", "--state", "idle", "--manifest", manifest], "only applies to move"],
		[["--reason", "no_power", "--state", "idle", "--manifest", manifest], "only applies to disabled/towed/maintenance"],
		[["--cargo", "full", "--manifest", manifest], "cargo"],
		[["--phase", "storage", "--manifest", manifest], "unknown phase"],
		[["--camera", "top", "--manifest", manifest], "camera"],
		[["--yaw", "45", "--manifest", manifest], "yaw"],
		[["--labels", "naked", "--manifest", manifest], "labels"],
		[["--material-board", "--state", "work"], "default object state"],
		[["--material-board", "--time", "3"], "time 0"],
		# Slice A rejected any --capture-dir with a "slice B not implemented"
		# placeholder; slice B implements capture, so this case became the
		# explicit-empty rejection. The headless capture refusal is verified as
		# a real command (see README), not a parse-level check.
		[["--capture-dir", "", "--manifest", manifest], "missing value"],
	]
	var idx := 0
	for case: Variant in cases:
		idx += 1
		var parsed := parse_args(case[0])
		_check("cli_neg_%02d" % idx, parsed["error"] != "" and String(parsed["error"]).contains(String(case[1])),
				String(parsed["error"]))
	var ok_default := parse_args(["--manifest", manifest])
	_check("cli_default", ok_default["error"] == "" and ok_default["params"]["camera"] == "normal"
			and ok_default["params"]["yaw"] == 0 and ok_default["params"]["state"] == "idle"
			and ok_default["params"]["phase"] == "completed" and ok_default["params"]["phase_t"] == 0.0
			and ok_default["params"]["cargo"] == "empty" and ok_default["params"]["reason"] == "none"
			and ok_default["params"]["time_s"] == 0.0 and ok_default["params"]["labels"] == "evidence"
			and not ok_default["params"]["self_test"], String(ok_default["error"]))
	var ok_full := parse_args(["--manifest", manifest, "--camera", "close", "--yaw", "90",
			"--state", "work", "--phase", "deploying", "--phase-t", "0.25", "--cargo", "loaded",
			"--reason", "none", "--time", "1.5", "--labels", "blind", "--self-test"])
	_check("cli_full", ok_full["error"] == "" and ok_full["params"]["camera"] == "close"
			and ok_full["params"]["yaw"] == 90 and ok_full["params"]["state"] == "work"
			and ok_full["params"]["phase"] == "deploying" and ok_full["params"]["phase_t"] == 0.25
			and ok_full["params"]["cargo"] == "loaded" and ok_full["params"]["time_s"] == 1.5
			and ok_full["params"]["labels"] == "blind" and ok_full["params"]["self_test"],
			String(ok_full["error"]))
	var ok_board := parse_args(["--material-board"])
	_check("cli_board", ok_board["error"] == "" and ok_board["params"]["mode"] == "board",
			String(ok_board["error"]))


func _board_cases(cache: Dictionary) -> void:
	var err := _build_board(cache)
	if not _check("board_build", err == "", err):
		return
	var board := _world.get_node_or_null("TrialBoard")
	var meshes := board.find_children("*", "MeshInstance3D", true, false)
	_check("board_object_count", meshes.size() == 12, "found %d" % meshes.size())
	for g: Variant in BOARD_GROUPS:
		var role := String(g[0])
		var sphere: MeshInstance3D = board.get_node_or_null("%s_sphere" % role)
		var cube: MeshInstance3D = board.get_node_or_null("%s_cube" % role)
		if not _check("board_pair_%s" % role, sphere != null and cube != null):
			continue
		var sphere_mat := sphere.get_surface_override_material(0) as StandardMaterial3D
		var cube_mat := cube.get_surface_override_material(0) as StandardMaterial3D
		_check("board_same_resource_%s" % role, sphere_mat != null and sphere_mat == cube_mat,
				"sphere and cube must share one loaded resource instance")
		var expected: Dictionary = ROLE_TABLE[role]
		var albedo_ok := true
		for i in 3:
			albedo_ok = albedo_ok and absf(sphere_mat.albedo_color[i] - float(expected["albedo"][i])) <= 0.00001
		_check("board_table_%s" % role,
				albedo_ok and absf(sphere_mat.roughness - float(expected["roughness"])) <= 0.00001
				and absf(sphere_mat.metallic - float(expected["metallic"])) <= 0.00001,
				"albedo=%s roughness=%s metallic=%s" % [sphere_mat.albedo_color, sphere_mat.roughness, sphere_mat.metallic])
		_check("board_geometry_%s" % role,
				absf(sphere.position.y - 0.3) < 0.00001 and absf(cube.position.y - 0.3) < 0.00001
				and absf(sphere.position.x - float(g[1])) < 0.00001
				and absf(cube.position.x - float(g[1])) < 0.00001
				and absf(sphere.position.z - (float(g[2]) - BOARD_GROUP_OFFSET)) < 0.00001
				and absf(cube.position.z - (float(g[2]) + BOARD_GROUP_OFFSET)) < 0.00001,
				"sphere=%s cube=%s" % [sphere.position, cube.position])


func _sample_cases(data: Dictionary, cache: Dictionary) -> void:
	var sha := _sha256_file(String(data["glb"]))
	_check("manifest_glb_hash", sha == String(data["glb_sha256"]), sha)
	var inst := _instantiate_sample(data)
	if not _check("sample_instantiate", inst["ok"], String(inst["error"])):
		return
	var wrapper: Node = inst["wrapper"]
	_check("sample_root_identity", wrapper.transform == Transform3D.IDENTITY, str(wrapper.transform))
	var bind_err := _bind_materials(wrapper, data, cache)
	_check("sample_bind", bind_err == "", bind_err)
	_check("sample_bounds", _check_bounds(wrapper, data) == "", _check_bounds(wrapper, data))
	_check("sample_model_origin", _model_origin_error(wrapper, String(data["glb"])) == "",
			_model_origin_error(wrapper, String(data["glb"])))
	var body_mat := (wrapper.get_node("Model/Body") as MeshInstance3D).get_active_material(0)
	_check("sample_active_material_is_shared", body_mat != null and body_mat == cache["body_light"],
			"active material must be the cached shared body_light resource")
	var board := _world.get_node_or_null("TrialBoard")
	if board != null:
		var board_mat: Material = (board.get_node("body_light_sphere") as MeshInstance3D).get_surface_override_material(0)
		_check("sample_shares_board_resource", body_mat == board_mat,
				"probe body_light must reuse the same loaded resource as the board")
	var socket: Node3D = wrapper.get_node_or_null(String(data["sockets"][0]["node"]))
	if _check("sample_socket_exists", socket != null):
		var declared: Array = data["sockets"][0]["position"]
		var actual := socket.position
		_check("sample_socket_position", absf(actual.x - float(declared[0])) <= 0.0001
				and absf(actual.y - float(declared[1])) <= 0.0001
				and absf(actual.z - float(declared[2])) <= 0.0001,
				"declared %s vs actual %s" % [declared, actual])
	var applied := _apply_preset(wrapper, params, data)
	_check("sample_apply_default", applied["error"] == "", applied["error"])
	_check("sample_mode_from_manifest",
			String(applied["mode"]) == "static" and String(applied["mode_reason"]) != ""
			and String(applied["na_reason"]) == "",
			"completed/idle cell must resolve to static with a reason, got %s" % applied)
	_check("sample_active_containment", _check_active_containment(wrapper, data) == "",
			_check_active_containment(wrapper, data))
	wrapper.free()


func _wrapper_cases(data: Dictionary, cache: Dictionary) -> void:
	var inst := _instantiate_sample(data)
	if not _check("wrapper_instance", inst["ok"], String(inst["error"])):
		return
	var wrapper: Node = inst["wrapper"]
	_bind_materials(wrapper, data, cache)
	var roots := _capture_roots(wrapper)
	var body: Node3D = wrapper.get_node("Model/Body")
	var cargo: Node3D = wrapper.get_node("Model/Socket_Cargo/Cargo")
	var home_y := body.position.y  # 0.2 from the GLB node translation
	var root_before: Transform3D = wrapper.transform

	var e1 := _wp(wrapper, "idle", "completed", 0.0, "empty", "none", 0.0)
	var pose_first: Transform3D = body.transform
	var e2 := _wp(wrapper, "idle", "completed", 0.0, "empty", "none", 0.0)
	_check("wrapper_idempotent", e1 == "" and e2 == "" and body.transform == pose_first,
			"%s/%s same input must give the same pose" % [e1, e2])

	_check("wrapper_base_y", absf(body.position.y - home_y) < 0.000001,
			"completed base keeps the GLB rest y, got %s" % body.position.y)

	_wp(wrapper, "work", "completed", 0.0, "empty", "none", 0.5)
	_check("wrapper_work_moves", absf(body.position.y - home_y - 0.05) < 0.000001,
			"work bob at t=0.5 should raise by 0.05, got %s" % body.position.y)
	_wp(wrapper, "disabled", "completed", 0.0, "empty", "none", 0.0)
	_check("wrapper_disabled_static", absf(body.position.y - home_y) < 0.000001)
	_wp(wrapper, "idle", "completed", 0.0, "empty", "none", 0.0)
	_check("wrapper_back_to_base", body.transform == pose_first, "work->disabled->idle must fully reset")

	_wp(wrapper, "idle", "completed", 0.0, "loaded", "none", 0.0)
	_check("wrapper_loaded_visible", cargo.visible)
	_wp(wrapper, "idle", "completed", 0.0, "empty", "none", 0.0)
	_check("wrapper_empty_hidden", not cargo.visible and body.transform == pose_first,
			"loaded->empty must hide the cargo and reset")

	_wp(wrapper, "idle", "packed", 0.0, "empty", "none", 0.0)
	_check("wrapper_packed_na", absf(body.position.y - (home_y - 0.05)) < 0.000001
			and String(wrapper.call("get_state_mode", "packed", "work")) == "na",
			"packed is na, static base 5 cm low")
	_wp(wrapper, "work", "packed", 0.0, "empty", "none", 0.5)
	_check("wrapper_packed_no_motion", absf(body.position.y - (home_y - 0.05)) < 0.000001,
			"na state must not keep or start motion")
	_wp(wrapper, "idle", "deploying", 0.5, "empty", "none", 0.0)
	_check("wrapper_deploy_mid", absf(body.position.y - (home_y - 0.025)) < 0.000001,
			"deploying phase_t=0.5 base, got %s" % body.position.y)
	_wp(wrapper, "idle", "completed", 0.0, "empty", "none", 0.0)
	_check("wrapper_phase_roundtrip", body.transform == pose_first, "phase round-trip must restore the base")

	# Non-completed phases hold their declared static/phase_t baseline for every
	# state; run-cycle motion must not leak into installed (regression for the
	# independent audit's declared_static_holds finding).
	_wp(wrapper, "idle", "installed", 0.0, "empty", "none", 0.5)
	var installed_pose: Transform3D = body.transform
	_check("wrapper_installed_static", body.position == Vector3(0, home_y, 0),
			"installed baseline must be the rest pose, got %s" % body.position)
	_wp(wrapper, "work", "installed", 0.0, "empty", "none", 0.5)
	_check("wrapper_installed_work_holds", body.transform == installed_pose,
			"installed declares static; work must not bob, got %s" % body.position)
	_wp(wrapper, "move", "installed", 0.0, "empty", "none", 1.5)
	_check("wrapper_installed_move_holds", body.transform == installed_pose,
			"installed declares static; move must not traverse, got %s" % body.position)
	_wp(wrapper, "idle", "completed", 0.0, "empty", "none", 0.0)

	_wp(wrapper, "move", "completed", 0.0, "empty", "none", 1.0)
	_check("wrapper_move_seek", absf(body.position.x - 0.5) < 0.000001, "move at t=1 is x=0.5")
	_wp(wrapper, "move", "completed", 0.0, "empty", "none", 5.0)
	_check("wrapper_move_holds_end", absf(body.position.x - 1.0) < 0.000001, "non-loop move holds the end pose")
	_wp(wrapper, "towed", "completed", 0.0, "empty", "none", 0.0)
	_check("wrapper_towed_tilt", absf(body.rotation.z - 0.0872665) < 0.000001)
	_wp(wrapper, "idle", "completed", 0.0, "empty", "none", 0.0)
	_check("wrapper_rotation_reset", body.transform == pose_first, "rotation must reset between applies")

	var before: Transform3D = body.transform
	var neg_cases := [
		["offline", {"state": "offline", "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0}, "offline"],
		["bad_state", {"state": "sleeping", "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0}, "unknown state"],
		["bad_phase", {"state": "idle", "phase": "storage", "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0}, "phases"],
		["phase_t_range", {"state": "idle", "phase": "deploying", "phase_t": 1.5, "cargo": "empty", "reason": "none", "time_s": 0.0}, "[0,1]"],
		["phase_t_elsewhere", {"state": "idle", "phase": "completed", "phase_t": 0.5, "cargo": "empty", "reason": "none", "time_s": 0.0}, "deploying"],
		["bad_cargo", {"state": "idle", "phase": "completed", "phase_t": 0.0, "cargo": "full", "reason": "none", "time_s": 0.0}, "cargo"],
		["reason_pair", {"state": "idle", "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "both", "time_s": 0.0}, "only applies"],
		["neg_time", {"state": "idle", "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": -1.0}, "non-negative"],
		["nan_time", {"state": "idle", "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": NAN}, "finite"],
		["missing_key", {"state": "idle", "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none"}, "missing key"],
		["extra_key", {"state": "idle", "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0, "yaw": 0}, "six contract keys"],
		["type_phase_t", {"state": "idle", "phase": "completed", "phase_t": [], "cargo": "empty", "reason": "none", "time_s": 0.0}, "must be a number"],
		["type_time", {"state": "idle", "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": []}, "must be a number"],
		["type_state", {"state": 5, "phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0}, "must be a String"],
		["type_phase", {"state": "idle", "phase": [], "phase_t": 0.0, "cargo": "empty", "reason": "none", "time_s": 0.0}, "must be a String"],
	]
	var idx := 0
	for case: Variant in neg_cases:
		idx += 1
		var err: String = wrapper.call("apply_preview", case[1])
		var unchanged: bool = body.transform == before
		_check("wrapper_neg_%s" % case[0], err != "" and String(err).contains(String(case[2])) and unchanged,
				"%s | body_moved=%s" % [err, not unchanged])

	_check("wrapper_root_never_moves", wrapper.transform == root_before and wrapper.transform == Transform3D.IDENTITY,
			str(wrapper.transform))
	_check("wrapper_roots_unchanged", _verify_roots(wrapper, roots) == "", _verify_roots(wrapper, roots))
	_wp(wrapper, "move", "completed", 0.0, "empty", "none", 1.5)
	_check("wrapper_move_end_within_active", _check_active_containment(wrapper, data) == "",
			_check_active_containment(wrapper, data))
	for pose in [["packed", "idle", 0.0, 0.0], ["deploying", "idle", 0.5, 0.0],
			["completed", "work", 0.0, 1.5], ["completed", "towed", 0.0, 0.0]]:
		_wp(wrapper, pose[1], pose[0], pose[2], "loaded", "none", pose[3])
		var active_error := _check_active_containment(wrapper, data)
		_check("wrapper_active_%s_%s" % [pose[0], pose[1]], active_error == "", active_error)
	wrapper.free()


func _wp(wrapper: Node, state: String, phase: String, phase_t: float, cargo: String,
		reason: String, time_s: float) -> String:
	return String(wrapper.call("apply_preview", {
		"state": state, "phase": phase, "phase_t": phase_t,
		"cargo": cargo, "reason": reason, "time_s": time_s,
	}))


func _negative_manifest_cases() -> void:
	DirAccess.make_dir_recursive_absolute(_tmp_root())
	var mutators := [
		["missing_id", func(d: Dictionary): d.erase("id"), "missing or empty string field: id"],
		["revision_zero", func(d: Dictionary): d["revision"] = 0, "positive integer"],
		["unit_wrong", func(d: Dictionary): d["unit"] = "foot", "unit"],
		["up_wrong", func(d: Dictionary): d["up"] = "+Z", "up"],
		["forward_wrong", func(d: Dictionary): d["forward"] = "+Z", "forward"],
		["bad_hash", func(d: Dictionary): d["glb_sha256"] = "0".repeat(64), "sha256 mismatch"],
		["short_hash", func(d: Dictionary): d["glb_sha256"] = "abcd", "64 hex"],
		["missing_glb", func(d: Dictionary): d["glb"] = "res://scenes/art_preview_r1/missing.glb", "GLB unreadable"],
		["role_unknown", func(d: Dictionary): d["material_bindings"][0]["role"] = "gold", "unknown role"],
		["state_missing", func(d: Dictionary): d["states"].erase("towed"), "seven contract states"],
		["state_bad_mode", func(d: Dictionary): d["states"]["idle"]["mode"] = "auto", "invalid mode"],
		["state_clipless_duration", func(d: Dictionary): d["states"]["idle"]["duration_s"] = 2.0, "duration_s must be 0"],
		["psm_hole", func(d: Dictionary): d["phase_state_modes"].erase("packed"), "missing phase: packed"],
		["source_missing", func(d: Dictionary): d["source"].erase("hash"), "source missing field: hash"],
		["source_files_type", func(d: Dictionary): d["source"]["files"] = 0, "must be an array"],
		["source_file_type", func(d: Dictionary): d["source"]["files"] = [0], "must be objects"],
		["source_file_path_type", func(d: Dictionary): d["source"]["files"] = [{"path": 0, "sha256": "a".repeat(64)}], "path must be a non-empty string"],
		["source_file_path_empty", func(d: Dictionary): d["source"]["files"] = [{"path": " ", "sha256": "a".repeat(64)}], "path must be a non-empty string"],
		["source_file_hash_type", func(d: Dictionary): d["source"]["files"] = [{"path": "source.py", "sha256": 0}], "sha256 must be a string"],
		["source_file_hash_invalid", func(d: Dictionary): d["source"]["files"] = [{"path": "source.py", "sha256": "z".repeat(64)}], "64 hex characters"],
		["no_bindings", func(d: Dictionary): d["material_bindings"] = [], "non-empty array"],
		["binding_dup", func(d: Dictionary): d["material_bindings"].append(
				d["material_bindings"][0].duplicate()), "duplicate material binding"],
	]
	for case: Variant in mutators:
		var d: Dictionary = _st_base_manifest.duplicate(true)
		case[1].call(d)
		var res := _load_manifest(_write_tmp("neg_%s.json" % case[0], JSON.stringify(d)))
		_check("manifest_neg_%s" % case[0], not res["ok"] and String(res["error"]).contains(String(case[2])),
				String(res["error"]))

	var bad_json := _write_tmp("neg_bad_json.json", "{\"id\": ")
	var no_files: Dictionary = _st_base_manifest.duplicate(true)
	no_files["source"].erase("files")
	var no_files_res := _load_manifest(_write_tmp("source_files_optional.json", JSON.stringify(no_files)))
	_check("manifest_source_files_optional", no_files_res["ok"], String(no_files_res["error"]))
	var res_bad := _load_manifest(bad_json)
	_check("manifest_neg_bad_json", not res_bad["ok"] and String(res_bad["error"]).contains("valid JSON"),
			String(res_bad["error"]))
	var empty_json := _write_tmp("neg_empty.json", "   \n")
	var res_empty := _load_manifest(empty_json)
	_check("manifest_neg_empty_file", not res_empty["ok"] and String(res_empty["error"]).contains("empty"),
			String(res_empty["error"]))
	var res_missing := _load_manifest(_tmp_root().path_join("does_not_exist.json"))
	_check("manifest_neg_missing_file", not res_missing["ok"] and String(res_missing["error"]).contains("cannot open"),
			String(res_missing["error"]))

	# Bind-stage failures need a manifest that parses but cannot bind.
	var bind_cases := [
		["node_missing", func(d: Dictionary): d["material_bindings"][0]["node"] = "Model/Nope", "binding node not found"],
		["surface_range", func(d: Dictionary): d["material_bindings"][0]["surface"] = 7, "out of range"],
		["coverage_missing", func(d: Dictionary): d["material_bindings"].pop_back(), "unbound surface"],
	]
	for case: Variant in bind_cases:
		var d: Dictionary = _st_base_manifest.duplicate(true)
		case[1].call(d)
		var res := _load_manifest(_write_tmp("negb_%s.json" % case[0], JSON.stringify(d)))
		if not _check("bind_neg_%s_load" % case[0], res["ok"], String(res["error"])):
			continue
		var inst := _instantiate_sample(res["data"])
		if not _check("bind_neg_%s_inst" % case[0], inst["ok"], String(inst["error"])):
			continue
		var berr := _bind_materials(inst["wrapper"], res["data"], {})
		_check("bind_neg_%s" % case[0], berr != "" and berr.contains(String(case[2])), berr)
		(inst["wrapper"] as Node).free()

	# A scene without the wrapper contract must be rejected.
	var glb_only := {"preview_scene": _st_base_manifest["glb"], "sockets": []}
	var inst_raw := _instantiate_sample(glb_only)
	_check("bind_neg_wrapper_missing", not inst_raw["ok"] and String(inst_raw["error"]).contains("SampleRoot"),
			String(inst_raw["error"]))

	# preview_scene must be a scene, not any loadable resource.
	var d_res: Dictionary = _st_base_manifest.duplicate(true)
	d_res["preview_scene"] = "res://assets/materials/art-r1/body_light.tres"
	var inst_res := _instantiate_sample(d_res)
	_check("inst_neg_wrong_resource_type", not inst_res["ok"] and String(inst_res["error"]).contains("not a scene"),
			String(inst_res["error"]))

	# A manifest whose glb field is a different real file (hash-consistent) must
	# be rejected because Model's actual origin does not match.
	var d_glb: Dictionary = _st_base_manifest.duplicate(true)
	d_glb["glb"] = "res://scenes/art_preview_r1/probe_sample_r1.tscn"
	d_glb["glb_sha256"] = _sha256_file(String(d_glb["glb"]))
	var res_glb := _load_manifest(_write_tmp("negb_glb_identity.json", JSON.stringify(d_glb)))
	if _check("inst_neg_glb_identity_load", res_glb["ok"], String(res_glb["error"])):
		var inst_glb := _instantiate_sample(res_glb["data"])
		_check("inst_neg_glb_identity", not inst_glb["ok"]
				and String(inst_glb["error"]).contains("does not match manifest glb"),
				String(inst_glb["error"]))

	# A non-identity SampleRoot (e.g. pre-scaled wrapper scene) must be rejected.
	var inst_scaled := (load(_st_base_manifest["preview_scene"]) as PackedScene).instantiate()
	(inst_scaled as Node3D).scale = Vector3(2, 2, 2)
	var scaled_err := _validate_wrapper_root(inst_scaled)
	_check("inst_neg_scaled_root", scaled_err != "" and scaled_err.contains("identity"), scaled_err)
	inst_scaled.free()

	# A wrapper that only implements the apply_preview contract must work; the
	# preview may not call extra methods on it.
	var script_ok := GDScript.new()
	script_ok.source_code = "extends Node3D\nfunc apply_preview(preset: Dictionary) -> String:\n\treturn \"\"\n"
	script_ok.reload()
	var minimal := Node3D.new()
	minimal.name = "SampleRoot"
	minimal.set_script(script_ok)
	var minimal_ret := _apply_preset(minimal, params, _st_base_manifest)
	_check("inst_pos_apply_only_contract", minimal_ret["error"] == "" and String(minimal_ret["mode"]) == "static",
			str(minimal_ret))
	minimal.free()
	var script_nil := GDScript.new()
	script_nil.source_code = "extends Node3D\nfunc apply_preview(preset):\n\treturn null\n"
	script_nil.reload()
	var nil_wrapper := Node3D.new()
	nil_wrapper.name = "SampleRoot"
	nil_wrapper.set_script(script_nil)
	var nil_ret := _apply_preset(nil_wrapper, params, _st_base_manifest)
	_check("inst_neg_apply_returns_nil", nil_ret["error"] != "" and String(nil_ret["error"]).contains("String"),
			str(nil_ret))
	nil_wrapper.free()

	# Whole-mesh covers must be rejected before any binding happens.
	for cover_case: Variant in [
		["material_override", func(mi: MeshInstance3D, mat: Material): mi.material_override = mat],
		["material_overlay", func(mi: MeshInstance3D, mat: Material): mi.material_overlay = mat],
	]:
		var inst_cov := _instantiate_sample(_st_base_manifest)
		if inst_cov["ok"]:
			var body_cov := (inst_cov["wrapper"] as Node).get_node("Model/Body") as MeshInstance3D
			cover_case[1].call(body_cov, StandardMaterial3D.new())
			var cov_err := _bind_materials(inst_cov["wrapper"], _st_base_manifest, {})
			_check("bind_neg_%s" % cover_case[0], cov_err != "" and cov_err.contains(cover_case[0]), cov_err)
			(inst_cov["wrapper"] as Node).free()

	# A chained next_pass on the role material would cover the shared resource.
	var poisoned := (load("%s/body_light.tres" % MATERIAL_DIR) as StandardMaterial3D).duplicate()
	poisoned.next_pass = StandardMaterial3D.new()
	var inst_np := _instantiate_sample(_st_base_manifest)
	if inst_np["ok"]:
		var np_err := _bind_materials(inst_np["wrapper"], _st_base_manifest, {"body_light": poisoned})
		_check("bind_neg_next_pass", np_err != "" and np_err.contains("next_pass"), np_err)
		(inst_np["wrapper"] as Node).free()

	# Declared static bounds must match the actually measured pose, not merely
	# be contained in loaded (a fake point static must fail).
	var inst_fb := _instantiate_sample(_st_base_manifest)
	if inst_fb["ok"]:
		var fake: Dictionary = _st_base_manifest.duplicate(true)
		fake["bounds"]["static"] = {"min": [0.0, 0.1, 0.0], "max": [0.0, 0.1, 0.0]}
		var fb_err := _check_bounds(inst_fb["wrapper"], fake)
		_check("bounds_neg_fake_static", fb_err != "" and fb_err.contains("bounds.static"), fb_err)
		(inst_fb["wrapper"] as Node).free()


# ----------------------------------------------------- slice B self-test cases
# Headless-checkable view/IO logic only. Real-window rendering, capture
# refusal, PNG content and interactive GUI behavior are verified as real
# commands (see README); this section cannot sign those.

func _b_cases() -> void:
	_b_view_cases()
	_b_validation_cases()
	_b_name_and_dir_cases()
	_b_record_cases()
	_b_overlay_cases()


func _b_view_cases() -> void:
	var vp := _ensure_world()
	var entity := _entity_root()
	_check("b_entity_under_world", entity != null
			and entity.get_node_or_null("VisualRoot") != null)
	_check("b_view_size", vp.size == Vector2i(ViewR1.VIEWPORT_WIDTH, ViewR1.VIEWPORT_HEIGHT),
			str(vp.size))
	_check("b_view_msaa", vp.msaa_3d == Viewport.MSAA_4X, str(vp.msaa_3d))
	_check("b_view_scale1", absf(vp.scaling_3d_scale - 1.0) < 0.000001, str(vp.scaling_3d_scale))
	_check("b_view_fxaa_off", vp.screen_space_aa == Viewport.SCREEN_SPACE_AA_DISABLED,
			str(vp.screen_space_aa))
	_check("b_view_taa_off", not vp.use_taa)
	_check("b_view_own_world", vp.own_world_3d)

	var used := ViewR1.place_camera(_cam, "normal")
	_check("b_cam_normal", used == "normal" and _cam.position == ViewR1.CAM_NORMAL_POS
			and absf(_cam.fov - 60.0) < 0.000001, "%s %s fov=%s" % [used, _cam.position, _cam.fov])
	used = ViewR1.place_camera(_cam, "close")
	var close_aim := (ViewR1.CAM_CLOSE_TARGET - ViewR1.CAM_CLOSE_POS).normalized()
	_check("b_cam_close", used == "close" and _cam.position == ViewR1.CAM_CLOSE_POS
			and absf(_cam.fov - 50.0) < 0.000001, "%s %s fov=%s" % [used, _cam.position, _cam.fov])
	_check("b_cam_close_aim", _cam.global_transform.basis.z.dot(-close_aim) > 0.999999,
			str(_cam.global_transform.basis.z))
	used = ViewR1.place_camera(_cam, "board")
	_check("b_cam_board", used == "board" and _cam.position == ViewR1.CAM_BOARD_POS
			and absf(_cam.fov - 50.0) < 0.000001, "%s %s" % [used, _cam.position])
	_check("b_cam_props", absf(_cam.near - 0.05) < 0.000001 and absf(_cam.far - 200.0) < 0.000001
			and _cam.keep_aspect == Camera3D.KEEP_HEIGHT
			and _cam.projection == Camera3D.PROJECTION_PERSPECTIVE,
			"near=%s far=%s" % [_cam.near, _cam.far])
	_check("b_place_unknown", ViewR1.place_camera(_cam, "top") == "")
	ViewR1.place_camera(_cam, "normal")

	var sun_rot := _sun.rotation_degrees
	_check("b_light_props", absf(sun_rot.x - (-55.0)) < 0.0001 and absf(sun_rot.y - (-30.0)) < 0.0001
			and absf(sun_rot.z - 0.0) < 0.0001 and absf(_sun.light_energy - 1.0) < 0.000001
			and _sun.light_color == Color("#ffffff") and _sun.shadow_enabled
			and absf(_sun.directional_shadow_max_distance - 80.0) < 0.000001, str(sun_rot))

	_set_yaw(90)
	_check("b_yaw_applies", absf(_entity_root().rotation.y - deg_to_rad(90.0)) < 0.000001
			and _entity_root().get_node("VisualRoot").transform == Transform3D.IDENTITY,
			str(_entity_root().rotation.y))
	_check("b_light_untouched_by_yaw", _sun.rotation_degrees == sun_rot and _sun.position == Vector3.ZERO,
			"only the object turns, light must not move")
	_set_yaw(0)

	var env: Environment = _wenv.environment
	_check("b_env_props", env.background_mode == Environment.BG_COLOR
			and env.background_color == Color("#aeb8bc")
			and absf(env.background_energy_multiplier - 1.0) < 0.000001
			and env.ambient_light_source == Environment.AMBIENT_SOURCE_COLOR
			and env.ambient_light_color == Color("#d5dbdf")
			and absf(env.ambient_light_energy - 0.60) < 0.000001
			and absf(env.ambient_light_sky_contribution - 0.0) < 0.000001
			and env.reflected_light_source == Environment.REFLECTION_SOURCE_DISABLED
			and env.tonemap_mode == Environment.TONE_MAPPER_LINEAR
			and absf(env.tonemap_exposure - 1.0) < 0.000001,
			"bg=%s ambient=%s" % [env.background_color, env.ambient_light_color])
	_check("b_env_fx_off", not env.glow_enabled and not env.ssao_enabled
			and not env.ssil_enabled and not env.ssr_enabled and not env.fog_enabled
			and not env.volumetric_fog_enabled and not env.sdfgi_enabled)
	# CameraAttributes hangs off the Camera3D/WorldEnvironment, not the
	# Environment resource (ClassDB probe 2026-10-04).
	_check("b_no_camera_attributes", _cam.attributes == null and _wenv.camera_attributes == null,
			"cam=%s world_env=%s" % [_cam.attributes, _wenv.camera_attributes])

	ViewR1.request_shadow_atlas()
	var snap := ViewR1.snapshot(vp, _cam, _sun, _wenv, "normal")
	_check("b_shadow_atlas_request_recorded", snap["shadow_atlas"]["requested_size"] == 4096
			and String(snap["shadow_atlas"]["readback"]).contains("unavailable"),
			str(snap["shadow_atlas"]))
	var sview: Dictionary = snap["viewport"]
	_check("b_snapshot_viewport", int(sview["width"]) == 1920 and int(sview["height"]) == 1200
			and String(sview["msaa_3d"]) == "4x" and absf(float(sview["scaling_3d_scale"]) - 1.0) < 1e-9
			and String(sview["screen_space_aa"]) == "disabled" and not bool(sview["use_taa"]),
			str(sview))
	var srender: Dictionary = snap["render"]
	_check("b_snapshot_render", String(srender["engine_version"]) != ""
			and String(srender["display_server"]) != ""
			and (String(srender["display_server"]) == "headless" or String(srender["adapter_name"]) != "")
			and String(srender["rendering_method_actual"]) != ""
			and String(srender["rendering_driver_actual"]) != "",
			str(srender))
	var scam: Dictionary = snap["camera"]
	_check("b_snapshot_camera", String(scam["keep_aspect"]) == "keep_height"
			and absf(float(scam["near"]) - 0.05) < 1e-9 and absf(float(scam["far"]) - 200.0) < 1e-9
			and String(scam["attributes"]) == "none",
			str(scam))
	var slight: Dictionary = snap["light"]
	_check("b_snapshot_light", bool(slight["shadow_enabled"])
			and absf(float(slight["directional_shadow_max_distance"]) - 80.0) < 1e-9
			and (slight["soft_shadow_filter_quality"] as Dictionary).has("note"),
			str(slight))
	var swe: Dictionary = snap["world_environment"]
	_check("b_snapshot_world_env", String(swe["camera_attributes"]) == "none", str(swe))
	var senv: Dictionary = snap["environment"]
	_check("b_snapshot_env", String(senv["background_color"]) == "aeb8bc"
			and String(senv["ambient_light_color"]) == "d5dbdf"
			and String(senv["reflected_light_source"]) == "disabled"
			and String(senv["tonemap_mode"]) == "linear",
			str(senv))


func _b_validation_cases() -> void:
	var manifest := String(params["manifest_path"])
	var gui_ok := _typed_params({"camera": "normal", "yaw": "90", "state": "work",
			"phase": "deploying", "phase-t": "0.25", "cargo": "loaded",
			"reason": "none", "time": "1.5", "labels": "blind"})
	_check("b_typed_gui_ok", gui_ok["error"] == "" and int(gui_ok["params"]["yaw"]) == 90
			and absf(float(gui_ok["params"]["phase_t"]) - 0.25) < 1e-9
			and absf(float(gui_ok["params"]["time_s"]) - 1.5) < 1e-9
			and String(gui_ok["params"]["labels"]) == "blind", String(gui_ok["error"]))
	var gui_cases := [
		[{"camera": "top"}, "camera"],
		[{"yaw": "45"}, "yaw"],
		[{"state": "offline"}, "offline is rejected"],
		[{"state": "idle", "reason": "no_power"}, "only applies"],
		[{"time": ""}, "not a valid number"],
		[{"time": "nan"}, "not a valid number"],
		[{"time": "-1"}, "non-negative"],
		[{"phase": "deploying", "phase-t": "1.5"}, "[0,1]"],
		[{"labels": "naked"}, "labels"],
	]
	var idx := 0
	for case: Variant in gui_cases:
		idx += 1
		var values := {"camera": "normal", "yaw": "0", "state": "idle", "phase": "completed",
				"phase-t": "0", "cargo": "empty", "reason": "none", "time": "0", "labels": "evidence"}
		values.merge(case[0], true)
		var typed := _typed_params(values)
		_check("b_typed_gui_neg_%02d" % idx, typed["error"] != ""
				and String(typed["error"]).contains(String(case[1])), String(typed["error"]))
	var parse_capture := parse_args(["--manifest", manifest, "--capture-dir", "/tmp/some-new-run"])
	_check("b_parse_capture_dir_pos", parse_capture["error"] == ""
			and String(parse_capture["params"]["capture_dir"]) == "/tmp/some-new-run",
			String(parse_capture["error"]))


func _b_name_and_dir_cases() -> void:
	var p := {"mode": "manifest", "camera": "normal", "yaw": 90, "state": "idle",
			"phase": "completed", "phase_t": 0.0, "cargo": "empty", "reason": "none",
			"time_s": 0.0, "labels": "evidence"}
	_check("b_filename_evidence", Capture.evidence_base_name(p, "probe_r1")
			== "probe_r1_normal_yaw090_idle_completed_pt0p000_empty_none_t0p000",
			Capture.evidence_base_name(p, "probe_r1"))
	var p_board := p.duplicate()
	p_board["mode"] = "board"
	_check("b_filename_board", Capture.evidence_base_name(p_board, "material_board") == "board",
			Capture.evidence_base_name(p_board, "material_board"))
	_check("b_filename_blind", Capture.blind_base_name() == "blind_0001")
	var p_blind := p.duplicate()
	p_blind["labels"] = "blind"
	_check("b_base_name_labels", Capture.base_name(p_blind, "probe_r1") == "blind_0001"
			and Capture.base_name(p, "probe_r1") != "blind_0001")

	var btmp := "/private/tmp/yudian-preview-b-selftest-%d" % OS.get_process_id()
	DirAccess.make_dir_recursive_absolute(btmp)
	_check("b_dir_missing_ok", Capture.capture_dir_error(btmp + "/new-run") == "",
			Capture.capture_dir_error(btmp + "/new-run"))
	var asfile := btmp + "/asfile"
	var f := FileAccess.open(asfile, FileAccess.WRITE)
	f.close()
	_check("b_dir_file_refused", Capture.capture_dir_error(asfile).contains("exists as a file"),
			Capture.capture_dir_error(asfile))
	DirAccess.make_dir_recursive_absolute(btmp + "/exists_empty")
	_check("b_dir_existing_refused", Capture.capture_dir_error(btmp + "/exists_empty").contains("already exists"),
			Capture.capture_dir_error(btmp + "/exists_empty"))
	DirAccess.make_dir_recursive_absolute(btmp + "/exists_full")
	var ff := FileAccess.open(btmp + "/exists_full/old.png", FileAccess.WRITE)
	ff.close()
	_check("b_dir_nonempty_refused", Capture.capture_dir_error(btmp + "/exists_full").contains("already exists"),
			Capture.capture_dir_error(btmp + "/exists_full"))
	_check("b_dir_empty_refused", Capture.capture_dir_error("").contains("empty"))
	_check("b_dir_create_ok", Capture.create_capture_dir(btmp + "/created") == ""
			and DirAccess.dir_exists_absolute(btmp + "/created"))
	_check("b_dir_create_then_collision", Capture.capture_dir_error(btmp + "/created").contains("already exists"))
	var da := DirAccess.open(btmp)
	if da != null:
		for name in da.get_files():
			da.remove(name)
		for sub in da.get_directories():
			var sub_da := DirAccess.open(btmp + "/" + sub)
			for name in sub_da.get_files():
				sub_da.remove(name)
			DirAccess.remove_absolute(btmp + "/" + sub)
		DirAccess.remove_absolute(btmp)


func _b_record_cases() -> void:
	var ctx := {
		"params": params, "asset_id": "probe_r1", "revision": 1, "data": _st_base_manifest,
		"applied": {"mode": "static", "mode_reason": "rest", "na_reason": ""},
		"view": {"dry": true}, "roots": {"dry": true},
		"project_dir": ProjectSettings.globalize_path("res://."),
		"png_name": "x.png", "json_name": "x.json",
		"manifest_path": String(params["manifest_path"]),
	}
	var record := Capture.build_record(self, ctx)
	for key in ["schema", "asset", "mode", "labels", "requested", "applied", "view",
			"roots", "git", "hashes", "files"]:
		if not _check("b_record_key_%s" % key, record.has(key)):
			return
	_check("b_record_request_applied", record["requested"]["state"] == params["state"]
			and record["applied"]["mode"] == "static", str(record["requested"]))
	var git: Dictionary = record["git"]
	_check("b_record_git_head", not String(git["head"]).begins_with("unavailable")
			and String(git["head"]).length() == 40, str(git))
	var materials: Dictionary = record["hashes"]["materials"]
	var mat_ok := materials.size() == 6
	for role in materials:
		mat_ok = mat_ok and String(materials[role]["sha256"]).length() == 64
	_check("b_record_materials", mat_ok, str(materials.keys()))
	var glb_hash: Dictionary = record["hashes"]["glb"]
	_check("b_record_glb_actual", String(glb_hash["actual_sha256"]) == String(glb_hash["declared_sha256"])
			and String(glb_hash["actual_sha256"]).length() == 64, str(glb_hash))
	var manifest_hash: Dictionary = record["hashes"]["manifest"]
	_check("b_record_manifest_actual", String(manifest_hash["actual_sha256"]).length() == 64
			and String(manifest_hash["actual_sha256"]) == _sha256_file(String(params["manifest_path"])),
			str(manifest_hash))
	_check("b_record_config", (record["hashes"]["preview_config"] as Dictionary).size() >= 7)
	var source_files: Array = record["hashes"]["source"]["files"]
	_check("b_record_source_files_match", not source_files.is_empty()
			and bool(source_files[0]["match"]) and String(source_files[0]["actual_sha256"]) != "",
			str(source_files))
	var manifest_text := FileAccess.get_file_as_string(String(params["manifest_path"]))
	var manifest_sha := _sha256_file(String(params["manifest_path"]))
	_check("b_manifest_no_self_hash", manifest_sha.length() == 64
			and not manifest_text.contains(manifest_sha),
			"the manifest must not record its own hash")


func _b_overlay_cases() -> void:
	_ensure_overlay()
	_check("b_overlay_in_world", _overlay != null and _overlay.get_parent() == _world)
	_overlay.set_info_text("asset probe_r1 · rev 1")
	_check("b_overlay_info_text", _overlay.info_text.contains("probe_r1")
			and _overlay._info_label.text.contains("probe_r1"))
	_overlay.set_labels_mode("blind")
	_check("b_overlay_blind_hides", not _overlay._info_label.visible
			and _overlay.labels_mode == "blind")
	_overlay.set_labels_mode("evidence")
	_check("b_overlay_evidence_shows", _overlay._info_label.visible)
	var icon_map := [
		["no_power", OverlayScript.REASON_BATTERY],
		["mechanical", OverlayScript.REASON_WRENCH],
		["both", OverlayScript.REASON_BOTH],
		["return_charge", OverlayScript.REASON_ARROW],
		["none", OverlayScript.REASON_NONE],
	]
	var icons_ok := true
	for entry: Variant in icon_map:
		_overlay.set_reason(String(entry[0]))
		icons_ok = icons_ok and _overlay._icon_kind == int(entry[1])
	_check("b_overlay_reason_icons", icons_ok)


func _tmp_root() -> String:
	if _tmp_dir.is_empty():
		_tmp_dir = "/private/tmp/yudian-preview-a-selftest-%d" % OS.get_process_id()
	return _tmp_dir


func _write_tmp(name: String, text: String) -> String:
	var path := _tmp_root().path_join(name)
	var f := FileAccess.open(path, FileAccess.WRITE)
	f.store_string(text)
	f.close()
	return path


func _cleanup_tmp() -> void:
	if _tmp_dir.is_empty() or not DirAccess.dir_exists_absolute(_tmp_dir):
		return
	var da := DirAccess.open(_tmp_dir)
	if da == null:
		return
	for fname in da.get_files():
		da.remove(fname)
	DirAccess.remove_absolute(_tmp_dir)
