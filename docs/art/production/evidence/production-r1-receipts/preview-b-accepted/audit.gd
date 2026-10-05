extends SceneTree
var host: Node3D
var failed := 0
var data: Dictionary
func _initialize():
    call_deferred("run")
func expect(name:String, condition:bool, detail:Variant=""):
    print("INDEPENDENT ",name," ","PASS" if condition else "FAIL"," ",detail)
    if not condition:failed+=1
func fresh(d:Dictionary)->Variant:
    return host._instantiate_sample(d)
func preset(phase="completed",state="idle",time=0.0)->Dictionary:
    return {"state":state,"phase":phase,"phase_t":0.0,"cargo":"empty","reason":"none","time_s":time}
func run():
    host=load("res://independent/host.gd").new();root.add_child(host)
    host._build_scene_scaffold({})
    var loaded:Dictionary=host._load_manifest("res://scenes/art_preview_r1/probe_manifest_r1.json")
    expect("baseline_manifest",loaded.ok,loaded.error)
    if not loaded.ok:quit(1);return
    data=loaded.data
    var d=data.duplicate(true)
    d.preview_scene="res://assets/materials/art-r1/body_light.tres"
    var wrong:Variant=fresh(d)
    expect("wrong_resource_returns_error",wrong is Dictionary and not wrong.get("ok",true),wrong)
    var inst:Dictionary=fresh(data);expect("baseline_wrapper",inst.ok,inst.error)
    if not inst.ok:quit(1);return
    var w:Node3D=inst.wrapper
    expect("baseline_binding",host._bind_materials(w,data,{})=="")
    w.apply_preview(preset("installed","idle",.5))
    var p:Vector3=w.get_node("Model/Body").position
    w.apply_preview(preset("installed","work",.5))
    expect("declared_static_holds",w.get_node("Model/Body").position.is_equal_approx(p),w.get_node("Model/Body").position)
    var bad=preset();bad.phase_t=[]
    var response:Variant=w.call("apply_preview",bad)
    expect("wrong_preset_type_returns_error",response is String and not response.is_empty(),response)
    d=data.duplicate(true);d.preview_scene="res://independent/minimal.tscn"
    var minimal:Dictionary=fresh(d)
    expect("minimal_wrapper_load",minimal.ok,minimal.error)
    if minimal.ok:
        host._bind_materials(minimal.wrapper,d,{})
        var result:Dictionary=host._apply_preset(minimal.wrapper,preset(),d)
        expect("only_apply_contract_works",result.get("error","missing") == "",result)
    var override_mat=StandardMaterial3D.new()
    w.get_node("Model/Body").material_override=override_mat
    var cache={}
    var override_err:String=host._bind_materials(w,data,cache)
    expect("whole_material_override_rejected",override_err!="",override_err)
    print("actual effective material is whole override=",w.get_node("Model/Body").get_active_material(0)==override_mat)
    d=data.duplicate(true);d.glb="res://independent/other_model.glb";d.glb_sha256=host._sha256_file(d.glb)
    var wrong_glb:Dictionary=fresh(d)
    expect("wrapper_actual_glb_identity_rejected",not wrong_glb.ok,wrong_glb.error)
    var valid:Dictionary=fresh(data)
    if valid.ok:
        var fake=data.duplicate(true);fake.bounds.static={"min":[0.0,.1,0.0],"max":[0.0,.1,0.0]}
        expect("wrong_static_bounds_rejected",host._check_bounds(valid.wrapper,fake)!="")
    d=data.duplicate(true);d.preview_scene="res://independent/scaled.tscn"
    var scaled:Dictionary=fresh(d)
    expect("nonidentity_sample_root_rejected",not scaled.ok,scaled.error)
    print("PREVIEW_A_INDEPENDENT_CONTRACT_FAILED_COUNT=",failed)
    quit(0 if failed==0 else 1)
