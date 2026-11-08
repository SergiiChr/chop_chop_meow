extends SceneTree

# Packs ambientCG photoscans into the game's textures, one of two ways.
# ground: the way Terrain3D reads them, albedo RGB with height in alpha and OpenGL normal RGB with roughness in alpha,
# every albedo scaled to the same mean linear brightness, so the terrain shader divides any blend by MEAN.
# surface: the way the model kit tints them, a grey grain PNG holding each pixel's brightness around the set's mean,
# at DETAIL strength and halved so a tint of twice the colour restores it, and the OpenGL normal PNG with its slope
# scaled to DEPTH.
# Arguments: <ground|surface> <source dir> <output dir> <size> <name=ambientCG id>...

# The mean the terrain shader's fragment include divides the grain by.
const MEAN := 0.14
# Luminance weights, the same as the terrain shader's LUMA.
const LUMA := Vector3(0.3, 0.59, 0.11)
# How much of the photoscan's brightness variation a surface's grain keeps; the ground keeps the same share.
const DETAIL := 0.4
# Cloth scans are nearly even at the game's distance, so the weave keeps all of its contrast to show at all.
const FULL_DETAIL := ["cloth"]
# How deep a surface's bump reads, the same as the ground's; a material's detail layer has no depth setting of its own.
const DEPTH := 0.5


func _initialize() -> void:
	call_deferred("_pack")


func _pack() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 5 or args[0] not in ["ground", "surface"]:
		push_error("Expected: <ground|surface> <source dir> <output dir> <size> <name=ambientCG id>...")
		quit(1)
		return
	var ground := args[0] == "ground"
	var source := args[1]
	var output := args[2]
	var size := int(args[3])
	for spec in args.slice(4):
		var parts := spec.split("=")
		if parts.size() != 2:
			push_error("Expected <name=ambientCG id>, got " + spec)
			quit(1)
			return
		var packed := _pack_ground(source, output, size, parts[0], parts[1]) if ground else _pack_surface(source, output, size, parts[0], parts[1])
		if not packed:
			quit(1)
			return
	print("TEXTURE-PACK PASS")
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


func _mean_luminance(albedo: Image, size: int) -> float:
	var mean := 0.0
	for y in size:
		for x in size:
			mean += _luminance(albedo.get_pixel(x, y))
	return mean / (size * size)


# Packs one ground set and returns whether every map loaded.
func _pack_ground(source: String, output: String, size: int, name: String, id: String) -> bool:
	var prefix := "%s/%s_1K-PNG_" % [source, id]
	var albedo := _load(prefix + "Color.png", size)
	var height := _load(prefix + "Displacement.png", size)
	var normal := _load(prefix + "NormalGL.png", size)
	var roughness := _load(prefix + "Roughness.png", size)
	if albedo == null or height == null or normal == null or roughness == null:
		return false
	var scale := MEAN / _mean_luminance(albedo, size)
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


# Packs one surface set and returns whether every map loaded.
func _pack_surface(source: String, output: String, size: int, name: String, id: String) -> bool:
	var prefix := "%s/%s_1K-PNG_" % [source, id]
	var albedo := _load(prefix + "Color.png", size)
	var normal := _load(prefix + "NormalGL.png", size)
	if albedo == null or normal == null:
		return false
	var mean := _mean_luminance(albedo, size)
	var detail := 1.0 if name in FULL_DETAIL else DETAIL
	var grain := Image.create(size, size, false, Image.FORMAT_RGBA8)
	for y in size:
		for x in size:
			var factor := lerpf(1.0, _luminance(albedo.get_pixel(x, y)) / mean, detail) / 2.0
			var grey := Color(factor, factor, factor).linear_to_srgb().clamp()
			grain.set_pixel(x, y, grey)
			var bump := normal.get_pixel(x, y)
			var slope := (Vector2(bump.r, bump.g) * 2.0 - Vector2.ONE) * DEPTH
			normal.set_pixel(x, y, Color(slope.x * 0.5 + 0.5, slope.y * 0.5 + 0.5, sqrt(maxf(0.0, 1.0 - slope.length_squared()))))
	grain.save_png("%s/%s_grain.png" % [output, name])
	normal.save_png("%s/%s_bump.png" % [output, name])
	print("Packed %s from %s, mean brightness %.3f" % [name, id, mean])
	return true
