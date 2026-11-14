extends SceneTree

# Packs ambientCG photoscans into the game's textures, one of two ways.
# ground: the way Terrain3D reads them, albedo RGB with height in alpha and OpenGL normal RGB with roughness in alpha,
# every albedo scaled to the same mean linear brightness, so the terrain shader divides any blend by MEAN.
# surface: the way the model kit tints them, a grain EXR holding each pixel's brightness around the set's mean as a
# multiplier, at the set's detail share, and the OpenGL normal PNG.
# A surface set whose detail is "colour" keeps each pixel's colour around the set's mean colour instead, which it
# prints: a model tinted that colour shows the photoscan as it is.
# Arguments: <ground|surface> <source dir> <output dir> <size> <name=ambientCG id[:detail|:colour]>...

# The mean the terrain shader's fragment include divides the grain by.
const MEAN := 0.14
# Luminance weights, the same as the terrain shader's LUMA.
const LUMA := Vector3(0.3, 0.59, 0.11)
# How much of the photoscan's brightness variation a surface's grain keeps unless its set says otherwise; the ground
# keeps the same share.
const DETAIL := 0.4


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
		var set := parts[1].split(":") if parts.size() == 2 else PackedStringArray()
		if set.size() == 0 or set.size() > 2:
			push_error("Expected <name=ambientCG id[:detail]>, got " + spec)
			quit(1)
			return
		var colour := set.size() == 2 and set[1] == "colour"
		var detail := float(set[1]) if set.size() == 2 and not colour else DETAIL
		var packed := (
			_pack_ground(source, output, size, parts[0], set[0]) if ground
			else _pack_surface(source, output, size, parts[0], set[0], 1.0 if colour else detail, colour)
		)
		if not packed:
			quit(1)
			return
	print("TEXTURE-PACK PASS")
	quit(0)


# Loads one of a set's maps at the packed size.
func _load(source: String, id: String, map: String, size: int) -> Image:
	var path := "%s/%s_1K-PNG_%s.png" % [source, id, map]
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
	var albedo := _load(source, id, "Color", size)
	var height := _load(source, id, "Displacement", size)
	var normal := _load(source, id, "NormalGL", size)
	var roughness := _load(source, id, "Roughness", size)
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


# The set's mean linear colour.
func _mean_colour(albedo: Image, size: int) -> Color:
	var mean := Color(0, 0, 0)
	for y in size:
		for x in size:
			mean += albedo.get_pixel(x, y).srgb_to_linear()
	return mean / (size * size)


# Packs one surface set and returns whether every map loaded.
func _pack_surface(
	source: String, output: String, size: int, name: String, id: String, detail: float, colour: bool
) -> bool:
	var albedo := _load(source, id, "Color", size)
	var normal := _load(source, id, "NormalGL", size)
	if albedo == null or normal == null:
		return false
	var mean := _mean_luminance(albedo, size)
	var mean_colour := _mean_colour(albedo, size)
	var grain := Image.create(size, size, false, Image.FORMAT_RGBH if colour else Image.FORMAT_RH)
	for y in size:
		for x in size:
			var pixel := albedo.get_pixel(x, y)
			if colour:
				var linear := pixel.srgb_to_linear()
				grain.set_pixel(x, y, Color(linear.r / mean_colour.r, linear.g / mean_colour.g, linear.b / mean_colour.b))
			else:
				var factor := lerpf(1.0, _luminance(pixel) / mean, detail)
				grain.set_pixel(x, y, Color(factor, factor, factor))
	grain.save_exr("%s/%s_grain.exr" % [output, name], not colour)
	normal.save_png("%s/%s_bump.png" % [output, name])
	if colour:
		print("Packed %s from %s, mean colour #%s" % [name, id, mean_colour.linear_to_srgb().to_html(false)])
	else:
		print("Packed %s from %s, mean brightness %.3f" % [name, id, mean])
	return true
