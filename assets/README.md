# Assets

The world is rendered with code-built low-poly 3D models.
There are no world or character sprite sheets.
The only 2D images left are interface icons.

## Art direction

The art direction is natural and modern rather than fantasy.
Buildings are DIY:

- a cinder-block furnace with a steel stovepipe
- a propane-tank forge on an angle-iron stand, fed by a plywood lever bellows with a heat gauge
- an OSB cabin on block piers with a corrugated shed roof and solar panel
- a plywood storage box
- a dropped canvas backpack
- an anvil on a welded stand
- a crafting bench with its slotted chisel and hammer
- slatted wood crates
- a market post with a mailbox
- a fieldstone campfire

Trees come in three variants chosen by object id: broadleaf oak, spruce and silver birch.

## 3D models

### Changing a model

To change a model, edit its builder.
Sizes are in world units: one tile is one unit and an adult is about 1.65 units tall.

### Assembly and shared helpers

Models are assembled from code-built Godot meshes.

- `src/WorldModels.cs` — world objects, wolf and trade cart.
- `src/ActorRig.cs` — player, worker and courier.
- `src/ModelKit.cs` — shared primitive, material and fade helpers.
- `src/BottleShape.cs` — refillable tin flask, wood stopper and three-segment water gauge; all four bottle-state icons use this model.
- `src/IngotShape.cs` — shared tapered cast ingot with beveled ends, metallic finish and casting mark; used by ingot icons and pile stock.

### People and animals

- `src/HumanShape.cs` — the common neutral torso and continuous skinned limb profiles for players, workers and couriers.
  - It shares five neutral mesh resources (hips, torso, leg, left arm and right arm) across all human roles and player shirt colors.
  - Each actor keeps its own skeletons and pose.
  - Cached three-color texture palettes preserve sleeve/skin, trousers/boots and sole transitions using the existing fade-compatible StandardMaterial3D path.
- `src/ActorShape.cs` — profile, skinning and curved strap builders.
  - Outfit colors use cached palette materials rather than duplicating body geometry.
  - These meshes are cached across actors while each rig poses its own joints.
- `CourierShape` — fits a reflective hi-vis vest and curved cap to the same core, with the common face details.
- `src/WolfShape.cs` — cached continuous wolf body and muzzle profiles, shaped ears, tapered legs with hocks and paws, and a brush tail.
  It keeps the wolf and carcass animation rig.

Visible geometry counts, excluding props being carried or sat upon:

| Actor | Triangles | Mesh instances |
|---|---|---|
| Player (unchanged) | 3,084 | 19 |
| Worker (unchanged) | 2,964 | 20 |
| Courier | 2,634 (was 2,496) | 17 (was 24) |

### Buildings and stations

- `src/CabinShape.cs` — the cabin's shared sloped wall shell with door and window openings, its continuous corrugated roof, and a simple interior.
  The interior has contrasting wall lining, a wooden floor and a sleeping platform, visible through the open doorway.
- `src/FurnaceShape.cs` — shared bevelled block faces, a recessed firebox, a shaped steel coping plate and a coal pile of irregular broken slabs with broad fracture faces.
  The furnace's outdoor pile shows its selected stocked fuel and disappears when that slot is empty.
- `src/ForgeShape.cs` — the reinforced forge's continuous rounded tank shell, fitted steel bands, recessed refractory chamber and continuous teardrop bellows boards.
  Its shared meshes keep separate animated bellows pivots and heat glow.
- `src/AnvilShape.cs` — the anvil's shared cast body with a curved waist, bevelled working face and swept horn.
- `src/ChestShape.cs` — the hollow wooden chest: bevelled shell and lid, fitted corner guards, loop handles and rear hinge hardware.
- `src/BenchShape.cs` — shared workbench planks with bevelled edges, fitted aprons and lower stretchers, and a mounted vise with separate jaws.
  The slotted chisel and hammer keep their visibility handles.
- `CookShape` — caches the cooking table's recessed masonry hearth, bevelled timber joinery, grill and open stew pot, hollow mortar with fitted pestle, and fire mesh.
  `WorldModels.CookingTable` keeps the independently visible `Glow` and `Mortar` handles, the light and smoke vent, and its existing hit volumes.
