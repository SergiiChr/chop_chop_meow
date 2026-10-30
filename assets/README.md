# Assets

The world is rendered with code-built low-poly 3D models.
There are no world or character sprite sheets.
The only 2D images are interface icons.

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

Keep each model under its triangle ceiling, counting everything visible:

| Model | Ceiling |
| --- | --- |
| Person or animal | 5,000 |
| Building, station or mine | 2,500 |
| Tree variant | 12,000 |
| Bush, rock, deposit, crop or other prop | 3,000 |
| Carried or dropped item, icon-only model | 1,000 |

Detail past these does not show at the game's camera distance and costs frame time across many instances.

### Assembly and shared helpers

Models are assembled from code-built Godot meshes.

- `src/WorldModels.cs` — world objects, wolf and trade cart.
- `src/ActorRig.cs` — player, worker and courier.
- `src/ModelKit.cs` — shared primitive, material and fade helpers.
- `src/BottleShape.cs` — refillable tin flask, wood stopper and three-segment water gauge; all four bottle-state icons use this model.
- `src/IngotShape.cs` — shared tapered cast ingot with beveled ends, metallic finish and casting mark; used by ingot icons and pile stock.

### People and animals

- `src/HumanShape.cs` — the common neutral torso and continuous skinned limb profiles for players, workers and couriers. It shares five neutral mesh resources (hips, torso, leg, left arm and right arm) across all human roles and player shirt colors. Each actor keeps its own skeletons and pose. Cached three-color texture palettes preserve sleeve/skin, trousers/boots and sole transitions using the fade-compatible StandardMaterial3D path.
- `src/ActorShape.cs` — profile, skinning and curved strap builders. Outfit colors use cached palette materials rather than duplicating body geometry. These meshes are cached across actors while each rig poses its own joints.
- `CourierShape` — fits a reflective hi-vis vest and curved cap to the same core, with the common face details.
- `src/WolfShape.cs` — cached continuous wolf body and muzzle profiles, shaped ears, tapered legs with hocks and paws, and a brush tail. It keeps the wolf and carcass animation rig.

### Buildings and stations

- `src/CabinShape.cs` — the cabin's shared sloped wall shell with door and window openings, its continuous corrugated roof, and a simple interior. The interior has contrasting wall lining, a wooden floor and a sleeping platform, visible through the open doorway.
- `src/FurnaceShape.cs` — shared bevelled block faces, a recessed firebox, a shaped steel coping plate and a coal pile of irregular broken slabs with broad fracture faces. The furnace's outdoor pile shows its selected stocked fuel and disappears when that slot is empty.
- `src/ForgeShape.cs` — the reinforced forge's continuous rounded tank shell, fitted steel bands, recessed refractory chamber and continuous teardrop bellows boards. Its shared meshes keep separate animated bellows pivots and heat glow.
- `src/AnvilShape.cs` — the anvil's shared cast body with a curved waist, bevelled working face and swept horn.
- `src/ChestShape.cs` — the hollow wooden chest: bevelled shell and lid, fitted corner guards, loop handles and rear hinge hardware.
- `src/BenchShape.cs` — shared workbench planks with bevelled edges, fitted aprons and lower stretchers, and a mounted vise with separate jaws. The slotted chisel and hammer keep their visibility handles.
- `CookShape` — caches the cooking table's recessed masonry hearth, bevelled timber joinery, grill and open stew pot, hollow mortar with fitted pestle, and fire mesh. `WorldModels.CookingTable` holds the independently visible `Glow` and `Mortar` handles, the light and smoke vent, and the hit volumes.
- `CanneryShape` — caches the cannery's bevelled steel stove with an open firebox and flue, fitted timber counter, shaped stockpot and lid with open handles, and hand press with cans. Bevels are limited to broad edges. Simplified can profiles and omitted covered caps keep the model light. The fire, light and smoke controls are on `WorldModels.Cannery`.
- `RackShape` — caches the tool station's fitted concrete feet, bevelled planks and cap, and shaped axe, pickaxe and rake. Each tool has its own `Racked` visibility pivot.
- `PileShape` — caches a pile-only dirt pad with fitted log edging, and stock shapes reused from other models. Sources are the mine supply log outline, nature boulders and loose-ore nuggets, furnace coal lumps, and the bench/mine stock-board and bar primitives. Connected lumps are extracted whole and logs omit redundant cylinder rings. Coal slots reuse the furnace's closed chunk with varied rotations. Families share one baked mesh across piles, fitted to the fourteen 9–4–1 slot positions. Items without a suitable source use a color-tinted block. `WorldModels.SyncPile` changes mesh and material with inventory, while `WorldView` keeps its sight guard, so remembered stock shows in fog. The farm yard uses `PileShape.Yard`.
- `FarmShape` — caches the farm's shaped soil rows and three seeded variants of sprouts, green plants and ripe wheat. Leaves are tapered and two-sided, stems are triangular and grain heads are shaped. One crop mesh is shown per growth stage under the `Crops` handles.
- `MineShape` — caches the mine's plan outline, inward-facing tunnel shell, spoil pad, fitted portal and sloped board hood, inner supports and rail runs. The cart has a continuous flared tub, inner lining and rim, and axles and wheels seated on the rail tops. Its load reuses loose-ore geometry. Construction pivots, cart/load visibility, lantern fire/light and tunnel glow are independent. Each cave site rotates with its own cliff face, projecting from the mountain foot with a matching blocked rock footprint. Grass and decorative bushes are cleared only inside the cave/rail approaches.
- `MineRockShape` — a continuous single-mesh outcrop at the cave mouth. Its rear is embedded beneath the mountain triangles sampled by `MountainShape`. The middle cliff columns are removed, and the mountain triangles are clipped around the tunnel to leave its interior clear.
- `CrateShape` — shared hollow shipping-crate body and lid meshes: chamfered slats with real gaps, end handholds, fitted corner battens, nail heads, floor planks and two low runners. Each crate has its own rear lid hinge and lifting body transform.
- `SignpostShape` — the market post's shared fitted signboard, mounted blackboard, pallet, rounded sheet-metal mailbox and separate order flag. Two decorative storage crates reuse `CrateShape`. The chalkboard uses a static generic price label, without live market values. The mailbox flag animates with the order state. The picking volume covers the board and storage.
- `FirePitShape` — the campfire's shared ring of eleven irregular faceted fieldstones, ash bed and fitted split logs with charred ends. A separate shared flame mesh has the flicker pivot, smoke vent and day/night light handles.

