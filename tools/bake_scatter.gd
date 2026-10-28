extends SceneTree

const SEED := 20261028
const AMOUNT := 40000
const WORLD_SIZE := 216.0
const WORLD_CENTER := WORLD_SIZE * 0.5
const POSITION_MODIFIER := preload("res://addons/proton_scatter/src/modifiers/create_inside_random.gd")
const ROTATION_MODIFIER := preload("res://addons/proton_scatter/src/modifiers/randomize_rotation.gd")
const SCALE_MODIFIER := preload("res://addons/proton_scatter/src/modifiers/randomize_transforms.gd")
const STACK_SCRIPT := preload("res://addons/proton_scatter/src/stack/modifier_stack.gd")
const SCATTER_SCRIPT := preload("res://addons/proton_scatter/src/scatter.gd")
const SHAPE_NODE_SCRIPT := preload("res://addons/proton_scatter/src/scatter_shape.gd")
const BOX_SHAPE_SCRIPT := preload("res://addons/proton_scatter/src/shapes/box_shape.gd")


func _initialize() -> void:
	call_deferred("_bake")


func _bake() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 1:
		push_error("Expected the absolute placements.json output path")
		quit(1)
		return

	var scatter: Node3D = SCATTER_SCRIPT.new()
	scatter.name = "ProtonScatter"
	scatter.global_seed = SEED
	scatter.dbg_disable_thread = true
	root.add_child(scatter)

	var shape_node: Node3D = SHAPE_NODE_SCRIPT.new()
	shape_node.name = "WorldBounds"
	shape_node.position = Vector3(WORLD_CENTER, 0.5, WORLD_CENTER)
	var box = BOX_SHAPE_SCRIPT.new()
	box.size = Vector3(WORLD_SIZE, 1.0, WORLD_SIZE)
	shape_node.shape = box
	scatter.add_child(shape_node)

	var stack = STACK_SCRIPT.new()
	var create = POSITION_MODIFIER.new()
	create.amount = AMOUNT
	create.restrict_height = true
	stack.add(create)
	var rotation = ROTATION_MODIFIER.new()
	rotation.rotation = Vector3(0.0, 360.0, 0.0)
	stack.add(rotation)
	var scale = SCALE_MODIFIER.new()
	scale.rotation = Vector3.ZERO
	scale.scale = Vector3(0.22, 0.22, 0.22)
	stack.add(scale)

	# Run the real upstream modifier stack in sequence on the main thread. The
	# generator's async API is awaited so no worker thread or render nodes exist.
	scatter.domain.discover_shapes(scatter)
	var transforms = await stack.start_update(scatter, scatter.domain)
	if transforms.size() != AMOUNT:
		push_error("ProtonScatter returned %d of %d requested transforms" % [transforms.size(), AMOUNT])
		quit(1)
		return

	var points: Array = []
	points.resize(transforms.size())
	for i in transforms.size():
		var transform: Transform3D = transforms.list[i]
		var scale_value := transform.basis.get_scale().x
		var yaw := fposmod(transform.basis.get_euler().y, TAU)
		points[i] = [
			snappedf(transform.origin.x, 0.0001),
			snappedf(transform.origin.z, 0.0001),
			snappedf(yaw, 0.000001),
			snappedf(scale_value, 0.000001),
		]
	var payload := {
		"source": "HungryProton/scatter@7198cc00078ebde61307578137c3f35223c6e438",
		"seed": SEED,
		"points": points,
	}
	var file := FileAccess.open(args[0], FileAccess.WRITE)
	if file == null:
		push_error("Cannot write placements output: " + args[0])
		quit(1)
		return
	file.store_string(JSON.stringify(payload) + "\n")
	file.close()
	print("SCATTER-BAKE PASS: %d placements, seed %d" % [points.size(), SEED])
	quit()