- `CanneryShape` — caches the cannery's bevelled steel stove with an open firebox and flue, fitted timber counter, shaped stockpot and lid with open handles, and hand press with cans.
  - Bevels are limited to broad edges.
  - Simplified can profiles and omitted covered caps keep the lit model at 1,057 triangles across five visible mesh instances.
  - The existing fire, light and smoke controls remain on `WorldModels.Cannery`.
- `RackShape` — caches the tool station's fitted concrete feet, bevelled planks and cap, and shaped axe, pickaxe and rake.
  - Each tool keeps its separate `Racked` visibility pivot.
  - The rack keeps its existing orientation and picking volume.
  - Fully stocked it uses 504 triangles across five visible mesh instances; empty it uses 268 across two.
- `PileShape` — caches a pile-only dirt pad with fitted log edging, and stock shapes reused from existing resources.
  - Sources: the mine supply log outline, nature boulders and loose-ore nuggets, furnace coal lumps, and the bench/mine stock-board and bar primitives.
  - Connected lumps are extracted whole and logs omit redundant cylinder rings; source meshes stay unchanged.
  - Coal slots reuse the furnace's 20-triangle closed chunk with varied rotations.
    The furnace's six-chunk scatter uses 120 triangles in one mesh.
  - Families share one baked mesh across piles, fitted to the existing fourteen 9–4–1 slot positions.
    Items without a suitable source keep a color-tinted block.
  - `WorldModels.SyncPile` changes mesh and material with inventory, while `WorldView` keeps its sight guard, preserving remembered stock in fog.
  - The farm yard stays unchanged.
  - An empty pile uses 108 triangles and one mesh instance.
    Full piles use fifteen instances and vary by material: wood 780 triangles, stone 1,228, coal 388, iron ore 388.
- `FarmShape` — caches the farm's shaped soil rows and three seeded variants of sprouts, green plants and ripe wheat.
  - Tapered two-sided leaves, triangular stems and shaped grain heads replace separate boxes.
  - One crop mesh is shown per growth stage under the existing `Crops` handles.
  - Farms reuse the approved `PileShape.Yard` mesh without changing pile contents.
  - The five looks keep the same gameplay mapping; ripe fields use 1,218 triangles across three visible mesh instances.
- `MineShape` — caches the mine's plan outline, inward-facing tunnel shell, spoil pad, fitted portal and sloped board hood, inner supports and rail runs.
  - The cart has a continuous flared tub, inner lining and rim, and axles and wheels seated on the rail tops.
    Its load reuses loose-ore geometry.
  - All construction pivots, cart/load visibility, lantern fire/light and tunnel glow remain independent.
    Delivered supplies are unchanged.
  - Each cave site rotates with its own cliff face, projecting from the mountain foot with a matching blocked rock footprint.
  - Older valley saves migrate mine anchors and remembered positions without changing inventories, construction stages or assigned workers.
  - Grass and decorative bushes are cleared only inside the cave/rail approaches.
  - The loaded mine uses 1,134 triangles across fourteen visible mesh instances, excluding the scenery.
- `MineRockShape` — replaces the detached cave mound with a continuous 64-triangle, single-mesh outcrop.
  - Its rear is embedded beneath the mountain triangles sampled by `MountainShape`.
  - The middle cliff columns are removed, and the mountain triangles are clipped around the tunnel to leave its interior clear.
- `CrateShape` — shared hollow shipping-crate body and lid meshes: chamfered slats with real gaps, end handholds, fitted corner battens, nail heads, floor planks and two low runners.
  - Each crate keeps its own rear lid hinge and lifting body transform.
  - The crate uses 488 triangles and two mesh instances, compared with 264 triangles and twenty-two instances for the previous solid-backed model.
- `SignpostShape` — the market post's shared fitted signboard, mounted blackboard, pallet, rounded sheet-metal mailbox and separate order flag.
  - Two decorative storage crates reuse `CrateShape`.
  - The chalkboard uses a static generic price label, without live market values.
  - The mailbox flag keeps its order-state animation.
  - The assembly uses 1,231 triangles across six mesh instances, plus one text label.
    Its picking volume covers the board and storage.
- `FirePitShape` — the campfire's shared ring of eleven irregular faceted fieldstones, ash bed and fitted split logs with charred ends.
  - A separate shared flame mesh keeps the original flicker pivot, smoke vent and day/night light handles.
  - Recursive visible counts are 518 triangles across three mesh instances burning, and 443 across two cold.

