extends SceneTree

# Packs texture sets the way Terrain3D reads them: albedo RGB with height in alpha, OpenGL normal RGB with roughness in alpha.
# Arguments: <source dir> <output dir> <size> <name=ambientCG id>...


func _initialize() -> void:
	call_deferred("_pack")


func _pack() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 4:
		push_error("Expected: <source dir> <output dir> <size> <name=ambientCG id>...")
		quit(1)
		return
	var source := args[0]
	var output := args[1]
	var size := int(args[2])
	for spec in args.slice(3):
		var parts := spec.split("=")
		_pack_set(source, output, size, parts[0], parts[1])
	print("GROUND-PACK PASS")
	quit(0)


func _load(path: String, size: int) -> Image:
	var image := Image.load_from_file(path)
	if image == null:
		push_error("Cannot load " + path)
		quit(1)
		return null
	image.convert(Image.FORMAT_RGBA8)
	image.resize(size, size, Image.INTERPOLATE_LANCZOS)
	return image


func _pack_set(source: String, output: String, size: int, name: String, id: String) -> void:
	var prefix := "%s/%s_1K-PNG_" % [source, id]
	var albedo := _load(prefix + "Color.png", size)
	var height := _load(prefix + "Displacement.png", size)
	var normal := _load(prefix + "NormalGL.png", size)
	var roughness := _load(prefix + "Roughness.png", size)
	for y in size:
		for x in size:
			var color := albedo.get_pixel(x, y)
			color.a = height.get_pixel(x, y).r
			albedo.set_pixel(x, y, color)
			var bump := normal.get_pixel(x, y)
			bump.a = roughness.get_pixel(x, y).r
			normal.set_pixel(x, y, bump)
	albedo.save_png("%s/%s_albedo.png" % [output, name])
	normal.save_png("%s/%s_normal.png" % [output, name])
	print("Packed %s from %s" % [name, id])
