extends SceneTree
func _initialize():call_deferred("run")
func run():
    var host=load("res://independent/host.gd").new();root.add_child(host);host._build_scene_scaffold({})
    var loaded=host._load_manifest("res://scenes/art_preview_r1/probe_manifest_r1.json")
    if not loaded.ok:printerr(loaded.error);quit(1);return
    var d:Dictionary=loaded.data
    var inst=host._instantiate_sample(d)
    if not inst.ok:printerr(inst.error);quit(1);return
    var w=inst.wrapper
    var bind=host._bind_materials(w,d,{})
    if bind!="":printerr(bind);quit(1);return
    var lo=Vector3(INF,INF,INF);var hi=Vector3(-INF,-INF,-INF)
    var failures=[];var cases=0
    for phase in d.phases:
        for phase_t in ([0.0,.5,1.0] if phase=="deploying" else [0.0]):
            for state in d.states:
                for cargo in d.cargo_modes:
                    for time in [0.0,.25,.5,.75,1.0,1.5,2.0,2.25]:
                        var p={"state":state,"phase":phase,"phase_t":phase_t,"cargo":cargo,"reason":"none","time_s":time}
                        var applied=host._apply_preset(w,p,d)
                        if applied.error!="":printerr(applied.error);quit(1);return
                        var ext=host._visible_vertex_extents(w)
                        lo=lo.min(ext.min);hi=hi.max(ext.max)
                        var err=host._check_active_containment(w,d)
                        if err!="":failures.append({"preset":p,"error":err})
                        if not w.transform.is_equal_approx(Transform3D.IDENTITY):printerr("root moved");quit(1);return
                        cases+=1
    var report={"cases":cases,"active_measured":{"min":[lo.x,lo.y,lo.z],"max":[hi.x,hi.y,hi.z]},"failed":failures.size(),"first_failures":failures.slice(0,8)}
    var f=FileAccess.open("/private/tmp/yudian-preview-b-independent-contract-20261004/matrix.json",FileAccess.WRITE)
    f.store_string(JSON.stringify(report,"  "));f.close()
    print("PROBE_MATRIX cases=",cases," failed=",failures.size()," measured min=",lo," max=",hi)
    quit(0 if failures.is_empty() else 1)