### Carried and dropped goods

- `BackpackShape` — dropped inventory.
  - The body reuses the player's exact cached six-ring canvas backpack profile through `ActorShape.Backpack`, laid on its back in a ground pose.
  - `BackpackShape` adds ground-resting webbing: slack shoulder straps, a carry loop, a fitted dark opening and a separate curved access flap.
  - The flap keeps the existing storage lid animation and fog-memory guard.
  - The dropped pack uses 310 triangles across three visible mesh instances, compared with 48 triangles across four instances for the previous solid orange tote.
- `CartShape` — caches the courier cart's hollow tapered tray with a folded rim, continuous pull frame and hand grip, fitted bed supports and axle, dished wheels and handled payment case.
  - Cart loads reuse an 80-triangle `CrateShape.Cargo` version of the approved slatted crate, omitting hidden interiors and small hardware at this scale.
  - The six crate slots, loose-goods/cash visibility, wheel pivots and courier animation remain independent.
  - The fully loaded cart uses 1,020 triangles across nine visible mesh instances, compared with 804 across forty-five before.
    The courier's separate shared human rig adds 2,634 triangles and seventeen instances.
- Carried goods on all human roles reuse the same cached `CrateShape.Cargo` mesh as cart loads.
  - It is fitted to the centered carry volume and shared visibility handle.
  - The carried crate uses 80 triangles and one mesh instance, replacing the prior 36-triangle, three-instance solid box.
  - All roles cradle the load from below, with horizontal forearms and the crate seated above the palms; inventory behavior is unchanged.

### Nature

- Optional Tree3D trial: `./build.py play -- --showcase --trees --tree3d` previews baked Tree3D 1.1.0 geometry; `--tree3d` also works in the playable forest.
  - `src/Tree3DShape.cs` loads shared tree and berry-bush meshes and alpha-cutout foliage materials.
  - Preview the three berry bushes and a harvested bush with `./build.py play -- --showcase --bushes --tree3d`. Berries attach to actual foliage-card twigs and retain their independent harvest visibility. Decorative scenery shrubs keep their inexpensive MultiMesh.
  - `assets/models/tree3d/` contains the baked meshes, original SVG leaf/bark textures, triangle manifest and upstream license notices.
  - Regenerate with `./tools/tree3d.py`; native authoring binaries stay in ignored `.tools/tree3d` and are unnecessary to play. See [trial details and compatibility](../docs/art/TREES.md).

- `src/NatureShape.cs` — trees, berry bushes, rocks, ore deposits and loose ore as shared vertex-coloured meshes, with three seeded shape variants each.
  - Oaks have root buttresses, exposed scaffold limbs and smaller leafy boughs with folded leaf edges.
  - Spruces have a tapered leader and irregular whorls of folded, serrated needle sprays with gaps between boughs.
  - Birches have marked pale bark, fine dark twigs and separate hanging foliage clusters.
  - Each code-built standing tree uses one shared mesh and material, drawn by one visible instance and one shadow-only instance. The original shadow survives canopy fading; both instances share the shake/fall pivot. All geometry is generated during loading, and the existing stump handle remains.
  - Review the three species with `./build.py play -- --showcase --trees`; external generator research is in [docs/art/TREES.md](../docs/art/TREES.md).
  - Faceted boulders pressed into the ground.
- `NatureShape.Bush` — seven seeded foliage clumps with short branching stems and folded, two-sided leaves.
  - Berries are positioned against that variant's actual cached foliage surface and keep their separate harvest visibility pivot.
  - Each of the three variants uses 1,200 triangles across two visible mesh instances when ripe, or 880 triangles in one mesh when harvested.
  - The separate berry-heap icon stays unchanged.
- `NatureShape.Ore` and `NatureShape.OreChips` — color-neutral host/seam and fractured chip geometry shared by coal, iron and tin deposits for each of three seeded variants.
  - Cached material palettes distinguish black coal, rusty iron and pale tin; coal chips use near-black `0c0e10` without lightened grey tops.
  - The existing four-boulder profiles stay unchanged, with seams clipped flush into their faces along continuous bedding planes.
  - All three ore kinds use 863, 878 and 882 triangles across three mesh instances by variant.
  - Mining scale, empty-state visibility and picking volumes remain unchanged.
  - The tin-ore icon is rendered from the updated deposit; coal and iron inventory icons use their separate loose-ore models.
