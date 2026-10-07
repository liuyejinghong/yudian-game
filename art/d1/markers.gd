extends RefCounted

const LIFT := 0.025
const STEP := 0.25
const COLORS := {
	"selected": Color("f5f1df"), "hover": Color("78d9e5"),
	"legal": Color("9ee3ac"), "illegal": Color("ff936c"),
	"committed": Color("edc77d")
}
const WIDTHS := {"selected": 0.10, "hover": 0.06, "legal": 0.08, "illegal": 0.08, "committed": 0.08}

## D1 player-r1 uses a 2m work circle; the caller owns bounds and permissions.
## center uses world x/z; sample_height(x, z) returns authoritative terrain y.
static func build(center: Vector2, radius: float, kind: String, sample_height: Callable) -> MeshInstance3D:
	if not COLORS.has(kind) or not sample_height.is_valid() or sample_height.get_argument_count() != 2:
		return null
	var width := float(WIDTHS[kind])
	var spread := 0.09 if kind == "committed" else 0.0
	if not center.is_finite() or not is_finite(radius) or radius <= spread + width * 0.5:
		return null
	if not (center + Vector2.ONE * (radius + spread + width)).is_finite() or not (center - Vector2.ONE * (radius + spread + width)).is_finite():
		return null
	var arcs: Array[Vector2] = []
	if kind == "illegal":
		var distance := 0.0
		while distance < TAU * radius:
			arcs.append(Vector2(distance / radius, minf(distance + 0.35, TAU * radius) / radius))
			distance += 0.60
	else:
		arcs.append(Vector2(0.0, TAU))
	var rings := [radius - spread, radius + spread] if kind == "committed" else [radius]
	var points: Array[Vector2] = []
	for ring in rings:
		for arc in arcs:
			var steps := maxi(1, ceili((arc.y - arc.x) * (ring + width * 0.5) / STEP))
			for step in range(steps):
				var start := Vector2.from_angle(lerpf(arc.x, arc.y, float(step) / steps))
				var end := Vector2.from_angle(lerpf(arc.x, arc.y, float(step + 1) / steps))
				var a: Vector2 = center + start * (ring - width * 0.5)
				var b: Vector2 = center + start * (ring + width * 0.5)
				var c: Vector2 = center + end * (ring + width * 0.5)
				var d: Vector2 = center + end * (ring - width * 0.5)
				points.append_array([a, b, c, a, c, d])
	if kind == "illegal":
		var half := minf(radius * 0.375, 0.75)
		for sign in [-1.0, 1.0]:
			var start := center - Vector2(half, half * sign)
			var end := center + Vector2(half, half * sign)
			var offset := Vector2(-(end - start).y, (end - start).x).normalized() * width * 0.5
			var steps := maxi(1, ceili(start.distance_to(end) / STEP))
			for step in range(steps):
				var a := start.lerp(end, float(step) / steps)
				var b := start.lerp(end, float(step + 1) / steps)
				points.append_array([a - offset, a + offset, b + offset, a - offset, b + offset, b - offset])
	var vertices := PackedVector3Array()
	for point in points:
		var height: Variant = sample_height.call(point.x, point.y)
		if not (height is float or height is int) or not is_finite(float(height)):
			return null
		vertices.append(Vector3(point.x, float(height) + LIFT, point.y))
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	material.albedo_color = COLORS[kind]
	var marker := MeshInstance3D.new()
	marker.mesh = mesh
	marker.material_override = material
	marker.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	marker.name = "Range_" + kind
	return marker
