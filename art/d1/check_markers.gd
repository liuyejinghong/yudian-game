extends SceneTree

var checks := 0
var failures := 0
var calls := 0

func _initialize() -> void:
	var path: String = get_script().resource_path.get_base_dir()
	var markers: Script = load(path.path_join("markers.gd"))
	if markers == null:
		printerr("A03_CHECK_FAIL: cannot load markers.gd")
		quit(1)
		return
	var center := Vector2(-2.0, 1.5)
	var radius := 2.0
	for mode in range(3):
		for kind in ["selected", "hover", "legal", "illegal", "committed"]:
			calls = 0
			var sample := func(x: float, z: float) -> float:
				calls += 1
				return height(x, z, mode)
			var marker: MeshInstance3D = markers.build(center, radius, kind, sample)
			check(marker != null, "build " + kind)
			if marker == null:
				continue
			var vertices: PackedVector3Array = marker.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
			check(calls == vertices.size(), "sample every emitted vertex")
			var heights_match := true
			for v in vertices:
				heights_match = heights_match and absf(v.y - height(v.x, v.z, mode) - 0.025) < 0.00001
			check(heights_match, "authoritative height + lift " + kind)
			var width := float(markers.WIDTHS[kind])
			var widths_match := true
			var steps_match := true
			var radii_match := true
			var inner_arc := 0.0
			var outer_arc := 0.0
			var cross_seen := false
			var gap_empty := true
			var coverage := [false, false, false, false, false, false, false, false]
			for i in range(0, vertices.size(), 6):
				var a := Vector2(vertices[i].x, vertices[i].z)
				var b := Vector2(vertices[i + 1].x, vertices[i + 1].z)
				var c := Vector2(vertices[i + 2].x, vertices[i + 2].z)
				widths_match = widths_match and absf(a.distance_to(b) - width) < 0.00001
				steps_match = steps_match and b.distance_to(c) <= 0.25001
				var ring := ((a + b) * 0.5).distance_to(center)
				if ring < radius * 0.75:
					cross_seen = true
					continue
				var start_angle := fposmod((b - center).angle(), TAU)
				var arc := fposmod((c - center).angle() - (b - center).angle(), TAU)
				coverage[int(fposmod(start_angle + arc * 0.5, TAU) / TAU * 8.0)] = true
				if kind == "committed":
					radii_match = radii_match and absf(absf(ring - radius) - 0.09) < 0.00001
					if ring < radius:
						inner_arc += arc
					else:
						outer_arc += arc
				else:
					radii_match = radii_match and absf(ring - radius) < 0.00001
					outer_arc += arc
				if kind == "illegal":
					var gap_angle := 0.475 / radius
					gap_empty = gap_empty and not (start_angle < gap_angle and start_angle + arc > gap_angle)
			check(widths_match and width <= 0.12, "line width " + kind)
			check(steps_match, "every segment at most 0.25m " + kind)
			check(radii_match, "matches circular work radius " + kind)
			check(not coverage.has(false), "full circular coverage " + kind)
			if kind == "committed":
				check(absf(inner_arc - TAU) < 0.0001 and absf(outer_arc - TAU) < 0.0001, "two complete circular edges")
				check(absf(width - 0.08) < 0.00001 and absf(0.18 - width - 0.10) < 0.00001, "committed 0.08m lines and 0.10m clear gap")
			elif kind == "illegal":
				check(cross_seen and gap_empty and outer_arc < TAU * 0.8 and outer_arc > TAU * 0.5, "illegal center cross and actual dash gaps")
			else:
				check(absf(outer_arc - TAU) < 0.0001 and not cross_seen, "single complete circle")
			marker.free()
	check(markers.WIDTHS["selected"] != markers.WIDTHS["hover"], "selection and hover differ in width")
	var flat := func(_x: float, _z: float) -> float: return 2.0
	for bad in [0.0, -1.0, INF, NAN, 0.001]:
		check(markers.build(center, bad, "legal", flat) == null, "reject invalid radius")
	for bad in [Vector2(INF, 0), Vector2(0, NAN)]:
		check(markers.build(bad, radius, "legal", flat) == null, "reject invalid center")
	check(markers.build(center, radius, "unknown", flat) == null, "reject unknown kind")
	check(markers.build(center, radius, "legal", Callable()) == null, "reject missing sampler")
	var wrong_args := func(_x: float) -> float: return 2.0
	check(markers.build(center, radius, "legal", wrong_args) == null, "reject wrong sampler signature")
	var bad_height := func(_x: float, _z: float) -> float: return NAN
	check(markers.build(center, radius, "legal", bad_height) == null, "reject non-finite sampled height")
	var bad_type := func(_x: float, _z: float) -> String: return "bad"
	check(markers.build(center, radius, "legal", bad_type) == null, "reject non-numeric sampled height")
	check(load(path.path_join("surface.gdshader")) is Shader, "shader resource loads")
	var palette: Variant = JSON.parse_string(FileAccess.get_file_as_string(path.path_join("palette.json")))
	check(palette is Dictionary and palette.status == "candidate", "palette candidate parses")
	print("A03_MARKERS_%s %d/%d" % ["PASS" if failures == 0 else "FAIL", checks - failures, checks])
	quit(0 if failures == 0 else 1)

func height(x: float, z: float, mode: int) -> float:
	if mode == 1:
		return x * 0.4 + z * 0.2
	if mode == 2:
		return sin(x * 2.3) * 0.7 + cos(z * 1.7) * 0.3
	return 2.0

func check(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("A03_CHECK_FAIL: " + label)
