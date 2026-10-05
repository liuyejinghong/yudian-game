extends RefCounted
## ART-PREVIEW-01 slice B: frozen r1 view construction — the SubViewport,
## cameras, directional light, environment and the shadow-atlas request.
## Values come from requirements/preview-r1.md (frozen); this file only wires
## and reads back properties, it does not decide art direction.
##
## Readback helpers return "actual" property values for the capture JSON;
## anything without a getter is recorded as requested-only, never claimed as
## verified.

const VIEWPORT_WIDTH := 1920
const VIEWPORT_HEIGHT := 1200

const CAM_NORMAL_POS := Vector3(18.3848, 28.0, 18.3848)
const CAM_NORMAL_TARGET := Vector3.ZERO
const CAM_NORMAL_FOV := 60.0
const CAM_CLOSE_POS := Vector3(3.0, 3.0, 4.0)
const CAM_CLOSE_TARGET := Vector3(0.0, 0.4, 0.0)
const CAM_CLOSE_FOV := 50.0
const CAM_BOARD_POS := Vector3(4.0, 4.0, 6.0)
const CAM_BOARD_TARGET := Vector3(0.0, 0.4, 0.0)
const CAM_BOARD_FOV := 50.0

const LIGHT_ROT_DEG := Vector3(-55.0, -30.0, 0.0)
const LIGHT_MAX_DISTANCE := 80.0
const SHADOW_ATLAS_SIZE := 4096

const BG_COLOR := Color("#aeb8bc")
const AMBIENT_COLOR := Color("#d5dbdf")
const AMBIENT_ENERGY := 0.60

const MSAA_NAMES := {0: "disabled", 1: "2x", 2: "4x", 3: "8x"}
const SSAA_NAMES := {0: "disabled", 1: "fxaa"}


static func configure_viewport(vp: SubViewport) -> void:
	vp.size = Vector2i(VIEWPORT_WIDTH, VIEWPORT_HEIGHT)
	vp.own_world_3d = true
	vp.msaa_3d = Viewport.MSAA_4X
	vp.scaling_3d_scale = 1.0
	vp.screen_space_aa = Viewport.SCREEN_SPACE_AA_DISABLED
	vp.use_taa = false
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.transparent_bg = false


static func build_camera() -> Camera3D:
	var cam := Camera3D.new()
	cam.name = "PreviewCamera"
	cam.projection = Camera3D.PROJECTION_PERSPECTIVE
	cam.keep_aspect = Camera3D.KEEP_HEIGHT
	cam.near = 0.05
	cam.far = 200.0
	return cam


## mode: "normal" | "close" | "board". Returns the actually used mode so the
## caller can record it; an unknown mode returns "" (the CLI/GUI validation
## rejects unknown cameras before this is reached).
static func place_camera(cam: Camera3D, mode: String) -> String:
	var used := mode
	match mode:
		"normal":
			cam.fov = CAM_NORMAL_FOV
			cam.look_at_from_position(CAM_NORMAL_POS, CAM_NORMAL_TARGET, Vector3.UP)
		"close":
			cam.fov = CAM_CLOSE_FOV
			cam.look_at_from_position(CAM_CLOSE_POS, CAM_CLOSE_TARGET, Vector3.UP)
		"board":
			used = "board"
			cam.fov = CAM_BOARD_FOV
			cam.look_at_from_position(CAM_BOARD_POS, CAM_BOARD_TARGET, Vector3.UP)
		_:
			used = ""
	return used


static func camera_target(mode: String) -> Vector3:
	match mode:
		"normal":
			return CAM_NORMAL_TARGET
		"close", "board":
			return CAM_CLOSE_TARGET
	return Vector3.ZERO


static func build_light() -> DirectionalLight3D:
	var sun := DirectionalLight3D.new()
	sun.name = "KeyLight"
	sun.light_color = Color("#ffffff")
	sun.light_energy = 1.0
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = LIGHT_MAX_DISTANCE
	sun.rotation_degrees = LIGHT_ROT_DEG
	return sun


static func build_environment() -> WorldEnvironment:
	var we := WorldEnvironment.new()
	we.name = "PreviewEnvironment"
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = BG_COLOR
	env.background_energy_multiplier = 1.0
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = AMBIENT_COLOR
	env.ambient_light_energy = AMBIENT_ENERGY
	env.ambient_light_sky_contribution = 0.0
	env.reflected_light_source = Environment.REFLECTION_SOURCE_DISABLED
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	env.tonemap_exposure = 1.0
	we.environment = env
	return we


## The RenderingServer shadow-atlas size has no getter, so the caller records
## "requested" only; this call is the contract-mandated runtime request.
static func request_shadow_atlas() -> void:
	RenderingServer.directional_shadow_atlas_set_size(SHADOW_ATLAS_SIZE, true)


static func _enum_name(map: Dictionary, value: int) -> String:
	return String(map.get(value, "unknown(%d)" % value))


