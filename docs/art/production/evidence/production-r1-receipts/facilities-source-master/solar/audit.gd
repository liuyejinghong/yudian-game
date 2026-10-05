extends SceneTree
var meshes: Array = []
var sockets: Array = []
var corners: Array[Vector3] = []
var triangles := 0
func _initialize():
    call_deferred("run")
func visit(node: Node):
    if node is MeshInstance3D and node.mesh:
        var own_vertices: Array[Vector3] = []
        var surfaces: Array = []
        for i in node.mesh.get_surface_count():
            var material = node.mesh.surface_get_material(i)
            var arrays = node.mesh.surface_get_arrays(i)
            for vertex in arrays[Mesh.ARRAY_VERTEX]:
                var transformed: Vector3 = node.global_transform * vertex
                own_vertices.append(transformed)
                corners.append(transformed)
            var n = arrays[Mesh.ARRAY_INDEX].size() if arrays[Mesh.ARRAY_INDEX] != null and not arrays[Mesh.ARRAY_INDEX].is_empty() else arrays[Mesh.ARRAY_VERTEX].size()
            triangles += n / 3
            surfaces.append({"surface":i,"material":material.resource_name if material else "MISSING","vertices":arrays[Mesh.ARRAY_VERTEX].size(),"triangles":n/3})
        var own_lo: Vector3 = own_vertices[0]
        var own_hi: Vector3 = own_vertices[0]
        for vertex in own_vertices:
            own_lo = own_lo.min(vertex)
            own_hi = own_hi.max(vertex)
        meshes.append({"path":str(node.get_path()),"visible":node.is_visible_in_tree(),"scale":[node.scale.x,node.scale.y,node.scale.z],"surfaces":surfaces,"actual_vertices_bounds":{"min":[own_lo.x,own_lo.y,own_lo.z],"max":[own_hi.x,own_hi.y,own_hi.z]}})
    if str(node.name).begins_with("Socket_"):
        var p: Vector3 = node.global_position
        var f: Vector3 = -node.global_basis.z
        var u: Vector3 = node.global_basis.y
        sockets.append({"path":str(node.get_path()),"position":[p.x,p.y,p.z],"forward":[f.x,f.y,f.z],"up":[u.x,u.y,u.z]})
    for child in node.get_children():
        visit(child)
func run():
    var args=OS.get_cmdline_user_args()
    if args.size()!=2:
        printerr("AUDIT requires res GLB and output JSON");quit(1);return
    var packed=load(args[0])
    if not packed is PackedScene:
        printerr("AUDIT GLB load failure");quit(1);return
    var model=packed.instantiate()
    root.add_child(model)
    visit(model)
    if corners.is_empty():
        printerr("AUDIT no mesh");quit(1);return
    var lo=corners[0];var hi=corners[0]
    for p in corners:
        lo=lo.min(p);hi=hi.max(p)
    var report={"glb":args[0],"engine":Engine.get_version_info(),"bounds_including_hidden":{"min":[lo.x,lo.y,lo.z],"max":[hi.x,hi.y,hi.z]},"meshes":meshes,"sockets":sockets,"triangles":triangles}
    var out=FileAccess.open(args[1],FileAccess.WRITE)
    if out==null:
        printerr("AUDIT output error");quit(1);return
    out.store_string(JSON.stringify(report,"  "))
    out.close()
    print("ART_IMPORT_AUDIT_OK meshes=",meshes.size()," triangles=",triangles," sockets=",sockets.size())
    quit(0)
