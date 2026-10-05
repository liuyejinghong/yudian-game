extends SceneTree
var model: Node
var original := {}
func _initialize():
    call_deferred("run")
func v3(v: Vector3) -> Array:
    return [v.x,v.y,v.z]
func read_nodes() -> Array:
    var rows: Array = []
    var nodes: Array = [model]
    nodes.append_array(model.find_children("*","Node3D",true,false))
    for n in nodes:
        if n is Node3D:
            var b: Basis = n.transform.basis
            var row := {"path":str(model.get_path_to(n)),"local_position":v3(n.position),"local_basis":[v3(b.x),v3(b.y),v3(b.z)],"visible":n.visible}
            if str(n.name).begins_with("Socket_"):
                row["world_position"]=v3(n.global_position)
                row["forward"]=v3(-n.global_basis.z)
                row["up"]=v3(n.global_basis.y)
            rows.append(row)
    return rows
func save_original():
    var nodes: Array = [model]
    nodes.append_array(model.find_children("*","Node3D",true,false))
    for n in nodes:
        if n is Node3D:
            original[n]={"transform":n.transform,"visible":n.visible}
func reset_original():
    for n in original:
        n.transform=original[n]["transform"]
        n.visible=original[n]["visible"]
func run():
    var args := OS.get_cmdline_user_args()
    if args.size()!=2:
        printerr("CLIP_AUDIT requires GLB and output JSON");quit(1);return
    var packed: Variant=load(args[0])
    if not packed is PackedScene:
        printerr("CLIP_AUDIT load failed");quit(1);return
    model=packed.instantiate()
    root.add_child(model)
    save_original()
    var result := {"engine":Engine.get_version_info(),"glb":args[0],"source_pose":read_nodes(),"players":[]}
    var players := model.find_children("*","AnimationPlayer",true,false)
    for p in players:
        var clips: Array=[]
        for name in p.get_animation_list():
            if name=="RESET":continue
            var a: Animation=p.get_animation(name)
            var tracks: Array=[]
            for i in a.get_track_count():
                tracks.append({"path":str(a.track_get_path(i)),"type":a.track_get_type(i),"keys":a.track_get_key_count(i)})
            var poses: Array=[]
            for t in [0.0,a.length*.25,a.length*.75,a.length]:
                p.stop()
                reset_original()
                p.play(name)
                p.seek(t,true)
                p.pause()
                poses.append({"time_s":t,"nodes":read_nodes()})
            clips.append({"name":name,"length":a.length,"loop_mode":a.loop_mode,"tracks":tracks,"poses":poses})
        result["players"].append({"path":str(model.get_path_to(p)),"clips":clips})
    var out:=FileAccess.open(args[1],FileAccess.WRITE)
    if out==null:
        printerr("CLIP_AUDIT output failed");quit(1);return
    out.store_string(JSON.stringify(result,"  "));out.close()
    print("ART_CLIP_SOURCE_AUDIT_OK players=",players.size())
    quit(0)