### Carried and dropped goods

- `BackpackShape` — dropped inventory. The body reuses the player's cached six-ring canvas backpack profile through `ActorShape.Backpack`, laid on its back in a ground pose. `BackpackShape` adds ground-resting webbing: slack shoulder straps, a carry loop, a fitted dark opening and a separate curved access flap. The flap animates as the storage lid and has a fog-memory guard.
- `CartShape` — caches the courier cart's hollow tapered tray with a folded rim, continuous pull frame and hand grip, fitted bed supports and axle, dished wheels and handled payment case. Cart loads reuse a `CrateShape.Cargo` version of the slatted crate, omitting hidden interiors and small hardware at this scale. The six crate slots, loose-goods/cash visibility, wheel pivots and courier animation are independent. The courier uses the shared human rig.
- Carried goods on all human roles reuse the same cached `CrateShape.Cargo` mesh as cart loads. It is fitted to the centered carry volume and shared visibility handle. All roles cradle the load from below, with horizontal forearms and the crate seated above the palms.

### Nature

- Tree3D 1.1.0 supplies the baked tree and berry-bush geometry. Preview it with `./build.py play -- --showcase --trees`.
- `src/Tree3DShape.cs` loads shared tree and berry-bush meshes, and cutout foliage shader materials that sway in the wind (`src/Wind.cs`) and have a blended twin for fading.
- Preview the three berry bushes and a harvested bush with `./build.py play -- --showcase --bushes`. Berries attach to foliage-card twigs and have their own harvest visibility. Decorative scenery shrubs use a MultiMesh.
- `assets/models/tree3d/` contains the baked meshes, SVG leaf/bark textures, triangle manifest and upstream license notices.
- Regenerate Tree3D output with `./tools/tree3d.py`. Native authoring binaries stay in ignored `.tools/tree3d` and are not needed to play. See [tree3d/README.md](models/tree3d/README.md).
- `src/NatureShape.cs` supplies shared vertex-coloured meshes for stumps, decorative shrubs, fruit, rocks, ore deposits and loose ore.
- `NatureShape.Ore` and `NatureShape.OreChips` — color-neutral host/seam and fractured chip geometry shared by coal, iron and tin deposits for each of three seeded variants. Cached material palettes distinguish black coal, rusty iron and pale tin; coal chips use near-black `0c0e10` without lightened grey tops. Seams are clipped flush into the four-boulder profiles along continuous bedding planes. The tin-ore icon is rendered from the deposit; coal and iron inventory icons use their separate loose-ore models.
- `NatureShape.Scrub` — decorative plain bushes as a lightweight lobe that reuses the resource bush's folded-leaf builder. Its neutral shading takes the MultiMesh colors. The baked-placement forest scatters three-lobe bushes, filtered away from water, trails, resources and mine approaches, in one MultiMesh batch with no per-frame work.
- `LeafShape` — one shared mesh of twenty-four pointed, folded autumn leaves with tapered stems for the leaf patch. Three fixed seeded layouts add variation alongside the ID-based yaw. Fully raked patches stay hidden until the daily refill. All vertices fit inside the picking radius and height.

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
- Tools are seen from the side, head up and to the right. Everything else is seen from the game camera's angle under the game's sunlight.
- Chisels and hammers have a model per grade. The bench shows the slotted grade lying on its top, and a held hammer shows its own grade.

### Item atlas

`item-atlas.png` is a 4×3 sheet of inventory icons.
`src/Hotbar.cs` loads it, and only the water icon (cell 10) is taken from it.
Every other icon comes from `icons/` or `ui/`.

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
Measure a new track with ffmpeg's `ebur128` filter, then apply the gain that brings it to the target.

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
