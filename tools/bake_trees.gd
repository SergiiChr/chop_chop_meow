extends SceneTree

# Run only in the isolated Tree3D authoring project created by tools/tree3d.py.
# Baked ArrayMeshes are portable; the game does not load a native extension.
func _initialize() -> void:
    var args := OS.get_cmdline_user_args()
    if args.size() != 1:
        push_error("Expected the absolute output folder")
        quit(1)
        return
    var output: String = args[0]
    DirAccess.make_dir_recursive_absolute(output)
    var report: Array = []
    for species in ["oak", "spruce", "birch", "bush"]:
        for variant in range(3):
            var tree: Node3D = ClassDB.instantiate("Tree3D")
            tree.set("trunk_segments", 6)
            tree.set("trunk_branches_count", 5)
            tree.set("trunk_height", 4)
            tree.set("trunk_length", 2.1)
            tree.set("trunk_branch_length", 0.75)
            tree.set("trunk_branch_length_falloff", 0.85)
            tree.set("trunk_max_radius", 0.22)
            tree.set("trunk_climb_rate", 0.45)
            tree.set("twig_scale", 0.9)
            if species == "spruce":
                tree.set("trunk_height", 12)
                tree.set("trunk_length", 0.65)
                tree.set("trunk_branch_length", 1.5)
                tree.set("trunk_branch_length_falloff", 0.82)
                tree.set("trunk_climb_rate", 0.4)
                tree.set("trunk_drop_amount", -0.15)
                tree.set("trunk_branch_clump_min", 0.1)
                tree.set("trunk_branch_clump_max", 0.25)
                tree.set("trunk_grow_amount", -0.08)
                tree.set("twig_scale", 0.8)
            elif species == "birch":
                tree.set("trunk_height", 7)
                tree.set("trunk_length", 2.4)
                tree.set("trunk_branch_length", 0.65)
                tree.set("trunk_max_radius", 0.12)
                tree.set("trunk_climb_rate", 0.55)
                tree.set("trunk_drop_amount", 0.08)
                tree.set("twig_scale", 0.65)
            elif species == "bush":
                tree.set("trunk_branches_count", 5)
                tree.set("trunk_height", 2)
                tree.set("trunk_length", 0.12)
                tree.set("trunk_branch_length", 0.45)
                tree.set("trunk_max_radius", 0.055)
                tree.set("trunk_climb_rate", 0.14)
                tree.set("trunk_grow_amount", 0.05)
                tree.set("trunk_drop_amount", -0.08)
                tree.set("twig_scale", 0.65)
            tree.set("seed", 117 + variant * 137 + ["oak", "spruce", "birch", "bush"].find(species) * 41)
            var parts: Array[MeshInstance3D] = []
            var bounds := AABB()
            for child in tree.get_children(true):
                if child is MeshInstance3D:
                    parts.append(child)
                    bounds = child.mesh.get_aabb() if parts.size() == 1 else bounds.merge(child.mesh.get_aabb())
            if parts.size() != 2 or bounds.size.y <= 0:
                push_error("Tree3D did not produce trunk and foliage meshes")
                quit(1)
                return
            # Keep the silhouettes inside gameplay's established canopy footprint.
            var width: float = 3.5 if species == "oak" else (2.7 if species == "spruce" else 2.0)
            var height: float = 5.3 if species == "spruce" else 4.9
            if species == "bush":
                width = 1.2
                height = 0.9
            var scale := Vector3(width / maxf(bounds.size.x, bounds.size.z), height / bounds.size.y, width / maxf(bounds.size.x, bounds.size.z))
            var center := Vector3(bounds.get_center().x, bounds.position.y, bounds.get_center().z)
            for part in range(2):
                var mesh := ArrayMesh.new()
                var source: Mesh = parts[part].mesh
                var count := 0
                for surface in range(source.get_surface_count()):
                    var arrays: Array = source.surface_get_arrays(surface)
                    var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
                    var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
                    for i in range(vertices.size()):
                        var vertex := vertices[i] - center
                        if species == "spruce":
                            var taper := 1.0 - 0.78 * clampf(vertex.y / bounds.size.y, 0, 1)
                            # Jacobian inverse transpose preserves normals under the height-dependent taper.
                            var slope := -0.78 / bounds.size.y
                            var n := normals[i]
                            normals[i] = Vector3(n.x / taper, n.y - slope * (vertex.x * n.x + vertex.z * n.z) / taper, n.z / taper).normalized()
                            vertex.x *= taper
                            vertex.z *= taper
                        vertices[i] = vertex * scale
                        if species == "bush":
                            vertices[i].y += 0.025
                        normals[i] = (normals[i] / scale).normalized()
                    arrays[Mesh.ARRAY_VERTEX] = vertices
                    arrays[Mesh.ARRAY_NORMAL] = normals
                    # Tree3D uses vertex colors for wind weights, not visible color.
                    arrays[Mesh.ARRAY_COLOR] = null
                    mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
                    var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
                    count += indices.size() / 3
                var kind: String = "trunk" if part == 0 else "leaves"
                var path := output.path_join("%s%d_%s.res" % [species, variant, kind])
                if ResourceSaver.save(mesh, path) != OK:
                    push_error("Cannot save " + path)
                    quit(1)
                    return
                report.append({"species": species, "variant": variant, "part": kind, "triangles": count, "size": str(mesh.get_aabb().size)})
            tree.free()
    var file := FileAccess.open(output.path_join("meshes.json"), FileAccess.WRITE)
    file.store_string(JSON.stringify(report, "  ") + "\n")
    print("TREE3D-BAKE PASS")
    quit()