- `NatureShape.Scrub` — decorative plain bushes as a lightweight lobe that reuses the resource bush's folded-leaf builder.
  - Its neutral shading takes the existing MultiMesh colors.
  - The original 900 seeded lobe transforms, colors and mine-approach clearance remain unchanged, in one MultiMesh batch with no new per-frame work.
  - Each lobe uses 48 triangles instead of the previous sphere's 70, reducing the full shrub scatter from 63,000 to 43,200 triangles.
- `LeafShape` — replaces the leaf patch's 26 box instances with one shared mesh of twenty-four pointed, folded autumn leaves and tapered stems.
  - Three fixed seeded layouts keep variation alongside the existing ID-based yaw.
  - Each full or partially raked patch uses 264 triangles and one visible mesh instance, compared with 312 across twenty-six before.
  - Fully raked patches stay hidden until the existing daily refill.
  - All vertices fit inside the unchanged picking radius and height.

## Icons

### Model icons

`icons/` holds icons rendered from the game's own models by `./build.py icons`.
The hotbar prefers them over everything else.
Rerun the command after changing a model.

They cover the steel axe and pickaxe, stone and steel chisels, mallet and steel hammer, torch, rake, mortar, crate, wood, planks, wood parts, stone, iron ore, coal, tin ore, iron, steel and tin bars, berries, leaves and every building.

How they are framed:

- Items show the part of a model that shows them in the world, such as the furnace's log pile.
- Planks, parts and bars show as the stock a storage pile holds (wood parts as a handle, a grip and two pegs).
- Berries show as a small heap of the bush's berries.
- Buildings show as placed, with a finished mine, a ripe farm and a pile full of logs.
- Tools are seen from the side, head up and to the right.
  Everything else is seen from the game camera's angle under the game's sunlight.
- Chisels and hammers have a model per grade.
  The bench shows the slotted grade lying on its top, and a held hammer shows its own grade.

### Item atlas

`item-atlas.png` is a historical 4×3 sheet of inventory icons. The refillable water bottle now uses individually rendered model icons.
Its other cells are unused.

### SVG glyphs

`ui/` holds hand-written SVG icons:

- Line glyphs named `line_*` for the menu badges and panel buttons (build, bag, craft, crew, guide, close, filter, stash and so on), tinted in code.
- A hard hat for workers.
- Icons named by item id for items without a model of their own: steel parts, tin cans, grass, fertilizer, wheat seeds, wheat, flour, dough, and the foods: raw meat, burnt steak, bread, crushed berries, berry jam, meat stew and the canned goods.

## Fonts

`fonts/` holds the interface typefaces under the SIL Open Font License, with licence texts beside them.

- Fredoka for headings.
- Nunito for text.

Both are variable fonts weighted in code.

## Sounds

### Music

`music/` holds the ambient music as 192 kbps MP3.

- `path_to_lake_land.mp3` and `mystic_theme.mp3` play in turn during the day.
- `forest.mp3` plays at night.

Every ambient track is normalised to -18 LUFS integrated loudness with true peaks below -1 dBTP, so tracks switch without a jump in volume.

Credits: Alexandr Zhelanov ([soundcloud.com/alexandr-zhelanov](https://soundcloud.com/alexandr-zhelanov)) and Augmentality (Brandon Morris).

### Sound effects

`sounds/` holds the sound effects as 320 kbps MP3, converted from the supplied files and peak-normalised.
Variants of one sound are matched in loudness, and a numbered suffix marks them.

`mine_ore` and `bellows_pump` were made with ffmpeg:

- A pickaxe strike is an anvil clink pitched down over the rock footstep and a low axe knock.
- A bellows pump is shaped brown noise over a soft, muffled fire catch.

Credits: Kenney ([www.kenney.nl](https://www.kenney.nl)) and Little Robot Sound Factory ([www.littlerobotsoundfactory.com](https://www.littlerobotsoundfactory.com)).

## Provenance

The item atlas was generated with the built-in image generation tool for this project.
Its prompt is preserved in [PROMPTS.md](PROMPTS.md).
No external copyrighted game art or third-party packs are used.
