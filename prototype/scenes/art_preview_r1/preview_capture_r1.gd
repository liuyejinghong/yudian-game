extends RefCounted
## ART-PREVIEW-01 slice B: capture IO — capture-dir guard, deterministic file
## names, PNG/JSON writing and the evidence record (git state, GLB/manifest/
## source/source.files/materials/preview-config hashes, actual viewport and
## render properties, preset and applied mode).
##
## Contract rules implemented here: the capture dir must not exist (collision
## refuses, nothing is overwritten); write failures exit non-zero with no PASS
## marker; the manifest never records its own hash — the capture record lists
## it instead; blind captures use neutral sequential file names, answers stay
## in the JSON sidecar only.

const MATERIAL_ROLES := ["body_light", "frame_dark", "rubber", "accent_warm", "solar_face", "soil_mars"]
const MATERIAL_DIR := "res://assets/materials/art-r1"

const PREVIEW_CONFIG_FILES := [
	"res://scenes/art_preview_r1/PreviewR1.tscn",
	"res://scenes/art_preview_r1/preview_r1.gd",
	"res://scenes/art_preview_r1/preview_view_r1.gd",
	"res://scenes/art_preview_r1/preview_overlay_r1.gd",
	"res://scenes/art_preview_r1/preview_panel_r1.gd",
	"res://scenes/art_preview_r1/preview_capture_r1.gd",
	"res://scenes/art_preview_r1/sample_wrapper.gd",
]


## The capture dir must not exist (file or directory): refuse on collision.
static func capture_dir_error(path: String) -> String:
	if path.is_empty():
		return "capture dir is empty"
	if FileAccess.file_exists(path):
		return "capture dir exists as a file, refusing: %s" % path
	if DirAccess.dir_exists_absolute(path):
		return "capture dir already exists, refusing to overwrite: %s" % path
	return ""


static func create_capture_dir(path: String) -> String:
	var err := DirAccess.make_dir_recursive_absolute(path)
	if err != OK:
		return "cannot create capture dir %s (%s)" % [path, error_string(err)]
	if DirAccess.dir_exists_absolute(path) == false:
		return "capture dir missing after creation attempt: %s" % path
	return ""


## Evidence names encode the request; blind names stay neutral/sequential.
static func evidence_base_name(p: Dictionary, asset_id: String) -> String:
	if p["mode"] == "board":
		return "board"
	return "%s_%s_yaw%03d_%s_%s_pt%s_%s_%s_t%s" % [
		asset_id, p["camera"], int(p["yaw"]), p["state"], p["phase"],
		_num_token(float(p["phase_t"])), p["cargo"], p["reason"], _num_token(float(p["time_s"])),
	]


static func blind_base_name() -> String:
	return "blind_0001"


static func base_name(p: Dictionary, asset_id: String) -> String:
	if p["labels"] == "blind":
		return blind_base_name()
	return evidence_base_name(p, asset_id)


static func _num_token(value: float) -> String:
	# "%.3f" keeps trailing zeros ("0p000"); String.num() would trim them.
	return ("%.3f" % value).replace(".", "p").replace("-", "m")


static func run_git(repo: String, args: PackedStringArray) -> Dictionary:
	var full := PackedStringArray(["-C", repo])
	full.append_array(args)
	var output := []
	var code := OS.execute("git", full, output, true)
	var text := ""
	if output.size() > 0:
		text = String(output[0]).strip_edges()
	return {"code": code, "output": text}


## Git HEAD + dirty flag of the worktree containing the prototype project.
## A git failure is recorded honestly instead of blocking the capture.
static func git_record(project_dir: String) -> Dictionary:
	var toplevel := run_git(project_dir, PackedStringArray(["rev-parse", "--show-toplevel"]))
	if int(toplevel["code"]) != 0:
		return {"head": "unavailable", "dirty": null,
				"error": "git rev-parse failed: %s" % String(toplevel["output"])}
	var repo := String(toplevel["output"])
	var head := run_git(repo, PackedStringArray(["rev-parse", "HEAD"]))
	var status := run_git(repo, PackedStringArray(["status", "--porcelain"]))
	return {
		"head": String(head["output"]) if int(head["code"]) == 0 else "unavailable",
		"dirty": null if int(status["code"]) != 0 else not String(status["output"]).is_empty(),
		"repo": repo,
	}


