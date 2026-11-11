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

- `src/WorldModels.cs` — world objects, wolf carcass and trade cart.
- `src/ActorRig.cs` — player, worker, courier and the intro film's boss.
- `src/intro/IntroSets.cs` — the intro film's office floor, rented room and train car, and its baseball bat.
- `src/ModelKit.cs` — shared primitive, material and fade helpers.
  A material is flat colour or one of the surfaces wood, metal, stone, cloth and earth: the colour stays the model's
  own tint, and the surface lays a photoscan's grain and bump over it in the model's own space, in the material's detail
  layer, so the marks stay put as it moves and a model's own albedo texture stays free.
  `Mat` makes a flat material and `Wood`, `Metal`, `Stone` and `Cloth` a surfaced one for primitives; `Vertex` dresses
  a mesh that carries its own colours and `Dress` any standard material. The foliage surface is laid on the meadow's
  shrubs by the ground dressing shader. Skin, hair, fur, glass, leaf cards and flames stay flat.
  The surfaces' grain and bump textures are in `models/surfaces/`, packed by `tools/surface_textures.py`.
- `src/BottleShape.cs` — refillable tin flask, wood stopper and three-segment water gauge; all four bottle-state icons use this model.
- `src/IngotShape.cs` — shared tapered cast ingot with beveled ends, metallic finish and casting mark; used by ingot icons and pile stock.
- `StockPile` in `src/PileShape.cs` — piled resources on any model: fixed slots that each show one item's cached pile stock.

### Showing piled resources

Show resources piled on or beside a model with a `StockPile`, never with meshes of the model's own, so a good looks the same in every pile.

- Build it with the slot positions and a scale: stock is centred on its slot, about 0.72 wide and 0.4 tall at scale 1.
- Sync it from the view with `Show(slot, item, data)` for one item per slot, or `Fill(item, count, perSlot, data)` for a heap of one item that grows a slot at a time.
- Both swap only cached meshes and materials, so they allocate nothing once warmed.
- An item without stock of its own shows the tinted fallback block; give its family stock in `PileShape` rather than drawing it elsewhere.
- Storage piles, the furnace's fuel and the packing station's goods use it.

### People and animals

- `src/HumanShape.cs` — the common neutral torso and continuous skinned limb profiles for players, workers and couriers. It shares five neutral mesh resources (hips, torso, leg, left arm and right arm) across all human roles and player shirt colors. Each actor keeps its own skeletons and pose. Cached three-color texture palettes preserve sleeve/skin, trousers/boots and sole transitions using the fade-compatible StandardMaterial3D path.
- `src/ActorShape.cs` — profile, skinning and curved strap builders. Outfit colors use cached palette materials rather than duplicating body geometry. These meshes are cached across actors while each rig poses its own joints.
- `CourierShape` — fits a reflective hi-vis vest and curved cap to the same core, with the common face details.
- `src/WolfShape.cs` — one cached skinned wolf mesh on a skeleton: deep chest, full belly with a slight tuck, a croup sloping into heavy thighs, thick maned neck, wedge muzzle with a jaw and teeth, upright ears, legs with elbows, hocks and paws, and a three-bone brush tail. The coat darkens along the back, warms to tan on the flanks and pales underneath.
- `src/WolfModel.cs` — poses the wolf skeleton for gaits, standing, leaping, pinning, feeding, recoil and falling, with IK legs and blending between motions.

### Buildings and stations

- `src/CabinShape.cs` — the cabin's shared sloped wall shell with door and window openings, its continuous corrugated roof, and a simple interior. The interior has contrasting wall lining, a wooden floor and a sleeping platform, visible through the open doorway.
- `src/FurnaceShape.cs` — shared bevelled block faces, a recessed firebox, a shaped steel coping plate and a coal pile of irregular broken slabs with broad fracture faces, the source of coal pile stock. The furnace's outdoor `StockPile` shows its selected stocked fuel, a pile for every two, and disappears when that slot is empty.
- `src/ForgeShape.cs` — the reinforced forge's continuous rounded tank shell, fitted steel bands, recessed refractory chamber and continuous teardrop bellows boards. Its shared meshes keep separate animated bellows pivots and heat glow.
- `src/AnvilShape.cs` — the anvil's shared cast body with a curved waist, bevelled working face and swept horn.
- `src/ChestShape.cs` — the hollow wooden chest: bevelled shell and lid, fitted corner guards, loop handles and rear hinge hardware.
- `src/BenchShape.cs` — shared workbench planks with bevelled edges, fitted aprons and lower stretchers, and a mounted vise with separate jaws. The slotted chisel and hammer keep their visibility handles.
- `CookShape` — caches the cooking table's recessed masonry hearth, bevelled timber joinery, grill and open stew pot, hollow mortar with fitted pestle, and fire mesh, plus the bowl and the pestle as separate meshes for `ActorRig` to put in a grinding actor's hands. `WorldModels.CookingTable` holds the independently visible `Glow` and `Mortar` handles, the light and smoke vent, and the hit volumes.
- `CanneryShape` — caches the cannery's bevelled steel stove with an open firebox and flue, fitted timber counter, shaped stockpot and lid with open handles, and hand press with cans. Bevels are limited to broad edges. Simplified can profiles and omitted covered caps keep the model light. The fire, light and smoke controls are on `WorldModels.Cannery`.
- `WorldModels.PackingStation` — a long waist-high table with a sage-painted top, sled-footed legs, straw on the shelf, kraft paper and a twine spool, built in `BenchShape.Packing`.
  - The crate being packed sits open on the table only while packing goes on.
  - Beside it a heap of the good grows a mound at the first item, a third and two thirds of a crate: the same stock a storage pile shows for that good, scaled down.
  - Up to three lidded crates stack beside the table, one for each empty crate the station holds.
  - Fully stocked it stays under 2,450 triangles.