## Read back the actual viewport/camera/light/environment properties for the
## capture record. Only real getters (ClassDB probe 2026-10-04: Camera3D has
## `attributes`, WorldEnvironment has `camera_attributes`, Environment has
## neither; DirectionalLight3D has no shadow-filter getter; RenderingServer
## has get_current_rendering_method/get_current_rendering_driver_name).
static func snapshot(vp: SubViewport, cam: Camera3D, sun: DirectionalLight3D,
		we: WorldEnvironment, camera_mode: String) -> Dictionary:
	var engine_info: Dictionary = Engine.get_version_info()
	var render := {
		"engine_version": String(engine_info.get("string", "unknown")),
		"engine_build": String(engine_info.get("build", "")),
		"display_server": DisplayServer.get_name(),
		"adapter_name": RenderingServer.get_video_adapter_name(),
		"adapter_vendor": RenderingServer.get_video_adapter_vendor(),
		"video_adapter_api_version": RenderingServer.get_video_adapter_api_version(),
		"rendering_method_actual": RenderingServer.get_current_rendering_method(),
		"rendering_driver_actual": RenderingServer.get_current_rendering_driver_name(),
	}

	var env: Environment = we.environment
	return {
		"viewport": {
			"width": vp.size.x,
			"height": vp.size.y,
			"own_world_3d": vp.own_world_3d,
			"msaa_3d": _enum_name(MSAA_NAMES, vp.msaa_3d),
			"msaa_3d_raw": vp.msaa_3d,
			"scaling_3d_scale": vp.scaling_3d_scale,
			"screen_space_aa": _enum_name(SSAA_NAMES, vp.screen_space_aa),
			"use_taa": vp.use_taa,
			"transparent_bg": vp.transparent_bg,
			"render_target_update_mode": vp.render_target_update_mode,
		},
		"render": render,
		"shadow_atlas": {
			"requested_size": SHADOW_ATLAS_SIZE,
			"readback": "unavailable (RenderingServer has no directional shadow atlas getter); requested value recorded",
		},
		"camera": {
			"mode_requested": camera_mode,
			"position": [cam.position.x, cam.position.y, cam.position.z],
			"rotation_degrees": [cam.rotation_degrees.x, cam.rotation_degrees.y, cam.rotation_degrees.z],
			"target": [camera_target(camera_mode).x, camera_target(camera_mode).y, camera_target(camera_mode).z],
			"fov": cam.fov,
			"keep_aspect": "keep_height" if cam.keep_aspect == Camera3D.KEEP_HEIGHT else "keep_width",
			"near": cam.near,
			"far": cam.far,
			"projection": "perspective" if cam.projection == Camera3D.PROJECTION_PERSPECTIVE else "orthogonal",
			"attributes": "none" if cam.attributes == null else "set (contract violation)",
		},
		"light": {
			"rotation_degrees": [sun.rotation_degrees.x, sun.rotation_degrees.y, sun.rotation_degrees.z],
			"light_color": sun.light_color.to_html(false),
			"light_energy": sun.light_energy,
			"shadow_enabled": sun.shadow_enabled,
			"directional_shadow_max_distance": sun.directional_shadow_max_distance,
			"soft_shadow_filter_quality": {
				"project_setting": "rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality",
				"value": ProjectSettings.get_setting(
						"rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality", null),
				"note": "DirectionalLight3D has no per-light filter getter; this preview does not override the project/engine default",
			},
		},
		"world_environment": {
			"camera_attributes": "none" if we.camera_attributes == null else "set (contract violation)",
		},
		"environment": {
			"background_mode": "color" if env.background_mode == Environment.BG_COLOR else "other(%d)" % env.background_mode,
			"background_color": env.background_color.to_html(false),
			"background_energy_multiplier": env.background_energy_multiplier,
			"ambient_light_source": "color" if env.ambient_light_source == Environment.AMBIENT_SOURCE_COLOR else "other(%d)" % env.ambient_light_source,
			"ambient_light_color": env.ambient_light_color.to_html(false),
			"ambient_light_energy": env.ambient_light_energy,
			"ambient_light_sky_contribution": env.ambient_light_sky_contribution,
			"reflected_light_source": "disabled" if env.reflected_light_source == Environment.REFLECTION_SOURCE_DISABLED else "other(%d)" % env.reflected_light_source,
			"tonemap_mode": "linear" if env.tonemap_mode == Environment.TONE_MAPPER_LINEAR else "other(%d)" % env.tonemap_mode,
			"tonemap_exposure": env.tonemap_exposure,
			"glow_enabled": env.glow_enabled,
			"ssao_enabled": env.ssao_enabled,
			"ssil_enabled": env.ssil_enabled,
			"ssr_enabled": env.ssr_enabled,
			"fog_enabled": env.fog_enabled,
			"volumetric_fog_enabled": env.volumetric_fog_enabled,
			"sdfgi_enabled": env.sdfgi_enabled,
		},
	}
