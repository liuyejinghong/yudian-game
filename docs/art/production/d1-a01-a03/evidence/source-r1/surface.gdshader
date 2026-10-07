shader_type spatial;
render_mode cull_back;

// The caller supplies a stage fact; this shader never changes height or stage.
uniform int stage : hint_range(0, 2) = 0;
uniform vec2 region_center = vec2(0.0);
uniform float region_radius = 2.0;
uniform vec4 soil_color : source_color = vec4(0.58, 0.32, 0.21, 1.0);
uniform vec4 gravel_color : source_color = vec4(0.43, 0.40, 0.34, 1.0);
uniform vec4 compacted_color : source_color = vec4(0.56, 0.43, 0.29, 1.0);
varying vec3 world_position;

float grain(vec2 point) {
	return fract(sin(dot(point, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 ground = world_position.xz;
	vec2 pixel_span = fwidth(ground);
	float footprint = max(pixel_span.x, pixel_span.y);
	float fine_fade = 1.0 - smoothstep(0.35, 1.0, footprint * 32.0);
	float stone_fade = 1.0 - smoothstep(0.35, 1.0, footprint * 8.0);
	float fine = grain(floor(ground * 32.0));
	float stones = step(0.74, grain(floor(ground * 8.0)));
	vec3 base = soil_color.rgb;
	float variation = (fine - 0.5) * 0.014 * fine_fade;
	if (stage == 1 && distance(ground, region_center) <= region_radius) {
		base = gravel_color.rgb;
		variation += (stones - 0.26) * 0.06 * stone_fade;
	} else if (stage == 2 && distance(ground, region_center) <= region_radius) {
		base = compacted_color.rgb;
		variation *= 0.4;
		variation += sin(ground.x * 12.0) * 0.012 * (1.0 - smoothstep(0.35, 1.0, footprint * 12.0));
	}
	ALBEDO = clamp(base + variation, vec3(0.0), vec3(1.0));
	ROUGHNESS = 0.93;
}
