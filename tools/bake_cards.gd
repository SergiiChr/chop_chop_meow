extends SceneTree

# Bakes a glTF model drawn with one cut-out texture, such as a plant whose leaves are cards, into one mesh for each of
# its parts and the texture they share.
# Every mesh keeps its points, smooth normals and places on the texture, and stands on its own origin as the part does
# in its own node; the texture keeps its see-through parts.
# Arguments: <output dir> <model.glb> <texture name> <texture size>


func _initialize() -> void:
	call_deferred("_bake")


func _bake() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 4:
		push_error("Expected: <output dir> <model.glb> <texture name> <texture size>")
		quit(1)
		return
	var document := GLTFDocument.new()
	var state := GLTFState.new()
	if document.append_from_file(args[1], state) != OK:
		push_error("Cannot read " + args[1])
		quit(1)
		return
	var scene := document.generate_scene(state)
	var texture: Image = null
	var parts := 0
	for node in scene.find_children("*", "MeshInstance3D", true, false):
		var mesh: Mesh = node.mesh
		var material := mesh.surface_get_material(0) as BaseMaterial3D
		if texture == null and material and material.albedo_texture:
			texture = material.albedo_texture.get_image()
		var arrays := mesh.surface_get_arrays(0)
		var baked := ArrayMesh.new()
		var kept := []
		kept.resize(Mesh.ARRAY_MAX)
		for channel in [Mesh.ARRAY_VERTEX, Mesh.ARRAY_NORMAL, Mesh.ARRAY_TEX_UV, Mesh.ARRAY_INDEX]:
			kept[channel] = arrays[channel]
		baked.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, kept)
		var name := String(node.name).to_lower().trim_suffix("_lod0")
		if ResourceSaver.save(baked, args[0].path_join(name + ".res")) != OK:
			push_error("Cannot save " + name)
			quit(1)
			return
		var box := baked.get_aabb()
		print("%s.res: %d triangles, %.3f m tall" % [name, (kept[Mesh.ARRAY_INDEX] as PackedInt32Array).size() / 3, box.end.y])
		parts += 1
	scene.free()
	if texture == null or parts == 0:
		push_error("No textured meshes in " + args[1])
		quit(1)
		return
	if texture.is_compressed():
		texture.decompress()
	texture.convert(Image.FORMAT_RGBA8)
	var size := int(args[3])
	texture.resize(size, size, Image.INTERPOLATE_LANCZOS)
	if texture.save_png(args[0].path_join(args[2])) != OK:
		push_error("Cannot save " + args[2])
		quit(1)
		return
	print("%s: %d px" % [args[2], size])
	print("CARD-BAKE PASS")
	quit(0)
