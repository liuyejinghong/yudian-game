shader_type spatial;
render_mode cull_back;

// Height, stage and committed nodes come from the caller.
uniform int stage : hint_range(0, 2) = 0;
uniform sampler2D committed_mask : filter_nearest, repeat_disable;
uniform bool use_committed_mask = false;
uniform vec2 world_origin = vec2(-30.0);
uniform vec2 world_span = vec2(60.0);
uniform vec2 region_center = vec2(0.0);
uniform float region_radius = 2.0;
uniform vec4 soil_color : source_color = vec4(0.58, 0.32, 0.21, 1.0);
uniform vec4 gravel_color : source_color = vec4(0.43, 0.40, 0.34, 1.0);
uniform vec4 compacted_color : source_color = vec4(0.56, 0.43, 0.29, 1.0);
varying vec3 world_position;
varying vec3 world_normal;

float grain(vec2 point) {
	return fract(sin(dot(point, vec2(127.1, 311.7))) * 43758.5453);
}

float soil_noise(vec2 point) {
	vec2 cell = floor(point);
	vec2 blend = fract(point);
	blend = blend * blend * (3.0 - 2.0 * blend);
	return mix(mix(grain(cell), grain(cell + vec2(1.0, 0.0)), blend.x),
		mix(grain(cell + vec2(0.0, 1.0)), grain(cell + vec2(1.0)), blend.x), blend.y);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
	vec2 ground = world_position.xz;
	float footprint = max(fwidth(ground).x, fwidth(ground).y);
	float fine_fade = 1.0 - smoothstep(0.35, 1.0, footprint * 24.0);
	float stone_fade = 1.0 - smoothstep(0.35, 1.0, footprint * 7.0);
	float broad = soil_noise(ground * 0.18);
	float mottles = soil_noise(ground * 0.9 + vec2(21.7, 8.3));
	float fine = grain(floor(ground * 24.0));
	vec2 stone_cell = floor(ground * 7.0);
	vec2 stone_center = vec2(grain(stone_cell + vec2(31.0, 11.0)), grain(stone_cell + vec2(9.0, 57.0)));
	float stones = (1.0 - smoothstep(0.1, 0.25, distance(fract(ground * 7.0), stone_center))) * step(0.6, grain(stone_cell));
	float slope = smoothstep(0.07, 0.55, 1.0 - abs(normalize(world_normal).y));
	float strata = sin(world_position.y * 22.0 + broad * 1.5) * 0.5 + 0.5;
	vec3 base = soil_color.rgb;
	float variation = (fine - 0.5) * 0.009 * fine_fade;
	bool in_region = distance(ground, region_center) <= region_radius;
	bool compacted = !use_committed_mask && stage == 2 && in_region;
	if (use_committed_mask) {
		vec2 uv = (ground - world_origin) / world_span;
		if (all(greaterThanEqual(uv, vec2(0.0))) && all(lessThanEqual(uv, vec2(1.0)))) {
			// Grid nodes map to texel centers, including both world endpoints.
			vec2 dimensions = vec2(textureSize(committed_mask, 0));
			vec2 texel_uv = (uv * (dimensions - vec2(1.0)) + vec2(0.5)) / dimensions;
			compacted = texture(committed_mask, texel_uv).r > 0.5;
		}
	}
	if (compacted) {
		base = compacted_color.rgb;
		variation *= 0.3;
		variation += (mottles - 0.5) * 0.025;
		variation += sin(ground.x * 9.0) * 0.015 * (1.0 - smoothstep(0.35, 1.0, footprint * 9.0));
	} else if (stage == 1 && in_region) {
		base = gravel_color.rgb;
		variation += (mottles - 0.5) * 0.08 + (stones - 0.03) * 0.045 * stone_fade;
	} else {
		base = mix(base, vec3(0.40, 0.23, 0.17), slope * 0.65);
		variation += (broad - 0.5) * 0.045 + (mottles - 0.5) * 0.018;
	}
	// Strata follow actual sloping faces, including committed cut edges.
	variation += slope * ((strata - 0.5) * 0.03 - 0.012);
	ALBEDO = clamp(base + variation, vec3(0.0), vec3(1.0));
	ROUGHNESS = compacted ? 0.86 : 0.96;
}
