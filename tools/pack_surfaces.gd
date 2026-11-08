extends SceneTree

# Packs photoscans as the surface textures the model kit tints: a grey grain PNG holding each pixel's brightness around
# the set's mean, at DETAIL strength and halved so a tint of twice the colour restores it, and the OpenGL normal PNG
# with its slope scaled to DEPTH.
# Arguments: <source dir> <output dir> <size> <name=ambientCG id>...

# How much of the photoscan's brightness variation the grain keeps; the ground keeps the same share.
const DETAIL := 0.4
# Cloth scans are nearly even at the game's distance, so the weave keeps all of its contrast to show at all.
const FULL_DETAIL := ["cloth"]
# Luminance weights, the same as the ground shader's LUMA.
const LUMA := Vector3(0.3, 0.59, 0.11)
# How deep the bump reads, the same as the ground's; a material's detail layer has no depth setting of its own.
const DEPTH := 0.5


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
	print("SURFACE-PACK PASS")
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
	var normal := _load(prefix + "NormalGL.png", size)
	if albedo == null or normal == null:
		return false
	var mean := 0.0
	for y in size:
		for x in size:
			mean += _luminance(albedo.get_pixel(x, y))
	mean /= size * size
	var detail := 1.0 if name in FULL_DETAIL else DETAIL
	var grain := Image.create(size, size, false, Image.FORMAT_RGBA8)
	for y in size:
		for x in size:
			var factor := lerpf(1.0, _luminance(albedo.get_pixel(x, y)) / mean, detail) / 2.0
			var grey := Color(factor, factor, factor).linear_to_srgb().clamp()
			grain.set_pixel(x, y, grey)
	grain.save_png("%s/%s_grain.png" % [output, name])
	for y in size:
		for x in size:
			var bump := normal.get_pixel(x, y)
			var slope := (Vector2(bump.r, bump.g) * 2.0 - Vector2.ONE) * DEPTH
			normal.set_pixel(x, y, Color(slope.x * 0.5 + 0.5, slope.y * 0.5 + 0.5, sqrt(maxf(0.0, 1.0 - slope.length_squared()))))
	normal.save_png("%s/%s_bump.png" % [output, name])
	print("Packed %s from %s, mean brightness %.3f" % [name, id, mean])
	return true