- `RackShape` — caches the tool station's fitted concrete feet, bevelled planks and cap, and shaped axe, pickaxe, rake, hammer and chisel. Each of the four pegs has a pivot per tool shape in `RackProp.Pegs`, and shows the one its slot holds.
- `PileShape` — caches a pile-only dirt pad with fitted log edging, and stock shapes reused from other models. Sources are the mine supply log outline, nature boulders and loose-ore nuggets, furnace coal lumps (also for charcoal), and the bench/mine stock-board and bar primitives. Connected lumps are extracted whole and logs omit redundant cylinder rings. Coal slots reuse the furnace's closed chunk with varied rotations. Families share one baked mesh across piles, fitted to the fourteen 9–4–1 slot positions. Items without a suitable source use a color-tinted block. `WorldModels.SyncPile` fills the pile's `StockPile` from its inventory, while `WorldView` keeps its sight guard, so remembered stock shows in fog. The farm yard uses `PileShape.Yard`.
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
- `NatureShape.Ore` and `NatureShape.OreChips` — color-neutral host/seam and fractured chip geometry shared by coal, iron and tin deposits for each of three seeded variants. Cached material palettes distinguish black coal, rusty iron and pale tin; coal chips use near-black `0c0e10` without lightened grey tops. Seams are clipped flush into the four-boulder profiles along continuous bedding planes. The tin-ore icon is rendered from the deposit; coal, charcoal and iron inventory icons use their separate loose-ore models, charcoal in brown-black.
- `NatureShape.Scrub` — decorative plain bushes as a lightweight lobe that reuses the resource bush's folded-leaf builder. Its neutral shading takes the MultiMesh colors. The baked-placement forest scatters three-lobe bushes, filtered away from water, trails, resources and mine approaches, in one MultiMesh batch with no per-frame work.
- `LeafShape` — one shared mesh of twenty-four pointed, folded autumn leaves with tapered stems for the leaf patch. Three fixed seeded layouts add variation alongside the ID-based yaw. Fully raked patches stay hidden until the daily refill. All vertices fit inside the picking radius and height.

## Icons

### Model icons

`icons/` holds icons rendered from the game's own models by `./build.py icons`.
The hotbar prefers them over everything else.
Rerun the command after changing a model.

They cover the steel axe and pickaxe, stone and steel chisels, mallet and steel hammer, torch, rake, mortar, crate, wood, planks, wood parts, stone, iron ore, coal, charcoal, tin ore, iron, steel and tin bars, berries, leaves and every building.

How they are framed:

- Items show the part of a model that shows them in the world, such as a storage pile's logs.
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

- Line glyphs named `line_*` for the menu badges and panel buttons (build, bag, craft, crew, guide, close, filter, stash, trash and so on), tinted in code.
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

`wolf_howl` (a lone wolf, the night's first howl) and `wolf_howl_group` (the pack answering) were supplied for this project.

Credits: Kenney ([www.kenney.nl](https://www.kenney.nl)) and Little Robot Sound Factory ([www.littlerobotsoundfactory.com](https://www.littlerobotsoundfactory.com)).

### Intro film

`intro.ogv` is the intro film the game opens with: 480×270, 15 frames a second, Ogg Theora with Vorbis sound.
It is made from frames the game draws ([README](../README.md#intro-film)); make it again after changing a model it
shows.

`intro/` holds the sounds only the film uses, mixed into it with the game's own sounds and music; a `.gdignore` keeps
them out of the game.

- `hit.mp3`, `boss.mp3` and `bag.mp3` — the bat on the computer, the boss yelling and a bag being packed and zipped,
  supplied for this project.
- `train.mp3` — six seconds of "Inside old train" by kokoreli777 (Pixabay 169418).
- `office.mp3` — thirteen seconds of "IT office 3" by thellywellyn (Pixabay 535970).
- `office_room.mp3` — thirteen seconds of "Office ambience 01 (owi)" by freesound_community (Pixabay 57193), laid over
  it.

The train and office recordings were downloaded from Pixabay; their titles and authors are as the downloads' file names
give them.

## Provenance

The item atlas was generated with the built-in image generation tool for this project.
Its prompt is preserved in [PROMPTS.md](PROMPTS.md).
The ground textures in `environment/terrain3d/textures/` are packed from three [ambientCG](https://ambientcg.com)
photoscans, Grass004, Ground037 and Rock030, released under CC0 1.0; `tools/ground_textures.py` downloads the pinned
1K PNG archives, checks their SHA-256 and packs them at 512 px with `tools/pack_textures.gd`.
The model surfaces in `models/surfaces/` come the same way from Wood049, Metal038, Rock030, Fabric061, Ground037 and
Moss002, packed at 256 px by `tools/surface_textures.py` with the same script: a grain EXR holding each pixel's
brightness around the photoscan's mean as a multiplier, at the ground's detail strength, and the normal PNG. The cloth
weave keeps all of its contrast and is drawn coarse, since a fabric scan is nearly even at the game's distance.
No other external game art or third-party packs are used.