## Assemble the full capture record. `owner` must expose _sha256_file(path).
## ctx keys: params, asset_id, revision, data (manifest data or {}), applied,
## view (snapshot dict), roots, project_dir, png_name, json_name.
static func build_record(owner: Object, ctx: Dictionary) -> Dictionary:
	var p: Dictionary = ctx["params"]
	var data: Dictionary = ctx["data"]
	var record := {
		"schema": "art-preview-r1-capture/1",
		"asset": {"id": ctx["asset_id"], "revision": ctx["revision"]},
		"mode": p["mode"],
		"labels": p["labels"],
		"requested": {
			"camera": p["camera"], "yaw": int(p["yaw"]), "state": p["state"],
			"phase": p["phase"], "phase_t": float(p["phase_t"]), "cargo": p["cargo"],
			"reason": p["reason"], "time_s": float(p["time_s"]),
		},
		"applied": {
			"mode": ctx["applied"].get("mode", ""),
			"mode_reason": ctx["applied"].get("mode_reason", ""),
			"na_reason": ctx["applied"].get("na_reason", ""),
		},
		"view": ctx["view"],
		"roots": ctx["roots"],
		"files": {"png": ctx["png_name"], "json": ctx["json_name"]},
	}
	if p["mode"] == "board":
		record["asset"]["note"] = "material trial board has no manifest revision"
	var git := git_record(String(ctx["project_dir"]))
	record["git"] = git
	ctx["git_repo"] = String(git.get("repo", ctx["project_dir"]))
	record["hashes"] = hashes_record(owner, ctx)
	return record


static func hashes_record(owner: Object, ctx: Dictionary) -> Dictionary:
	var data: Dictionary = ctx["data"]
	var hashes := {"materials": {}, "preview_config": {}}
	for role in MATERIAL_ROLES:
		var path := "%s/%s.tres" % [MATERIAL_DIR, role]
		hashes["materials"][role] = {"path": path, "sha256": owner._sha256_file(path)}
	for path in PREVIEW_CONFIG_FILES:
		hashes["preview_config"][path] = {"sha256": owner._sha256_file(path)}
	if not data.is_empty():
		# Actual file hashes read here (GLB res:// path, manifest from disk);
		# the declared manifest values are cross-checked, never copied.
		hashes["glb"] = {
			"path": String(data["glb"]),
			"declared_sha256": String(data["glb_sha256"]),
			"actual_sha256": owner._sha256_file(String(data["glb"])),
		}
		var manifest_path := String(ctx.get("manifest_path", ""))
		if manifest_path != "":
			hashes["manifest"] = {
				"path": manifest_path,
				"actual_sha256": owner._sha256_file(manifest_path),
				"note": "recorded by the capture side only; the manifest cannot hash itself",
			}
		var source: Dictionary = data["source"]
		var source_record := {
			"declared_hash": source.get("hash", ""),
			"generator": source.get("generator", ""),
			"files": [],
		}
		var files: Variant = source.get("files", [])
		if files is Array:
			for f: Variant in files:
				var entry: Dictionary = f
				var rel := String(entry.get("path", ""))
				var repo := String(ctx.get("git_repo", ctx["project_dir"]))
				var abs_path := rel if rel.begins_with("/") else "%s/%s" % [repo, rel]
				var actual: String = owner._sha256_file(abs_path)
				source_record["files"].append({
					"path": rel,
					"declared_sha256": entry.get("sha256", ""),
					"actual_sha256": actual,
					"match": actual != "" and actual == String(entry.get("sha256", "")),
				})
		hashes["source"] = source_record
	return hashes


## Write the evidence triple: PNG, JSON record, capture-side hash manifest.
## Any write failure is returned as an error string; the caller must exit 1
## without printing a PASS marker.
static func write_capture(dir: String, base: String, img: Image, record: Dictionary) -> String:
	var png_path := dir.path_join(base + ".png")
	var write_err := img.save_png(png_path)
	if write_err != OK:
		return "PNG write failed: %s (%s)" % [png_path, error_string(write_err)]
	if not FileAccess.file_exists(png_path):
		return "PNG missing after write: %s" % png_path

	var json_path := dir.path_join(base + ".json")
	var json_text := JSON.stringify(record, "  ") + "\n"
	var json_err := _write_text(json_path, json_text)
	if json_err != "":
		return json_err

	var hash_manifest := {
		"schema": "art-preview-r1-capture-hashes/1",
		"note": "capture-side hash manifest; this file cannot contain its own hash",
		"files": {
			base + ".png": _file_hash_entry(png_path),
			base + ".json": _file_hash_entry(json_path),
		},
	}
	var manifest_err := _write_text(dir.path_join("capture_hashes.json"),
			JSON.stringify(hash_manifest, "  ") + "\n")
	if manifest_err != "":
		return manifest_err
	return ""


static func _file_hash_entry(path: String) -> Dictionary:
	var bytes := FileAccess.get_file_as_bytes(path)
	var ctx := HashingContext.new()
	ctx.start(HashingContext.HASH_SHA256)
	ctx.update(bytes)
	return {"sha256": ctx.finish().hex_encode(), "bytes": bytes.size()}


static func _write_text(path: String, text: String) -> String:
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		return "write failed: %s (%s)" % [path, error_string(FileAccess.get_open_error())]
	f.store_string(text)
	f.close()
	return ""
