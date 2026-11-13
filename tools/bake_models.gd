extends SceneTree

# Bakes glTF models whose colours come from a palette texture into meshes that carry those colours on their corners,
# so the game dresses them with its own materials and loads no texture for them.
# Every triangle keeps its own three corners, which leaves the faces flat like the game's code-built models.
# Arguments: <output dir> <model.glb>...


func _initialize() -> void:
	call_deferred("_bake")


func _bake() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 2:
		push_error("Expected: <output dir> <model.glb>...")
		quit(1)
		return
	for path in args.slice(1):
		if not _bake_model(path, args[0]):
			quit(1)
			return
	print("MODEL-BAKE PASS")
	quit(0)


func _bake_model(path: String, output: String) -> bool:
	var document := GLTFDocument.new()
	var state := GLTFState.new()
	if document.append_from_file(path, state) != OK:
		push_error("Cannot read " + path)
		return false
	var scene := document.generate_scene(state)
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	var colours := {}
	var triangles := _add(scene, Transform3D.IDENTITY, tool, colours)
	scene.free()
	if triangles == 0:
		push_error("No textured triangles in " + path)
		return false
	var target := output.path_join(path.get_file().get_basename().replace("-", "_") + ".res")
	if ResourceSaver.save(tool.commit(), target) != OK:
		push_error("Cannot save " + target)
		return false
	print("%s: %d triangles, colours %s" % [target.get_file(), triangles, " ".join(colours.keys())])
	return true


# Adds every mesh under the node, placed as the scene places it, and returns the number of triangles added.
func _add(node: Node, parent: Transform3D, tool: SurfaceTool, colours: Dictionary) -> int:
	var placed := parent * (node as Node3D).transform if node is Node3D else parent
	var triangles := 0
	if node is MeshInstance3D:
		var mesh: Mesh = node.mesh
		for surface in range(mesh.get_surface_count()):
			var material := mesh.surface_get_material(surface) as BaseMaterial3D
			var palette := material.albedo_texture.get_image() if material and material.albedo_texture else null
			if palette == null:
				continue
			if palette.is_compressed():
				palette.decompress()
			var arrays := mesh.surface_get_arrays(surface)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var uvs: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			for start in range(0, indices.size(), 3):
				var corners: Array[Vector3] = []
				for k in range(3):
					corners.append(placed * vertices[indices[start + k]])
				# One colour a face, read at its middle, so a face whose corners touch two palette cells stays whole.
				var uv := (uvs[indices[start]] + uvs[indices[start + 1]] + uvs[indices[start + 2]]) / 3
				var pixel := Vector2i(
					clampi(int(uv.x * palette.get_width()), 0, palette.get_width() - 1),
					clampi(int(uv.y * palette.get_height()), 0, palette.get_height() - 1)
				)
				var colour := palette.get_pixelv(pixel)
				colours[colour.to_html(false)] = true
				var normal := (corners[2] - corners[0]).cross(corners[1] - corners[0]).normalized()
				for corner in corners:
					tool.set_color(colour)
					tool.set_normal(normal)
					tool.add_vertex(corner)
				triangles += 1
	for child in node.get_children():
		triangles += _add(child, placed, tool, colours)
	return triangles
