extends SceneTree

# Packs texture sets the way Terrain3D reads them: albedo RGB with height in alpha, OpenGL normal RGB with roughness
# in alpha, every albedo scaled to the same mean linear brightness, so the terrain shader divides any blend by MEAN.
# Arguments: <source dir> <output dir> <size> <name=ambientCG id>...

# The mean the terrain shader's fragment include divides the grain by.
const MEAN := 0.14
# Luminance weights, the same as the include's LUMA.
const LUMA := Vector3(0.3, 0.59, 0.11)


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
		if parts.size() != 2:
			push_error("Expected <name=ambientCG id>, got " + spec)
			quit(1)
			return
		if not _pack_set(source, output, size, parts[0], parts[1]):
			quit(1)
			return
	print("GROUND-PACK PASS")
	quit(0)


func _load(path: String, size: int) -> Image:
	var image := Image.load_from_file(path)
	if image == null:
		push_error("Cannot load " + path)
		return null
	image.convert(Image.FORMAT_RGBA8)
	image.resize(size, size, Image.INTERPOLATE_LANCZOS)
	return image


func _luminance(color: Color) -> float:
	var linear := color.srgb_to_linear()
	return LUMA.x * linear.r + LUMA.y * linear.g + LUMA.z * linear.b


# Packs one set and returns whether every map loaded.
func _pack_set(source: String, output: String, size: int, name: String, id: String) -> bool:
	var prefix := "%s/%s_1K-PNG_" % [source, id]
	var albedo := _load(prefix + "Color.png", size)
	var height := _load(prefix + "Displacement.png", size)
	var normal := _load(prefix + "NormalGL.png", size)
	var roughness := _load(prefix + "Roughness.png", size)
	if albedo == null or height == null or normal == null or roughness == null:
		return false
	var mean := 0.0
	for y in size:
		for x in size:
			mean += _luminance(albedo.get_pixel(x, y))
	var scale := MEAN / (mean / (size * size))
	for y in size:
		for x in size:
			var linear := albedo.get_pixel(x, y).srgb_to_linear() * scale
			var color := linear.clamp().linear_to_srgb()
			color.a = height.get_pixel(x, y).r
			albedo.set_pixel(x, y, color)
			var bump := normal.get_pixel(x, y)
			bump.a = roughness.get_pixel(x, y).r
			normal.set_pixel(x, y, bump)
	albedo.save_png("%s/%s_albedo.png" % [output, name])
	normal.save_png("%s/%s_normal.png" % [output, name])
	print("Packed %s from %s, brightness scaled by %.2f" % [name, id, scale])
	return true
