extends SceneTree
func _initialize():
    var names = ["shadow_filter", "camera_attributes", "editable", "disabled", "shadow_filter_quality"]
    var classes = ["DirectionalLight3D", "Environment", "WorldEnvironment", "Camera3D", "LineEdit", "HSlider"]
    var report = {}
    for cls in classes:
        var found = []
        for p in ClassDB.class_get_property_list(cls):
            if p.name in names:
                found.append(p.name)
        report[cls] = found
    for cls in ["RenderingServer", "Engine"]:
        var found = []
        for m in ClassDB.class_get_method_list(cls):
            if "rendering_method" in m.name or "rendering_driver" in m.name or "shadow_filter" in m.name:
                found.append(m.name)
        report[cls] = found
    report["shadow_filter_project_default"] = ProjectSettings.get_setting("rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality", "NOT_FOUND")
    print(JSON.stringify(report))
    print("PREVIEW_B_API_PROBE_OK")
    quit(0)
