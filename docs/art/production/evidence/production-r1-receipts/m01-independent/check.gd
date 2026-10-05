extends SceneTree
const EXPECTED = {
    "body_light": ["D8DCD6", .65, 0.0], "frame_dark": ["38454B", .75, .2],
    "rubber": ["24292B", .9, 0.0], "accent_warm": ["D77B3D", .6, 0.0],
    "solar_face": ["203B52", .35, .1], "soil_mars": ["98684D", .95, 0.0]
}
func _initialize():
    var valid = true
    for role in EXPECTED:
        var path = "res://materials/" + role + ".tres"
        var material = load(path)
        if not material is StandardMaterial3D:
            push_error("M01 wrong or missing resource: " + role)
            valid = false
            continue
        var target = Color(EXPECTED[role][0])
        for component in range(4):
            if abs(material.albedo_color[component] - target[component]) > 0.000001:
                push_error("M01 color mismatch: " + role)
                valid = false
        if abs(material.roughness - EXPECTED[role][1]) > 0.000001 or abs(material.metallic - EXPECTED[role][2]) > 0.000001:
            push_error("M01 PBR mismatch: " + role)
            valid = false
        if material.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED or material.emission_enabled:
            push_error("M01 alpha/emission mismatch: " + role)
            valid = false
        for item in material.get_property_list():
            if item.type == TYPE_OBJECT and str(item.name).ends_with("_texture") and material.get(item.name) != null:
                push_error("M01 unexpected texture: " + role + "/" + str(item.name))
                valid = false
        print("M01_PARAM ", role, " color=", material.albedo_color.to_html(), " roughness=", material.roughness, " metallic=", material.metallic)
    var missing = ResourceLoader.load("res://materials/not_a_role.tres")
    if missing != null:
        push_error("M01 missing resource unexpectedly loaded")
        valid = false
    print("M01_MISSING_PATH_REJECTED=", missing == null)
    if valid:
        print("M01_INDEPENDENT_OK: six resources and missing path")
    quit(0 if valid else 1)
