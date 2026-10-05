extends Node3D
# TEST ONLY: fixed accepted GEO static proportion check, no state delivery.
func apply_preview(preset:Dictionary)->String:
    if preset.get("state") != "idle" or preset.get("phase") != "completed" or preset.get("phase_t") != 0.0 or preset.get("time_s") != 0.0 or preset.get("reason") != "none":
        return "Static proportion check only; state production is NOT_RUN here"
    if preset.get("cargo") not in ["empty","loaded"]:
        return "invalid cargo"
    get_node("Model/tuoyun-r1/Model/TowRod").visible = false
    get_node("Model/tuoyun-r1/Model/CargoBox").visible = preset["cargo"] == "loaded"
    return ""
