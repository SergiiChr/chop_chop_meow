# Chop Chop Meow

An isometric woodland survival and production prototype.
Gather nearby materials, build a small settlement, craft parts and tools, pack steel tools into crates and sell them, then hire workers to automate resource gathering and furnace work.
Play alone or with up to three friends at home or over the internet.

The world is rendered in 3D with low-poly models under an orthographic isometric camera: natural woodland, modern DIY buildings and readable silhouettes.
Item and building icons are rendered from the same models, and the menu and panel glyphs are drawn icons.

## Run on Linux

From the project directory:

```sh
./build.py play
```

`play` exports the optimized release build into `build/play/` and runs it, so every launch first spends a little
time exporting.
The game's log says which build runs: `Build: optimized release export`.
The checks, `editor`, `icons` and `tools/environment_review.py` run the debug build from the project folder.

The launcher keeps Godot 4.7.2 .NET, .NET 10, import files, export templates and build output under `.tools/`
and `build/`.
First-time setup downloads missing tools and needs internet access.
To use an installed Godot 4.7.2 .NET binary, set `GODOT_BIN=/path/to/godot`.

Interactive play needs a Vulkan-capable graphics driver.
The project uses Godot's Forward+ renderer, which picks a discrete GPU over an integrated one by itself.

### Launcher commands

```sh
./build.py play     # export the optimized release build and run it
./build.py setup    # prepare local tools, compile C#, and import assets
./build.py editor   # open the Godot editor
./build.py check    # run every check: C# and Python types and format, the docs' code names, then all tests
./build.py test     # run only the tests
./build.py build    # check, export, and smoke-test build/ChopChopMeow.x86_64
./build.py icons    # re-render item and building icons from their models (opens a window)
./build.py record build/recordings/out.avi -- --showcase --frames 120  # record the game to a movie at 60 frames a second
```

### Release bundle

`build` also produces `build/ChopChopMeow-linux-x86_64.tar.gz`.
Extract it and run `ChopChopMeow/ChopChopMeow.x86_64`; keep the adjacent `.pck` and runtime folder together.

The bundle includes the .NET runtime and does not need the SDK.
The extracted bundle is smoke-tested without the development SDK in its environment.

### Developer review options

Play accepts Godot arguments after `--`:

| Option | Effect |
| --- | --- |
| `--showcase` | Load a populated review fixture instead of a fresh game |
| `--showcase --wolves` | Add a night ambush to that fixture |
| `--showcase --food-effects` | Show all four food effects under the needs plate, with second laps on health and hunger |
| `--showcase --lunch` | Show the crew at lunch with the worker overview open |
| `--showcase --idle` | Show the crew with their jobs taken off, sitting at a campfire that has no wood and burns while they sit |
| `--showcase --campfire-music trio` | Show idle workers playing guitar, harmonica and drum at the same campfire, with a listener. Use `guitar`, `guitar-perch`, `harmonica` or `drum` to see one worker get ready to play alone. Add `--campfire-visit` to include the worker's wait before the first solo |
| `--showcase --tools` | Show the camp's tool station; `--panel` opens its page, and `--filters` opens its filter window too |
| `--showcase --strike` | Show the unpaid crew holding protest signs |
| `--showcase --goal-popup` | Reach the bench in the default fixture, with no goods sold yet, so **A start** pops up as completed and then **Road to riches** as new |
| `--showcase --diary` | Show the player on the lake's shore beside its floating page, with a page lying on the ground and three finds in the diary, two of their days unread; `--diary-fall` drops that page off the oak beside them a second in instead, `--panel` opens the Diary, `--diary-day 45` opens that day large, `--diary-find` shows the pop-up of a page found in leaves and `--diary-first` leaves the diary empty and no page on the ground, so the next find is the first |
| `--showcase --firebugs` | Show a night meadow with three nettles, a tool station holding a bug net, firebugs hovering round a player who carries a bug net, and one firebug darting off; `--day-phase 0.3` shows the nettles by day, and `--chase` has the player run the firebugs down and net them |
| `--showcase --trees`, `--bushes` | Show the three tree species, or the berry bushes in their five stages with plain bushes, stumps, a tree about to fall and a shovel to dig with |
| `--showcase --environment-review coast` | Fixed daylight view of `coast`, `lake`, `forest`, `terrain`, `den` or `mine` (with `--mine-ore iron`); add `--review-fog` for fog |
| `--screenshot /absolute/path.png` | Capture the running view after startup and exit |
| `--showcase --neighbor` | Show a second player working at the bench |
| `--showcase --paths` | Show gravel paths in every shape their tiles join into, two staircases, a path under a chest and a pile of gravel; `--top` looks straight down with the tiles square on screen |
| `--showcase --hide-ui` | Hide the interface, leaving only the world in a screenshot |
| `--showcase --steel-vessels` | Show a cabin's ladder and roof barrel, standing and stacked barrels, and vessels on the tool rack, with `--zoom`, `--camera-turn` and `--camera-pitch` for a closer look |
| `--showcase --doorway queue` | Play two players at a cabin's door under the game's rules: `queue` (both walk up, one steps in, the other follows and knocks), `occupied` (one knocks while the other is inside, then stands aside as the other steps out and goes in), `wolf` (wolves tug each out of the doorway at dusk), `wounded` (a badly hurt player tugged out and pinned), `locked` (a wolf scratches at the locked door of a cabin with a player inside and goes away), `forced` (a wolf forces the unlocked door and takes the player out), `pair` (the same with two players inside, seen by the one left inside) `pack` (three wolves, two waiting behind the one at the door), `sides` (three wolves at a player stepping in: one tugs them out and one takes each arm) or `takedown` (the same with a player hurt enough to go down, pinned and fed on; the scene keeps them alive); `--panel` opens the cabin's page beside the scene; `--zoom` brings the camera closer and `--camera-turn 90` turns it that many degrees round the player, and `--camera-pitch 89` looks down from that angle |
| `--showcase --wolf-scene patrol` | Play wolves by a cabin at dusk under the game's rules, with the camera on them: `patrol` (one turns four times on its patrol), `howl` (two turn to a player who steps out and howl), `feast` (four bring a hurt player down and feed), `follow` (three a torch scared off spread round the player and circle), `campfire` (four circle the fire until a fifth closes in), `rout` (one following alone runs when the player walks at it), `stand` (one of two growls, and both turn on the player who walks at it) or `fight` (two snap at a player who swings an axe at them); every scene but the first two keeps its player alive; takes `--zoom`, `--camera-turn` and `--camera-pitch` as `--doorway` does |
| `--showcase --straw-bed sleep` | Inspect a straw bed in the clearing: `model`, `sleep`, `wake` or `wake-walk` (hold a movement key as the sleeper wakes). Add `--panel` for its controls, or `--night --torch` for the sleeper by torchlight. |
| `--showcase --day-phase 0.3` | Set the time of day, from 0 at dawn |
| `--showcase --zoom 2` | Set the camera zoom, for a close look at a model |
| `--showcase --resolution 2560x1440` | Render at a given size, such as the player's own |
| `--animation-review` | Show a grid of player and worker actions and seven wolf clips |
| `--frames 240` | Quit after this many frames, to end a recording (`./build.py record`) |
| `--slow 4` | Slow game time this many times, for a recording in slow motion |
| `--animation-review --animation-clip "player run"` | Show one clip close up, moving over marked ground at its speed in the world, with the camera following; a felling clip such as `"player steel axe"` plays against a trunk |
| `--animation-review --animation-clip "wolf leap"` | Show one wolf clip close up; the leap, hit, feed, snap and brace play against a player |
| `--animation-review --animation-clip "worker bucket roof"` | Follow a worker carrying a bucket up the ladder to pour and back down, with `"worker bottle roof"` for a bottle and `"player barrel"` or `"worker barrel"` for carrying a barrel |
| `--animation-pitch 4` | Tilt the review camera down by this many degrees, low for a side view; the game's 30 otherwise |
| `--animation-turn -90` | Turn the close-up rig from where it moves, here to face the camera |
| `--animation-target 1.3` | Stand what the close-up's blow works, a tree, rock, anvil, stump or wolf, this many metres from the actor |
| `--render-intro /absolute/directory` | Play the intro film in place of a game and save its frames there, 30 a second, with the cue sheet of its sounds |
| `--render-intro /absolute/directory --intro-stills 1.5,9.2` | Save only the film's frames at those seconds |
| `--render-intro /absolute/directory --intro-dance` | Save only the solo dance of the catgirl on the hero's screen, as tall as the picture |
| `--performance-run /absolute/directory` | Record a benchmark: sprinting from the sea past a staffed camp and the lake to the valley's top corner, by day and again at night with a lit torch; summarise it with `python3 tools/performance_report.py /absolute/directory` |
| `--drive /absolute/directory` | Play the window from command files written to that directory, as keys and mouse, and save screenshots there; it keeps `playtest-` saves and settings of its own (`playtest` skill) |

Other showcase flags are parsed in `src/showcase/Showcase.cs`.
These options are developer review tools; they do not represent a normal fresh-start playthrough.

```sh
./build.py play -- --showcase --screenshot /tmp/chop-chop-meow.png
./build.py record build/recordings/review.avi -- --animation-review --frames 120  # a movie at 60 frames a second
./tools/environment_review.py             # landscape views, saved in build/environment-review/
./tools/environment_review.py --shoreline # coast, lake edge and mine views
./tools/animation_review.py "player run"   # four views of a clip in build/animation-review/
./tools/animation_review.py "player run" --views game side top --frames 240  # include overhead and record longer
./tools/intro_film.py                     # the intro film's frames and sound cue sheet, in build/intro/frames/
./tools/intro_film.py --stills 1.5,9.2    # single frames of the film, in build/intro/stills/
./tools/intro_film.py --dance             # only the catgirl's solo dance, in build/intro/dance/
```

### Intro film

The game opens with a film of about 50 seconds, `assets/intro.ogv`, played over the start menu; Esc skips it.
It shows the hero bored at his office desk, his hand on the mouse, the catgirls dancing on his screen, his boss yelling
at him, his blank stare, the bat he takes to his computer, packing at home, the train, the walk through the forest, the
shore where he squats to touch the sea, and a cold first night by a fire with wolves howling.
It is made to look like a movie on a 1990s console: small frames, 15 a second, lightly blocky from compression.

The game draws the film itself, so a change to a model or a shot means making the movie again:

1. `./tools/intro_film.py` draws the frames, 30 a second, and writes the cue sheet of the film's sounds beside them.
   The paint filter is drawn over them 21 pixels wide, not the game's 8, so its strokes are still there after step 2
   shrinks them.
2. A movie tool turns the frames and cue sheet into `assets/intro.ogv`: keep every second frame, shrink it to 480×270,
   pack each as a JPEG so its blocks just show, mix the cue sheet's sounds from `assets/sounds` and `assets`, and write
   Ogg Theora, which the game plays and blows up to the window.
   The movie tool is not part of this project; the `intro-film` skill (`.agents/skills/intro-film`) has the commands.

`./build.py setup` downloads and installs the pinned Terrain3D Linux x86_64 runtime when needed.
`./tools/ground_textures.py` and `./tools/surface_textures.py` repack the committed ground and model surface textures
from their pinned CC0 sources, `./tools/polyhaven_textures.py` downloads the shore and ground photoscans drawn as they
are, `./tools/water_textures.py` downloads the water's textures, `./tools/food_models.py` bakes the committed
food models from theirs, and `./tools/nettle_models.py` the nettle and the nettle rope.
Sources and licenses of the environment assets are in [assets/README.md](assets/README.md).

## Start menu and options

The game opens with the intro film, which Esc skips, and then on a start menu with Continue (the most recent save), New
game, Load game, Join game, Options, Controls and Quit.
Choose Controls from the start or pause menu to see the mouse and keyboard shortcuts.

The pause menu also has New game and Quit; both ask first, since anything done since the last save is lost.

Options holds a render resolution picker, a frame rate limit (off, 60 or 90 fps), an anti-aliasing picker, a paint
filter switch (on by default), a fullscreen switch (off by default), a frame rate counter switch (on by default, shown
under the clock) and a music volume slider.
Anti-aliasing is off, FSR Upscale, FSR Native (the default) or TAA; each FSR choice names the size it draws the world at.
FSR Upscale renders the world at two thirds of the render size and upscales it with AMD FSR 2, which smooths edges too
and costs less than full size; FSR Native smooths with FSR 2 at full size.
The interface always draws at full size.
The paint filter turns the drawn world into patches of paint, each pixel taking its colour from an area 9 render pixels
across, while name tags and the interface stay sharp.
The game renders at the picked size, 16:9 or 16:10 from 1280 × 720 to 3840 × 2400; the window opens at that size, or as
large as the screen allows, and resizing it only stretches the image, with bars when the shapes differ.
Fullscreen covers the whole screen with no window frame, still rendering at the picked size.
The display choices are stored in `settings.cfg` and applied at launch.
The interface is drawn at 0.8 of the size it is laid out for (`Game.InterfaceScale`).
It keeps that share of the screen at every resolution, with its text drawn at the size it shows.

Wind sways grass, bushes and tree foliage, drifting between calm spells and gusty ones, and grass parts around the players and workers in sight.
Footpaths wear in where the player and workers walk, not where they stand, and grow back within two game days; they are not saved, so a new or loaded game starts on unworn ground.
Fires and stoves leave soot stains on the ground, light bounces off what is on screen (SSIL), things shade the ground and each other where they
meet (SSAO), and world sounds pan with their place on screen.
The sky lights roofs and walls from above, with grass-coloured bounce from below.
In the first-person preview, the visible sky changes colour with the time of day and shows the sun or moon.
The picture is tonemapped with AgX, which keeps colours as light brightens.
A burning campfire lights the circle that deters smaller wolf packs. Other fixtures give light with no such safety.
The ground has grain and bump from grass and rock photoscans under its own colours: meadow over the valley with soft
lusher and drier patches, sparse broad patches of moss over gravelly earth, and stone on the mountain slopes.
The woods along the mountains have a photoscan of forest floor with twigs and dead leaves, and where the ground is
worn, a photoscan of muddy soil with leaves shows through, both in their own colours.
Shrubs cast real shadows; grass and small objects darken the ground under them through a shade map baked while loading.

## Controls

| Input | Action |
| --- | --- |
| WASD or arrow keys | Move in screen directions. Away from hunting wolves, reversing a run brakes, turns and runs back |
| Shift while moving | Sprint 1.6× as fast; food and drink drain 1.7× as fast; not under 15% food or drink |
| Walk or sprint on a gravel path | Move 1.2× as fast |
| Right mouse button on a loose resource or a piece of paper | Walk over and pick it up |
| Hold right mouse button on a resource | Approach and gather while held |
| Hold right mouse button on a placed crate or barrel | Walk over and pick it up with its contents |
| Right mouse button on open ground | Eat, drink, light a torch, or swing toward the pointer using the hotbar selection |
| Hold right mouse button on open ground with a shovel selected | Walk over and dig a planting hole or gather sand |
| Left mouse button | Select a resource, building, crate or worker, or place a selected building. Put a held stack into the storage clicked, or let go of it anywhere else. On a piece of paper, walk over and pick it up |
| B | Open building plans, and the remove selector that takes buildings down |
| Tab | Open or close the backpack |
| C / V during placement | Turn a building, crate, barrel or pipe 90° |
| C outside placement | Open crafting |
| LMB / RMB while placing steel pipes | Lay a pipe / take up the pipe under the pointer |
| R / T while placing steel pipes | Turn 90° / cycle the pipe type |
| H | Open the crew overview |
| 1–9 or click a hotbar slot | Select and outline that slot. Start placement if it holds a plan or placeable container |
| Pick up a plan or backpack item and click a hotbar slot | Assign it to that slot |
| Layer button below the status bar | Switch between the surface and underground. Underground, click a pipe to see its contents and flow rate |
| Space with a reinforced furnace or water pump selected | Walk into position if needed and pump once |
| Space otherwise | Swing an axe or fist along the held movement keys, the last direction you walked, or where you look in first person |
| F5 / F9 | Quick save / load to the current slot; until a slot is chosen they open the save or load screen |
| Escape | Cancel placement, assignment or the walk to a cabin, close a day open in the Diary, close a panel, or open the pause menu with the Goal Log, save, load, options, Controls, new game and quit |
| Mouse wheel | Zoom in the overhead view |
| LMB on an item slot | Pick up the entire stack or put down the held stack |
| Alt-click an item slot or recipe card | Open the Recipe map at that item without moving, using or crafting it |
| RMB on an item slot | Pick up half its stack; with a stack held, put down one |
| Double LMB on an item | Use it: eat, drink, light a torch, open a crate as storage, or select a tool (in hand with no slot selected when it is not on the hotbar) |
| Double LMB on a cabin | Open its page and walk around obstacles to its door and inside |
| Shift-click | Move the entire stack to the other open storage, or queue 5 crafting batches |
| Ctrl-click | Move all items of that type to the other open storage, or queue 10 crafting batches. Ctrl from the backpack, and Store all, leave stacks bound to the hotbar |
| RMB or double LMB on a hotbar slot | Use its item |
| Drag an empty spot on a panel | Move the side panel or the backpack drawer |

Mines face their cliff and open their panel as soon as you place them.
Other plans and crates stay active until you press Esc or run out of materials or crates.
Steel pipe placement stays active for removal when you run out of pipes.
The hotbar starts with an axe, pickaxe, chest, campfire, furnace, anvil, crafting bench, berries and water.

### First-person preview

Open **Options** from the start or pause menu and switch on **First-person preview**. Each launch starts overhead.
Move with WASD or the arrow keys relative to where you look. Move the mouse to look around.
Left and right click act on nearby things under the centre dot.
Space swings where you look, or walks into position and pumps once if a furnace or water pump is selected.
Opening a panel or menu frees the pointer so you can click its controls. Close it to look around again.
The valley is visible out to the horizon. Look down to see your body, and swing to see your arms and weapon move.
Cabins, being downed and underground pipe work use the overhead view.

## Interface

Point at a building, resource or worker to see its name.
At the lake's edge the label reads “Collect water”.
Click a target to open its panel.

### Status bar

Health, hunger and thirst are three upright vials in the top-left corner with a heart, a stomach and a droplet under them.
Each empties as its level falls and turns red at 25% or less.
Bonus health continues the health bar in a brighter color. Only the part past 100% wraps to the bottom for a second lap.
Hunger over 100% also wraps to a brighter second lap.
Active food effects appear in a row below the plate. Hover an effect to see what it does and how long it has left.

Beside them the bank shows the coins in the trading post's wage slot, which has no limit, and the wages due each day, in red when they exceed the bank.
Under the wages it shows the coins sales brought in since dawn, green once they are above a day's wages and red until then.
Beside a trading post, put coins on it to pay in, or click it to pick up all its coins and put them in your backpack.

The day dial in the top-right corner shows the sun crossing its arc from sunrise to sunset, then the moon in its current phase until dawn.
A hit flashes red along the screen edges, and they pulse faintly while food or drink is under 15%.
Your stomach growls as food falls under 20%, and again at 15%, 10% and 5%.

Messages stack at the top middle of the screen, up to four at a time, and each fades out after 7 seconds.
They stay put when a side panel opens, which may cover their right end.
A message repeated while it still shows restarts its time instead of stacking again, and a full stack drops the one closest to fading.
Actions whose result shows by itself, such as gathering, crafting, building or filling a bottle, post no message; warnings, refusals and news do.
With a side panel open they keep to the space left of it.

### Bottom bar and hotbar

The bottom bar holds round badges for building, the backpack, crafting, the recipe map and the crew next to the hotbar.
Once you find part of the diary, a diary badge is stamped into the row after the crew's ([Diary](#diary)).

Put a held stack or a plan from the building plans on a hotbar slot to assign it.
A stack put on a slot binds that exact backpack stack to it, which then has the selected hotbar slot's rim in the backpack.
The stack goes back where it was; one held from storage, such as a chest, moves into the backpack first, into an empty slot or onto a stack of its item with room for all of it.
Slots reference backpack stacks or building plans; they are not extra storage.
A bound slot uses only its own stack: it eats, drinks, places crates and swings from it, and shows its count.

- A bound slot empties once its stack moves out of the backpack, such as into a chest or a crate.
- A stack used up, such as its last berry eaten, its last crate placed or its wood spent on crafting, passes its slot to another stack of its item at once; with none left, the slot waits greyed for the next one to come in.
- A bottle stays bound through every liter drunk, empty too, and a lit torch stays in its stack until it burns out.
- A bound stack moved onto another stack of its item takes its hotbar slot along; the other stack's slot, if it had one, empties. Sort merges the same way.
- A stack has one hotbar slot: put on a second slot, it moves there and the first empties.
- A slot that names an item instead of a stack, as a new game's defaults and older saves do, waits greyed and binds the first stack of that item to come into the backpack.

Building slots enter placement, and tools select a working reference.
A double click on a slot uses its stack, as in the backpack.
With a building's page or an opened crate showing, Shift-click on a hotbar slot moves its own stack there, and Ctrl-click all of its item, the bound stack too.
In the backpack drawer, Shift-click moves the stack clicked, bound or not, and Ctrl-click all of its item but the stacks bound to the hotbar; Store all leaves them too.
Crafting and building costs take from bound stacks like any other.
Hotbar assignments and a tool in hand persist in saves.

Building plans are an icon grid; hovering a plan shows its description and what it still needs.

### Inventories

Inventories are grids of slots.
Most items stack to 64.
Coins stack to 1000.
Packed crates, tools and refillable bottles take a slot each.
Empty steel buckets and barrels can stack, and each filled one takes its own slot.
Empty barrels stack in your backpack alongside up to one barrel containing water.
A worker carries one barrel or one packed crate at a time, with only their tool slot free beside it.
Their page then says "Carrying a barrel" or "Carrying a packed crate" above their hands, instead of how many items they carry.

| Inventory | Capacity |
| --- | --- |
| Backpack | 24 slots |
| Chest | 5 slots |
| Worker | 12 items, like a crate: one stack of 12 or up to 12 different items; or one barrel or packed crate alone |
| Tool station | 4 slots for tools, buckets or bottles |

- You can move items in and out of storage and stations up to 15 metres away (`World.ItemReach`).
- Inventory slots retain their positions. Pick up a stack and put it down to move, merge, split or swap stacks; Sort packs loose stacks and groups them by item type.
- LMB picks up a slot's stack onto the cursor, a held stack, and RMB picks up half of it. With a stack held, LMB puts it down on a slot and RMB puts down one.
- A held stack shows its count beside the cursor. It stays in its slot until it is put down, and nobody else can take it meanwhile: another player who tries is told it is held, workers and haulers leave it, and a station holding one starts no batch.
- A stack put down that does not all fit fills the slot, and the rest stays held until you put it down elsewhere.
- A slot that does not take the held stack, such as an input slot for another item or an output slot, shakes, and the stack stays held.
- Esc, a click on open ground or on the interface anywhere but an item slot, closing the side panel or walking out of reach of the building it came from lets go of the held stack, which is then back in its slot.
- Click over open ground to place a held crate, barrel or building plan.
  Moving back over the interface returns it to the cursor.
- A press on one slot let go of anywhere else, such as over another slot, does nothing.
- Drag an empty spot on the side panel or the backpack drawer to move it. Both open in their usual spots for each new page or building.
- Double-click to eat, drink one liter, light a torch, open a crate or barrel, or select a tool in its hotbar slot or directly in hand.
  A single click on a backpack slot waits 200 ms for a second one before it picks the stack up.
- Raw food such as raw meat or wheat says it needs cooking first. What each food restores is set per item in `src/world/model/GameTables.cs`.
- A held stack put on storage clicked in the world, such as a chest, goes into it. An item only selected on the hotbar never does.
- Chests, placed crates, the tool station and compost have a funnel button beside close that opens their input and output filters in one window.
  A pile's funnel opens one row naming its one resource.

### Station panels

Every station and the crafting menu share one layout: recipe cards under a heading for each group of what they make (tools, materials, containers, food).
A click on a heading closes its group and another opens it; the list scrolls once it outgrows the page.
A card shows the product, its name and what a batch takes, each count in brick while your backpack is short.
A recipe whose tool is nowhere to hand names it instead, such as "No steel chisel".
A card shows how many batches of it wait at its top right corner.
The crafting menu also shows its queue under the work in progress, one queue slot per recipe in the order first queued, however many batches of it wait.

Hovering a card shows what the recipe requires, how long it takes, what its product is used for and where its tool is:

| Tool line | Means |
| --- | --- |
| available (green) | Slotted at the station, or carried when crafting by hand |
| in your backpack (yellow) | The station's slot is empty and you carry the tool: you can work the recipe, a worker cannot |
| not available (red) | Neither slotted nor carried; a tool made by hand with no tool, such as the stone chisel, says what it takes |

A station's tool slots sit in a small window of their own left of its page, "Available tools", each with the tool it holds.

| Input on a card | At a station | In the crafting menu |
| --- | --- | --- |
| Click | Mark it for workers to keep making once the batches you queued are done (a thick outline with a hard hat); click again to unmark | Queue one |
| Double-click | Queue one and load one batch of its inputs and a fuel from the backpack; the hard hat goes on the recipe | Queue one |
| Shift-click, Ctrl-click | Queue 5 or 10 and load their inputs and a fuel each from the backpack; the hard hat goes on the recipe | Queue 5 or 10 |
| Right-click, with Shift or Ctrl | Take 1, 5 or 10 waiting batches off | Take 1, 5 or 10 off and get their materials back |

A right-click on a queue slot in the crafting menu takes batches off the same way.
A batch leaves the count when it starts, and one in progress cannot be taken off.

A station is on one recipe at a time, and its page shows that recipe's slots; the crafting menu queues as many recipes as you like.
The card of the recipe a station is on has an outline, thicker and with the hard hat when it is also the marked one.
A station nobody has marked or queued anything at is on the first recipe of its list.
Queueing more of the recipe a station is on adds to its count.
Queueing another recipe depends on what the station is doing:

| The station | Queueing another recipe |
| --- | --- |
| Is working a batch | Nothing changes, and its input and output slots shake |
| Is between batches, with something in its slots | Inputs the new recipe uses stay in its slots, and fuel it burns stays in the fuel slot; the rest goes into your backpack, its product too even where the new recipe uses it, the batches that still waited are dropped, and it turns to the new recipe, its hard hat with it |
| Is between batches, with no room in your backpack for what leaves its slots and fuel slot | Nothing changes, and its slots shake |
| Has empty slots | It turns to the new recipe, its hard hat with it |

So a station turned from the steel pickaxe to the steel shovel keeps its steel parts and wood parts, and one turned to the bucket keeps the wood parts left over.
A turn loads from your backpack only what the slots still lack for the batches queued; more of the recipe a station is on loads a whole batch on top of what it holds.
Fuel a batch has already burned is gone; queueing a recipe loads its fuel again from your backpack.
When your backpack holds less than one batch, queueing loads what it has, its slots shake, and a message names everything still missing, such as "Missing fuel (wood, coal or charcoal)." or "Missing 2L of water."

A click that would move the hard hat to another recipe than the one the station is on is refused while a batch is being worked or the slots hold anything.
The slots shake a moment after such a click, once it is clear no second press makes it a double-click.
Taking the hard hat off, and putting it on the recipe the station is on, always work.
A hard hat moved to another recipe drops the batches that still waited.

So a station with a hard hat is always on the hard-hat recipe: once the batches you queued are done, workers go on making it.
Only a worker works a hard-hat batch at the bench, anvil or cooking table: you work only the batches you queued there.
Click the hard hat off for a station to stop after your batches.
It starts no batch while its slots hold something another recipe left there, such as a hauler's late delivery: haulers carry that to storage, or you take it out.
A station page opens with the group of the recipe the station is on open.
A station with nothing queued and nothing marked makes nothing, and stays on the recipe it was on.

A bench, anvil or cooking table with a batch you queued is worked by a player standing still within 1 m of its edge, or by a worker; a packing station by either, whatever it packs.
A reinforced furnace pumps at once within reach.
For glass, the pump position is behind the furnace.
Press Space with the furnace selected, or click Pump, to walk to the bellows and press once if out of reach.

Stations show one slot per recipe input, the batch time above a progress bar and the output slot, with what one batch takes or makes under each slot.
The output slot takes back what the recipe makes from your backpack, up to a stack.
By hand a station takes only what its recipe uses, the fuel it burns in its fuel slot, and what it makes; anything else is refused, such as "Stone is not used by what this station makes."
A station that burns fuel has its own fuel slot under the bar, marked by a flame, with how long the fuel in it lasts and the fuels the recipe burns beside it.
Fuel the recipe cannot burn shows in brick; workers carry it back to storage and bring a fuel it burns.
Side outputs and leftovers sit in a second row under the output.
Each slot holds a single stack of 64.

- Put a held backpack stack on its input slot to load it, RMB to load one, or Shift-click the backpack stack to load it and Ctrl-click to load all of that item.
- LMB on a slot picks up its stack, RMB half; Shift-click or Ctrl-click moves it back to the backpack.
- A farm plot, campfire, mine and packing station have input and output slots that work the same way. Compost, chests and the tool station are storage, whose slots take what the building holds.

### Job buttons

Every building's panel ends in a row of job buttons, one per kind of work there with how many workers do it:

| Place | Jobs |
| --- | --- |
| Crafting bench, anvil, cooking table, packing station | Craft, Haul |
| Stone furnace, canning station | Haul |
| Reinforced furnace | Bellows, Glass blower, Haul |
| Water pump | Pump |
| Mine | Build, Haul, Mine |
| Farm plot | Farm, Haul |
| Trading post | Manage deliveries |
| Chest, placed crate | Haul |
| Campfire | Tend fire |
| Tool station | Stock |
| Compost pile | Tend |
| Resource | Gather |

A worker on Tend fire keeps a campfire fed with wood from storage.
A campfire with no wood left burns anyway while a worker sits at it, at lunch or with nothing to do, and goes out once nobody sits there; a campfire with wood burns it as usual.

Clicking a job lists the crew with a tick box each.
A circled number beside a worker counts their other jobs, and hovering it lists them.
A worker can hold several jobs, at one place or several, and a new job goes to the end of their list.
The list is in order: they do the first job that can be done, and after each round of it they read the list from the top again.
A round is a delivery, a crafted batch, a crate packed full, a furnace batch at the bellows, a piece mined or a harvest.
With no job that can be done, a worker sits at the nearest campfire they can reach, as at lunch: six to a fire, the rest on the ground nearby in groups of up to three.
Each time an idle worker takes a seat at the fire, they have a 25% chance of bringing a guitar, harmonica or drum.
The choice is even among instruments not already there. If the fire already has all three, the worker sits without one.
With fewer than all three instruments, each musician plays a short sound, then waits two to three seconds after it has finished before the next.
These sounds play a little above the music volume, and guitar strums a good deal louder.
When all three instruments are at the same fire, one worker on each instrument plays a song and the other idle workers seated there nod to the beat.
The group plays one song, then each musician goes back to playing short sounds now and then, with no second song until the trio breaks up.
Workers sitting for lunch do not play or nod.
A worker who could not reach a fire's seats tries again once a building goes up or comes down.
With no campfire they wait at the nearest trading post, or in the clearing without one.
Their status says what their first job lacks.
A worker on Bellows pumps while the furnace works or holds a batch to make.
Only one worker pumps at a time.
A worker on Glass blower can work the pipe at the front.

The crew button opens an overview with each worker's daily pay and the total above.

### Taking buildings down

The building plans (B) end with a red cross: click it for the remove selector.
A plan held on the cursor is let go of by that click.
With it, the building under the mouse turns red when it is within reach, 15 metres, and a click takes it down at once,
without asking.
A gravel path tile turns red and comes up the same way, when the mouse is on gravel with no building over it.
Right-click switches between the surface and underground.
Press Esc to put the selector away.
A building's cost and everything in it go to your backpack, and what does not fit waits in a recovery box where it stood; a mine also gives back the timber, iron and stone delivered to it.
A cabin someone is in, or a trading post with a cart out, stays up until they come out or the cart is back.

## Survival

### Food, water and time

A full stomach lasts one in-game day and a full water gauge lasts one day; a day lasts 14 minutes.
Raw meat cannot be eaten.
Your refillable bottle holds three liters, each restoring one third of the water gauge.
Put a steel barrel into the Roof barrel window to the left of the cabin page to mount it on the roof.
At bedtime, the barrel serves everyone's thirst first and fills bottles second, both in the order they entered.
When players wake, any water left in the barrel goes to their thirst in the same order.
Taking down a cabin returns its barrel with its water.
Keep the empty bottle after drinking and refill it at the lake; without a bottle, drink directly from a fresh water source for one liter, taking the same gathering time.
Filling a bottle takes the same time as one gather action.
Empty or filled replacements can be bought individually.
Craft an empty bottle at a bench from one steel sheet and one wood part with a steel hammer.
A recipe that uses water, such as meat stew or dough, takes liters of it poured out.
Drag a bottle to the station's water input or Shift-click or Ctrl-click it to pour while the bottle stays in your backpack.
Ctrl-click also pours from vessels bound to the hotbar.
So water can come in batches, part-drunk bottles too; loading a batch pours from the least full bottles first.
The input slot shows a water droplet and counts liters.
Shift or Ctrl on it takes the water back into bottles in your backpack with room, the fullest first, and so does picking it up and putting it in the backpack; with no room, the slot shakes and says so.
Workers do the same: they bring bottles, pour them and carry them back to storage.
Water left in a station whose recipe no longer uses it is taken out by a hauler, who fetches bottles with room from storage, fills them there and stores them.
Double-clicking a recipe card that uses water pours it from your bottles the same way.
Turning a station that holds water to another recipe takes the water into your bottles, and needs room in them for all of it.
Taking down a station fills the bottles you carry with its water, and the rest is spilled.
These rules cover any refillable vessel the game adds (`GameData.Vessels`).

Food and drink restore hunger and thirst. Sleep normally restores health, and some cooked foods also help you recover.
Sleep at any hour in a cabin or on a straw bed. Enter the cabin first to see its Sleep button.
You start healing while you sleep, even if other players stay awake, as long as hunger and thirst both stay above 45%.
A full night can restore about a third of your health. You wake at dawn, if hunger or thirst runs out, or when you choose Wake.
Both pages forecast health, hunger and thirst under “Stats after sleep”.
Each appears as a capsule over its icon, as in the corner.
When a roof barrel is mounted, a note beneath the forecast says water is refilled from the roof barrel.
The note still appears when the mounted barrel is empty.
The forecast uses the water available in the barrel.

### Death and recovery

Death drops your backpack, coins included, in a recovery box marked by a bobbing gold gem.
Until you respawn, anything you try in the world says "You are busy."; you can still skip to morning and pick a hotbar slot.
You respawn after 30 seconds inside the cabin nearest to where you fell that has a place left, locked or not, or in the clearing without one.
Emptied boxes disappear, and workers never use them as storage.
During the respawn countdown, if no cabin exists, “Skip to morning?” lets you advance the world to dawn.
In multiplayer, every player must be downed before skipping.

### Fog of war

Fog of war covers the map in the overhead view.
Ground you have never seen is black.
You see live only within a circle around you (`SightRadius` in `src/world/model/GameData.cs`, 20 metres), which fills the screen at full zoom-out.
This circle is your line of sight: it ends where the grey fog starts.

Ground you have seen before is greyed out and shows things as you last saw them: a tree felled out of sight still stands there until you look again.
What changed while you were away shows as it now is when you come back, without playing the change: such a tree is already a stump, and a chest that was filled keeps its lid shut.
Remembered fires and furnaces show unlit, wolves, workers and carts are not shown, and work out of sight neither shakes what it is done on nor throws chips.
Explored ground and remembered things are kept in saves.

### The valley

The world is a fixed 216×216-tile valley.
It opens onto the sea at the bottom of the screen, which cannot be drunk and is the map's edge.
Impassable mountain ranges close in from the left and right toward the valley's inland end, and a lake lies in the middle.
You can wade 2 metres into the lake's shallows, and so can workers and wolves.
Things can fall into the shallows and sink to the lake bed, except a page, which floats.
A building's middle stays 0.8 m back from the shore, so a large one, such as a cabin, may still reach a little over
the shallows.
Apart from that only a pump's intake goes in the water: a pump's handle, a cabin's ladder and the spots a furnace or
packing table is worked from need dry ground.

The plains have occasional rocks, bushes and berry bushes, and the trees thicken into woods toward the mountains, so finding more means going out to look.
Click a resource to see what it yields in read-only item slots with a count for each item.
The lake shows a water droplet in an unnumbered slot.
A berry bush holds 24 berries and is full again a day after the first one is picked from it, whether or not it was picked clean.
It shows fewer berries as they are picked, in five stages: full, three quarters, half, a quarter and bare once picked clean, each stage 6 berries apart.
A felled tree leaves a stump. Trees grow again from planted acorns. A couple of hundred plain bushes grow about the plains.
Hold right mouse on a stump or a plain bush with a shovel in your backpack to dig it up: a stump gives 3 wood and a bush 36 leaves, three times what lies under an oak.
It comes out whole after a few seconds of digging and is gone for good, so the backpack needs room for all of it.
Iron and coal deposits sit at the foot of rock faces at the base of both ranges and need a pickaxe.
Loose coal is scattered below the nearer coal face and is picked up whole with one right-click, with no tool.
Loose stones and sticks lie about the plains: each picks up the same way as 1 stone or 1 wood, and is gone once picked.
A pickaxe hit on a rock or an ore deposit knocks a loose piece of it off one time in twenty; it flies off and lands within 2 tiles, ready to pick up.
A pale grey cliff further inland holds tin. Mine it with a pickaxe.

### Nettles and firebugs

Three nettles come up each night where nobody is looking, until ten stand in the valley.
Hold right mouse on one to pick it: it takes 3 seconds, gives 1 nettle and stings for 1% of health a second, which can kill.
The player groans in pain for as long as it stings, and other players hear the groan from where the picker stands.
A picked nettle is gone for good, a backpack with no room for it leaves it standing, and nothing is built or laid where one stands.
Nettle rope is made at the crafting bench, 1 rope from 1 nettle, and a bug net from 5 rope, 1 wood and 1 wood part ([Crafting bench](#crafting-bench)).
Whoever makes rope strikes two mallet blows, then twists the fibres by hand, in turn.

Firebugs come out in clusters of one to three, eight clusters as night falls and one more after each hour of the night, at least 25 m from every building and where nobody is looking.
A quarter of the clusters have three firebugs, and the rest two or one at even odds, all within 1 m of the first one.
A cluster that comes out by water or near a building can be smaller, and one at the edge of sight can show its others coming out.
All of them are gone at dawn.
A firebug hovers about its place with a steady glow that grows half as bright again for about a second every 10 seconds.
When you come within 4 m it darts off 6.55 m, faster than a walk and slower than a sprint, then rests for 1.6 seconds where it stops, whoever comes near.
With the bug net selected, a right click on open ground swings it toward the pointer, down on a slant from behind the right shoulder across the front to the ground at the left, and a right click on a firebug swings it at the firebug.
The hoop crosses the front 1.0 to 1.4 m from you, within 55° either side of the pointer, and lands 0.4 seconds after the click: it catches each firebug it passes over then, one firebug each into the backpack while it has room.
A firebug nearer or further than the hoop is missed, and a swing sends every firebug it misses within 5 m darting, resting or not.
Caught firebugs live for three in-game days, then disappear one by one.
Hover over a stack to see its average time left.

Nettles, nettle rope, bug nets and firebugs are not bought or sold.
Caught firebugs give a [lantern](#lantern) its light.
A nettle, a bug net and a firebug each say a line under their name in tooltips and in their details on the recipe map.

### Sand and tides

Select a shovel and point at the sea beach to highlight a one-meter square.
Hold right mouse to dig the square. It gives 10 sand and leaves a shallow, rounded cut with dark soil at the bottom.
Cuts that share an edge join into one excavation.
The sea reaches high tide at each full moon, once every eight in-game days, and replaces the sand as it covers each square.
Hover over the moon on the clock to see how many in-game days remain until the next high or low tide.
You can dig the square again once it is clear of water.
You can walk out to the low-tide edge of the beach even when the tide is in.

### Replanting trees

An axe hit has the same one-in-twenty chance to drop an acorn as a pickaxe hit has to drop a chip.
Use right mouse to pick up the acorn.
Double-click a shovel in your backpack, or select it on the hotbar, to see an outline over open ground.
Esc leaves the outline active while the shovel is selected on the hotbar.
Hold right mouse over open ground away from beach sand to walk there and dig a hole when you get close enough.
Releasing right mouse before the dig finishes cancels it.
Open the hole and place one acorn in its empty slot to plant it. A small nub marks the planted acorn.
An unused hole disappears at dawn.
Click the nub to see its status and a read-only item slot with the wood count.
A tree shows **Planted** until you pour a total of 5 L from bottles or buckets into its water slot, then **Growing**.
After watering, the oak grows daily. It holds 4 wood after one day, 21 after five, and 42 when it becomes an **Adult tree**
after ten. You can chop it from the first day it produces wood.
All standing trees keep growing. After adulthood, size and wood grow logarithmically, adding less as each tree ages.
A tree that has never been chopped holds 71 wood at age 20 days and 100 at age 40 days.
Starting trees are already 10 days old.
A growing trunk destroys any building it reaches.
Players in a crushed cabin move outside, and stored items go into a recovery box where the building stood.

When a replanted oak becomes an adult it makes a spot of leaves near its trunk.
The leaf spot grows wider as the tree grows and keeps that size after the tree is felled.
Gathering leaves from any such spot also picks up the acorns there.
A leaf spot holds one or two acorns and replaces one every ten in-game days until full.

## Progression

The opening progression prioritizes water, shelter and fire, then hand gathering with the starting steel axe: chisel, planks, mallet, bench and crates; sell berries at a trading post for the first pickaxe; then work toward steel.

### Buildings

The placement preview stays visible over the shoreline, water and existing buildings.
A building, yard or crate goes up over stumps and plain bushes and clears them away, giving nothing for them.
A cabin needs dry, open ground at its door and at the foot of its ladder.
Those spots stay clear of other buildings.

| Building | Cost |
| --- | --- |
| Storage chest | 8 wood |
| Campfire | 6 wood, 4 stone |
| Cabin | 20 wood, 8 stone |
| Pile, farm plot | 8 wood |
| Gravel path, one tile | 4 gravel |
| Trading post | 10 wood, 4 stone |
| Crafting bench | 10 planks |
| Tool station | 8 planks, 4 stone |
| Packing station | 8 planks, 4 wood |
| Stone furnace | 16 stone, 6 wood |
| Anvil | 4 iron bars, 6 stone |
| Reinforced furnace | 20 stone, 5 iron bars, 8 wood |
| Cooking table | 2 steel bars, 6 stone, 8 planks |
| Canning station | 2 steel bars, 8 planks |
| Mine | free to mark; its second stage takes 50 wood, 10 iron bars and 50 stone |

### Goal Log

Press Esc to open the Goal Log.
Click a goal name for its details, or expand **Completed** to revisit finished goals.
Hover over an item icon to see its name.
Click it to open the recipe map with that item highlighted.
Close the map to return to the goal.

You can start on the campfire, cabin, crafting bench, stone furnace, hiring and profit goals right away.
The bench reveals **Road to riches**, the stone furnace reveals **Hotter than fire**, and spotting a mineable deposit
reveals **Steel tools**.
Getting a steel bar reveals **Too soft**.
Completing it reveals **Infinite power**.

Your first diary page reveals **A success story**, which is yours alone: it counts the pages you have found, and the
diary in its details opens your Diary as its badge does.
It is complete once you have all 11 days of the diary.

**Infinite power** requires an open mine for each ore found in the cliffs.
**NEET** requires at least 1,300 coins in profit a day for seven days in a row.
Profit is the sale coins that came back that day minus wages charged at the next dawn.
The mines and profit target together show the ending screen with Restart and Keep playing.

A goal that appears during play pops up under the messages at the top of the screen with a chime: a trophy badge pops
in and a bar opens out of it with **New goal** and the goal's name.
A completed goal pops up the same way with **Goal completed** and a different chime.
A goal that appears already done pops up only as completed.
Each popup stays about five seconds, and goals that change together pop up one after another.
**A success story** pops up once the diary page you found is closed.
Click a popup to open the Goal Log on that goal over whatever is open: a held stack goes back and placing a building or
any other pick is cancelled first.
The goals a new, loaded or joined game starts with do not pop up.

### Diary

Someone lived in the valley before you and kept a diary, and its pages are scattered about.
Each player finds the pages for themselves, and a page lying in the world shows only to the player it is for.

| Where | What turns up |
| --- | --- |
| The lake | A piece of paper drifts in the shallows, there for every player who has not taken it yet: day 2 |
| Digging beach sand | One dig in twenty turns up a page at once, until you have it: day 6 |
| Gathering leaves under an oak | One gather in twenty, the same way: day 10 |
| Picking berries | One pick in twenty, the same way: day 40 |
| Chopping a tree | One chop in twenty knocks a page off it, which flutters down beside the trunk: days 36 and 37 |

The diary itself, with four more days, and a half burned page with one more cannot be found yet.

Right- or left-click a piece of paper lying in the world to walk over and pick it up.
A page that fell for you waits where it landed, even while you are out of the game.
Anything built over it clears it away, and a later chop can knock it off a tree again.
A find comes up in a pop-up that says where you found it, with a Take button and nothing else.
Until you take it you are busy: you cannot walk, gather or act.
Taking it turns a page and opens the day it holds large, the first of them when it holds two.
After your first find, closing that day stamps the diary's badge into the bottom bar, to show where the Diary is kept.

The Diary shows a cell for each of the diary's 11 days, in order, four to a row.
Each cell is a scrap of paper torn along one edge, with a loose page lying straight on it.
A day you have found is bright, with its place in the diary, 1 to 11, written in black over the page.
A day you have not found is greyed out, with a faint question mark.
A day you have not read yet has a brick rim, and the badge shows a red dot while one waits.
Click a cell to open the day large, handwritten on a white sheet shaped like the pages you find.
The close button, Esc or a click beside the sheet closes it.
The last day was written in the dark: its writing falls apart into scribbles.
Opening or closing a day turns a page.

## Crafting

### Crafting menu

The crafting menu (C) crafts at base speed, about 10 seconds a recipe, and queues up to 99 of each recipe.
Queueing takes the materials from the backpack at once.
Crafting goes on only while you stand still, squatting over the work: walking stands you up and pauses it until you stop.
Recipes are grouped by what they make; one that needs a tool you do not carry names it on its card.

A stone chisel takes 2 stone and no tool.
With a chisel you carve planks (1 wood → 4), wood parts (1 wood → 2), a wooden mallet and torches (3 wood).

### Recipe map

The recipe map icon next to Crafting on the bottom bar opens the map over the whole screen.
Esc or its close button closes it.
Alt-click an item slot, recipe card or item in a storage filter to open the map without acting on the item.
The map scrolls to the item, highlights it and opens its details. Closing the map returns to the panel you came from.
Opening it from the bottom bar or by Alt-clicking lets the game run underneath.
Opening it from a goal keeps the pause menu open underneath.
Movement controls are disabled while the map is open.
An item discovered while the map is open reveals its name and icon without reopening the map.

- The map groups items by category. Coal and the iron, steel and tin chains are in **Metals**. The wheat chain is in
  **Food**. Follow the arrows from a material to what it makes. A building branches from the material it costs most.
  Discovered cards name where the item is made or found. Marking a mine site reveals its plan.
- Items the settlement has made, gathered, bought or started with have a filled icon slot, as do their materials.
  Undiscovered items and buildings show as question marks. You can select them on the main map, and the details panel
  shows the same dashed question-mark badge. Their details and Item trees stay hidden until discovery.
- Small icons at the foot of a discovered card show a required tool (gold rim) or an additional resource (blue rim).
  Click one to find that item on the map and light it up briefly.
- Click anywhere on a card to highlight it and the arrows connected to it. The details panel on the right shows how it
  is made, what it requires and what it is used for. The button opens the nearest station that makes it or the crafting
  menu for a recipe made by hand.
- Alt-click a discovered card to open its Item tree in the same window. Direct recipe products appear under **Fuel**,
  **Ingredient** or **Tool**, and can appear under more than one use. **Buildings** shows what primarily costs this
  material. **Actions** shows what the item is for. A shovel separates **Dig up things.** for stumps, bushes and
  beach sand from **Dig holes** on open ground. The pickaxe lists **Mine ores** for extracting resources.
  The pickaxe and hammers share **Building a mine** for preparing it.
  All bottle fills share a single **Bottle** card. Bottle and Steel bucket both show **Transport liquids**.
  Water shows **Drink**, **Water farm plots** and **Water saplings**. Berry jam shows **Food**.
  Routine container handling is not listed.
  When a tree has one result, it connects the item and result on a straight line.
  Tools that can strike wolves show **Weapon**, including Torch and Steel bucket. Torch also shows **Source of light**.
  Mortar and pestle is a recipe tool only.
  Items the village buys say **Can be sold for profit.**
  A station's tree shows what it makes. Empty branches are hidden. Click a card to highlight it on the main map, or
  Alt-click a discovered card to open its tree. The upper-left **Recipe map** button returns to the view you left.

### Crafting bench

The crafting bench costs 10 planks, works twice as fast and must be worked like the anvil, by you or an assigned worker.
It has a chisel slot and a hammer slot, and a slotted tool's speed multiplies its rate.
You can also work the bench with a tool from your backpack, and the faster of that and the slotted one counts; a worker needs the tool slotted.

It makes what the menu's chisel makes (planks, wood parts, the mallet, torches and the mortar), plus these, all with any hammer:

| Recipe | Takes |
| --- | --- |
| Crate | 6 planks |
| Steel axe, pickaxe, shovel, hammer | see Steel tools below |
| Steel chisel | 1 steel part + 1 wood part |
| Rake | 1 steel part + 2 wood parts |
| Bug net | 5 nettle rope + 1 wood + 1 wood part |

Some recipes need a better tool:

- Tin cans (1 tin sheet → 2) need a steel hammer.
- Crush rocks (1 stone → 1 gravel) needs a steel hammer, and is made only at the bench.
- A mortar and pestle (3 stone, by hand or at the bench) needs a steel chisel.
- A steel water bottle (1 steel sheet + 1 wood part) needs a steel hammer.
- An empty lantern (1 glass jar + 1 steel sheet + 1 wood part) needs a steel hammer.

Nettle rope (1 nettle → 1 rope) is the one recipe a better tool spoils: it is made only at the bench and only with a wooden mallet.
The mallet may be the one in the bench's hammer slot or one in your backpack, whichever hammer the other is.
Asked for with a steel hammer and no mallet, the bench queues and loads nothing for it, does not take its mark, keeps the batches it had waiting, and says "Steel hammer will damage the fibers.", which the recipe's card says too.
Nettles can still be put into the bench's slots by hand, where they wait for a mallet.
A worker needs the mallet in the slot, so a bench with one hammer slot makes either rope or what a steel hammer makes.

A better tool always does a plainer tool's work; the steel chisel works twice as fast as the stone one.
The stone chisel itself is only knapped by hand.

### Steel tools

The anvil turns one steel bar into one steel part or two steel sheets, and one tin bar into two tin sheets.
Anvils only work while someone stands at them hammering, with a steel hammer.
The anvil's hammer slot takes only a steel hammer.
You can also work the anvil with one from your backpack, while a worker needs it slotted.
The steel hammer's speed multiplies the anvil's rate, as a tool's does at the bench.
Making a steel hammer takes steel parts, so the first one is bought at a trading post.
A small steel tool such as a hammer takes 1 steel part and a big one such as an axe, pickaxe or shovel 2, plus 1 wood part for the handle.

## Furnaces, steel and glass

- The stone furnace smelts 2 iron ore into an iron bar and 2 tin ore into a tin bar over coal or charcoal.
- It also burns 2 wood into 1 charcoal, sears meat and bakes bread (see Cooking) over wood, coal or charcoal.
- The reinforced furnace smelts 2 iron bars and 2 coal into 2 steel bars, burns only coal from its fuel slot, and needs heat from manual or worker-operated bellows. Steel forms from 1450 °C; below that it keeps burning coal without making steel.

### Glass jars

Choose Glass jar at the reinforced furnace and fit one Steel pipe in the Glass blowing pipe slot.
The pipe stays in the slot at the end of the batch.
Each attempt uses 5 Sand and 1 coal from the fuel slot.

Work in pairs.
The glass blower stands in front, faces the furnace and aims the pipe at the glowing center of the chamber.
The other person presses Space or clicks Pump to walk to the bellows and work them.
Either job can be done by a player or a hired worker.
Leave clear ground at both ends.
Each player works at the closest available work spot.
A manual stroke adds 145 °C for either furnace recipe.

A jar takes 10 seconds between 1,300 and 1,600 °C.
If the furnace cools, progress waits.
Stop pumping above 1,600 °C.
If it stays that hot for more than half a second, the glass melts off the pipe and falls to the ground.
That attempt's sand and fuel are lost, and the furnace starts a fresh batch once it is in range again and supplied.

## Cooking and canning

Sear 2 raw meat into 2 burnt steaks at the stone furnace (Sear meat recipe, one fuel per batch).
Each dough bakes into 4 bread at the stone furnace.

The cooking table (2 steel bars, 6 stone, 8 planks) is a steel grill over a stone firebox with a plank counter and a mortar slot.
With a mortar and pestle slotted and someone working it, or carried by you while you work it, it crushes 10 berries into 1 serving of crushed berries and grinds wheat into flour.
Whoever works it takes the mortar off the counter, holds the bowl in one hand and grinds with the pestle in the other.
You can do both by hand from the crafting menu with a carried mortar.

Dough is kneaded by hand at the table, with no fire, and only while you or an assigned worker work it there:

- 1 flour + 3 liters of water → 3 dough

Its other recipes cook on their own:

- 2 servings of crushed berries → 1 L of berry jam, over one wood, coal or charcoal
- 1 raw meat + 1 liter of water → meat stew, over one wood, coal or charcoal

Pour jam into a bottle, steel bucket or barrel. The same bottle shows pink gauge bars for jam and blue bars for water. Each can of jam takes 1 L and 1 tin can.
The canning station (2 steel bars, 8 planks) seals jam or meat stew over one wood, coal or charcoal.
The village buys canned jam at a base price of 80 coins per can, but does not buy loose or bottled jam.
Raw meat sells as it is, in crates.

A berry restores 5% hunger and crushed berries restore 55%. A liter of bottled jam or a can of jam fills hunger to 100%.
Hunger stops at 100% unless meat stew fills it past that point.

Cooked food descriptions in item slots and the Recipe map show how much hunger they restore and list their **Effects**:

| Food | Effects |
| --- | --- |
| Bread | **Carb load** makes Shift sprinting 25% faster for 4 in-game hours. |
| Burnt steak | **Protein recovery** restores health at the normal sleep healing rate for 4 in-game hours, even while awake. While you sleep it adds to sleep healing, so you heal twice as fast. |
| Meat stew | **Well fed** fills hunger to 150% and lasts until it falls back to 100%. The hover countdown shows how long the extra hunger will last at your current pace. Stew also gives **Protein recovery**. |
| Berry jam | **Vitamins** heals up to 10 normal HP and fills 50 bonus HP for one in-game day. Damage spends bonus HP first. Healing fills normal HP first, then the bonus. Only health above 100 HP is lost when Vitamins wears off. Jam also gives **Carb load**. |

Canned food gives the same effects as the original food.
Another serving adds each timed effect’s full duration to its time remaining without making it stronger.
Food effects end when you die.

Both stations, like the stone furnace's food recipes, take wood, coal or charcoal in their fuel slot.

## Storage

### Wood crates

A wood crate holds 12 items in total, as one stack or as single items, and its slots grow and shrink with its contents.
Place one from the hotbar, or pick it up from the backpack and click open ground; click it to open it, and hold right mouse on it to pick it up with its contents packed inside.
Double-click a crate in the backpack or on the hotbar to open it as storage.
Right-click also opens it from the hotbar.
Even an empty crate works like a chest with the backpack, and you can put it down from there.
Picking up the open crate itself closes it, and it is then a held stack like any other.

Workers never open or place crates; they pack them at a packing station.
A placed crate takes part in worker automation like a chest: gatherers and haulers store in it and take from it by its filters, and it offers Haul.
Its filters never name a crate, and no worker puts a crate into it.
A crate of goods keeps its filters when picked up and put down again; an empty crate loses them.
Putting a crate inside a crate asks first; saying yes ends the game.

### Steel barrels and buckets

A crafting bench with a steel hammer makes a barrel from five steel sheets and two steel parts.
It makes a bucket from three steel sheets, a steel part and a wood part.
The barrel recipe takes 30 seconds of base work before the bench and hammer speed bonuses.
Barrels stack in the bench output.
A pile stores empty barrels together, or barrels of one liquid at any fill, such as "Steel barrel of water".
You can carry stacks of empty barrels alongside one barrel containing water or jam.
Workers carry a single barrel in both arms and keep their tool slot free.
Carrying a barrel prevents sprinting.
A player carrying liquid in the barrel braces it and walks.
Each liter makes the carry slower, down to half speed at 50 liters.

Double-click a barrel in the backpack or on the hotbar to open its liquid slot.
Right-click also opens it from the hotbar.
Select it on the hotbar to place it.
Hold right-click on a placed barrel to pick it up without losing its contents.
Pour from a bottle or bucket into its one slot, or take liquid back into vessels in your backpack.
Water and jam cannot share a vessel.
Filling from an empty stack fills one barrel in the opened cell and stores the remaining empties elsewhere in the backpack.
The pour needs room for the split before it uses any water.
Sorting the backpack or swapping the opened barrel's cell returns you to the backpack.
Shift-click or Ctrl-click a vessel to pour into an open barrel, including vessels bound to the hotbar.
A placed barrel's single filter names its liquid, like a pile's filter names its resource.
A barrel put down empty takes no water from workers until its filter names water; one put down with water in it names water at once.
A barrel holds 50 liters.
A steel bucket holds 10 liters and hangs by its handle on the tool rack.

A placed barrel offers Haul.
A cabin offers Haul while its roof barrel is mounted.
A worker fills the bucket or bottle in their tool slot at the lake or a nearer full placed barrel, then pours it
into the barrel they were assigned to stock, while its filter names water.
Workers can fetch a bucket or bottle from the tool rack.
They climb the outside ladder to fill a roof barrel and then climb down.

### Packing station

The packing station (8 planks, 4 wood) packs one good into crates.
Click the slot at the top to choose the good from everything a crate holds.
Its two inputs each have a limit of 64, one for the chosen good and one for empty crates.
Tools take individual spaces, with room for up to a crate's worth.
It packs one item every 1.5 seconds while you or an assigned worker work it.
The crate being packed shows on the right with how full it is; a full crate waits there until someone takes it.

- Craft: works the station.
- Haul: carries out the full crate, and anything it no longer packs, to storage; brings the good and empty crates, up to a stack of each ([hauling](#hauling)).

Workers deliver the good straight to the station while its input has room.

A crate of one good is a kind of item that filters can name: "Crate of bread" in a chest's filter, or in the trading post's.
A full crate goes to the nearest storage whose filter asks for that crate, otherwise to the nearest storage taking everything.
A pile of crates takes more crates of the same good.

### Piles

A pile (8 wood) is a 3×3 yard for one resource, holding 14 stacks that show as a heap of the resource itself, bigger the more it holds.
Its resource is set by its filter, which has one row and one item: what workers bring is also what they take.
A new pile takes nothing from workers until its filter names a resource.
Set the filter on an empty pile, or put a first resource in by hand and the filter is set to it.
From then on workers store and take only that, and anything else is refused, by hand too: "This is a wood pile."
The filter stays when the pile is emptied.
It can be taken off or changed only while the pile is empty, and a new resource goes on only once the old one is off.
A bottle, bucket or barrel of one liquid is one resource at any fill, so a "Bottle of water" pile holds full and part-full bottles; empty bottles go on a pile of their own.
Crates of one good are one resource per good, and crates of mixed goods are one resource, "Mixed crate".

Piles are walkable and snap edge to edge to a pile beside them, so they line up in neat grids.

### Compost

The compost switch in the header of an empty pile with no filter makes it take leaves, grass and any food, cooked, raw or half-made, and turn 4 of them into 1 fertilizer every in-game hour.
It keeps its own filters. An empty bottle stays in the pile after its jam composts.

Workers bring leaves and grass to it first, and food only when no other storage takes it.
**Skip food delivery** stops food deliveries, while food put in by hand still composts.
A worker on its Tend job fetches what its filters take from storage and carries the fertilizer to storage.

### Filters

Storage input filters govern automated deposits; output filters govern worker withdrawals.
An empty row lets nothing through.
**Everything**, the cell marked A first in the item list, lets every item through and stands alone in its row: adding it takes the row's items off, and adding an item takes it off.
A new chest, placed crate or tool station takes nothing in and lets everything out: its input row is empty and its output row holds A.
A trading post with A asks for every kind of packed crate, and compost with A takes everything that composts.
Piles and barrels name one resource and have no A.
A row shows its items in the order they were added.
The item list that **+** opens shows items in groups, each simplest first: what is gathered, grown or bought, then materials, tools, containers and food, and last crates of goods.
Bottles, buckets and barrels come among containers twice: empty, which takes only empty ones, and holding each liquid, such as "Steel bucket of water", which takes them at any fill.
No storage names a bare liquid such as water: placed barrels and stations take liquids.
A tool station holds buckets and bottles only empty: a worker done with one that still holds something stores it like a load.
**Clear** takes everything off both rows, so nothing goes in or out.
Manual inventory transfers ignore these automation filters, and so does a page's Store all button.

A load a worker carries goes to the nearest place that asks for it:

- a chest, pile, placed crate or trading post whose input filter names it, or a packing station packing it;
- a station whose recipe uses it, into the input slot or the fuel slot, up to a stack;
- a farm plot short of seed or fertilizer for a sowing, or a mine site short of a material.

Only when none of those has room does it go to the nearest storage taking everything (A).
With none of those either, the load goes back, wherever it came from, to the nearest building whose output filter lets it out, up to one stack of it.
Storage counts by the output filter set on it, so a new chest or placed crate, whose output row holds A, takes back anything it holds.
A building with output slots has an output filter of what it outputs: a station its recipe's product, a farm plot its harvest, an open mine its ore and stone, and a packing station crates of its good.
With no such place, the load stays in hand: the worker says "Holding … · no accepting storage" and goes on with the jobs of their list that need no hands, such as Craft, Bellows and Farm, until something takes it.
Workers send a load elsewhere when a filter changes or a slot fills while they carry it.
A hauler sizes a load carried out again when picking it up, to what the place it goes to still has room for.
A chest or pile near a source fills first and buffers the goods; without one, a gatherer walks each load to the nearest station with room.

### Hauling

A worker on Haul carries one load at a time for their station, up to 12 items or one packed crate.
The next load is picked in this order:

1. an input or the fuel slot that holds less than one batch;
2. an output that is full, or anything another recipe left in the slots;
3. the input or fuel that covers the fewest batches;
4. the output that holds the most.

Inputs and fuel are filled to a stack.
A load of something the recipe uses both as an input and as fuel, such as coal for steel, is shared between the two slots as a batch uses them: of 12 coal, 8 go in as input and 4 as fuel.
A station keeps the goods it makes when no storage takes them, and goes on working until its output stack is full.
Workers fetching a station's inputs take them from storage or straight from another station's finished goods, such as steel parts from the anvil.

A chest or placed crate offers Haul too.
Its hauler first carries out what its output filter lets go, to the nearest place asking for it: never to storage taking everything.
Then they bring what its input filter names, from storage or from a station's finished goods; A names nothing to bring.
Two storages that both ask for an item never pass it to each other.
So a chest asking for crates of bread sends them on to a trading post asking for them, but not to another chest asking for them.
In a haul group, every carry for a chest or crate ranks with an input short of a batch, before full outputs and topping up.

### Tool station

The tool station (8 planks, 4 stone) is a rack with four slots for tools, buckets or bottles, including bottles holding jam. Workers use only water vessels for water jobs.
Its page lists which worker has which tool out and who waits for one.
Workers need tools for some jobs:

- an axe to chop wood;
- a pickaxe to break boulders and mine ore and coal;
- a shovel to dig up stumps and bushes;
- a hammer to fit a mine;
- a bug net to catch firebugs.

Leaves, berries and loose pieces need no tool, though a rake gathers leaves three times as fast as bare hands; a worker takes a rake when a tool station has one.
A worker without the tool walks to the nearest tool station, takes the best tool of that kind and works at its speed.
They put it back when the job in hand has no use for it or when heading home at dusk, into a tool station whose input filter takes it.
A new tool station takes nothing in, so set its input to A or name the tools for workers to put theirs back.
If there is none, the worker goes to the nearest known spot of the same job that they can work, one that needs no tool or one whose tool is to be had, and to loose pieces only after those; with none of those either, they wait, a message says once what they need, and they carry on as soon as one is put in.
A gatherer whose spot runs out goes on to the next known spot of the same resource, and to loose pieces of it only once no other spot is left.

Woodcutters skip trees that have yet to become adults.
Stumps are part of gathering wood.
A woodcutter who fells a tree digs up its stump next while a tool station has a shovel, and counts any other known stump as a spot like a tree, going to whichever is nearer.
With no shovel to be had they leave stumps and go on chopping.
A worker carries one tool at a time, so they swap the axe for the shovel at a tool station and back.
Digging up bushes is a job of its own: a worker given a bush goes on to the next known bush and never rakes leaves, and a worker raking leaves never digs up a bush.
A dug-up bush gives 36 leaves, which a worker carries off 12 at a time.

Nettles and firebugs can be given to a worker like any resource, each as a job of its own.
A nettle picker says what tool they wait for.
A firebug catcher takes a bug net and catches the firebug they reach, but workers leave at dusk, before firebugs come out, so a catcher finds none to catch yet.

A worker on the station's Stock job brings the tools its input filter takes from other storage to the rack.

Oaks drop a patch of leaves beside the trunk.
Gather them by hand, or pull a rake through them with both hands to gather them three times as fast.
They fall again every day.

## Steel piping and water pumps

Place a water pump in the freshwater lake’s shallows with its handle facing dry ground.
It costs 4 steel bars, 10 stone and 4 wood parts.
Select the pump and click **Pump** or press **Space**.
Your character walks around obstacles to the handle on the bank if needed, then pumps once.
Moving yourself or changing the selection cancels the walk.
Aim at the pump to select it, including its thin lever and piston.
Aim beside it to collect water.
You can also assign a worker the **Pump** job.
The pressure gauge reads from 0 to 5 L/s.
Each short tick is half a liter per second, the gain from one stroke.
Ten consecutive strokes reach full pressure.
After the last stroke finishes, pressure falls to zero over 10 seconds from full.

At an anvil, use a steel hammer to form one steel bar into one stackable steel pipe in 10 base seconds.
Put the hammer in the anvil’s hammer slot for a worker to use.
In building plans, pick **Steel piping** to open the cutaway and dim the surface.
Thin blue outlines mark the pipes, trench edges and building inlets against the exposed earth.
Empty pipes show bare steel.
Water turns a pipe and inlet blue, and jam turns them red, even at zero pressure.
Left-click to lay a steel pipe.
Right-click to take one back up.
Press R for a quarter turn and T to cycle through straight, T-junction and cross.
The pipe plan stays active after the last pipe so you can still take pipes up.
Press Esc to finish.
Pipes join across a shared tile side when both openings face each other.

Run the line through the tile outlined in blue under the pump and each building it serves.
A vertical pipe joins each building with plumbing to the line.
A dotted guide marks an open connection.
Put 10 steel pipes in a building's **Plumbing** slot to install its plumbing.
You can add them in batches.
Removing steel pipes from the slot disables the building's plumbing.
Taking down the building or picking up a barrel gives the installed steel pipes back.

| Building | What it takes from pipes |
| --- | --- |
| Placed steel barrel | Fills its 50 L tank |
| Cabin | Fills a mounted roof barrel through a pipe up the outside wall |
| Cooking table | A brass tap over a splash plate fills the water input for the next queued or marked batch |
| Farm plot | Four low sprinkler heads top up the soil's 10 L water reserve |
| Canning station | Berry jam for the next queued or marked can of jam |

A cabin can connect to the line before a roof barrel is mounted.
Farm sprinklers spray whenever water arrives, even on fallow or ripe plots and when the soil reserve is full.
Water fills the line outward from each running pump, up to 1 L per tile.
A nonempty plumbed barrel supplies its liquid at 1 L/s plus 1 L/s for every 10 L it holds.
It fills empty pipes even with no building drawing liquid. Its own pipe line does not drain straight back into it.
When no lake pump is feeding them, connected barrels of the same liquid equalize as the fuller one feeds the emptier one.
Water and jam need separate lines. You cannot join lines that already contain different liquids.
Buildings that need water share the available flow evenly.
A closed tap can have a full pipe at its inlet.
When pressure falls to zero, water stays in the pipes for the next building that needs it.
Cutting a line leaves the water in each surviving section.
Click the round Layer button below the left side of the status bar to switch between the surface and underground.
Click a laid pipe underground to see what it holds and its available flow rate in L/s.
The remove tool still switches layers with right-click and takes up a pipe or a surface object with left-click.

## Gravel paths

Crush rocks at a bench with a steel hammer: each stone gives one gravel.
A gravel path is a building of one tile, a metre square, for 4 gravel.
You and your workers move 1.2 times as fast on gravel, walking or sprinting, and your steps crunch; a wolf at your heels
still outruns a walk.
Workers keep their usual routes, so lay paths along the lines they already walk.

Tiles join into paths three quarters of a metre wide with round ends:

- a tile alone is a round pad;
- tiles side by side join into one strip;
- two tiles corner to corner are joined by a slanted strip, whatever lies between them;
- so a turn is cut on the slant on its inside and round on its outside, and a staircase of tiles has straight slanted
  edges;
- four tiles in a square are gravel all over.

A tile goes on any dry open ground within 5 tiles of you, also under your feet and under buildings and yards already
there; a building or yard goes up over tiles and leaves them under it.
It does not go on a tile where a tree, boulder, ore deposit, berry bush, nettle or leaf spot stands, picked clean or not.
It clears a stump or plain bush away.
Nothing grows through gravel.
A click on a path does what a click on open ground does.
Take tiles up with the remove selector, which gives the 4 gravel back ([Taking buildings down](#taking-buildings-down)).

## Farming

A farm plot (8 wood) is 6×6 tiles and snaps edge to edge with piles and other plots.
Put 1 wheat seeds and 1 fertilizer in it and it sows.
The soil darkens, and over 3 days of watered growth small green straws come up, grow into tall green plants and turn into yellow wheat.
Wheat uses 10 liters of water per day of growth and pauses when the plot runs dry.
The plot holds up to 10 liters.
Pour from a filled bucket or bottle into the water droplet slot, just like at the cooking table.
The blue bar under fertilizer shows how much water is left.
Dark patches spread across the soil as the bar fills and shrink as the crop uses water.
When the wheat ripens, the water slot and fill bar hide until the harvest is done.

Hold right mouse on a ripe plot to harvest it by hand, no tool needed.
An assigned worker also harvests, and everyone adds to one shared progress, so you can stop and come back.
A harvest takes one worker about 1 working day and gives 4 wheat, 2 wheat seeds and 1 grass into the plot's output.

Seeds do not sow themselves: move them from the output into the input slot by hand, or let a hauler do it.
The farm panel stacks seeds, fertilizer and water on the left, with wheat, seeds and grass in a row on the right.
While the wheat grows, the panel shows how much growing time is left and says when it needs water.

- A worker on Farm harvests.
- A worker on Haul keeps the plot's input slots at one sowing, also while the wheat grows: seed from the plot's own harvest first, then seed and fertilizer from storage.
  The rest of the harvest goes to storage; spare seed that no storage takes stacks up in the plot's seed slot.
  When a growing plot runs dry, the hauler takes a steel bucket from a tool station, fills it at the lake or a full
  barrel, and walks around the plot, pouring at four spots.

Wheat seeds and fertilizer can also be bought from the village.

## Trading post

The trading post has three icon buttons: Market, Workers and the price sheet.

### Market

The market works like a station with six unlabeled input boxes and six output boxes.
It only takes packed crates.
The village pays for:

- raw meat, burnt steak, berries, bread, meat stew, canned jam, canned meat stew;
- steel axes, pickaxes, shovels and hammers.

Anything else in a crate goes as a donation: the cart takes it and pays nothing for it.
The returning cart says what was donated.
A crate whose firebugs have all died stays in the market as an empty crate, and the next cart takes it as a donation too.
A sale pays for the goods and the crate.
Load crates, press Sell, and a cart takes them to Bramblewick and returns 30 seconds later with the coins.
Bought goods also arrive in the output boxes.

The filter button next to close names the crates workers bring here; with none named, workers bring nothing.
Tick Send when full and the cart leaves on its own once all six boxes hold a crate.

Trade carts keep their timing: a cart walks down the valley at a steady pace, fades out once it is well along, and waits out of view before walking back.

### Coins and wage slot

Coins are a backpack item that stacks to 1000.
Purchases are paid from carried coins; hiring is paid from the bank first, then carried coins.

Sale proceeds land in the wage slot between the input and output boxes, which holds any number of coins.
Daily wages are paid from the wage slot first and then from your backpack.
The slot only holds coins, and you can move coins in or out by hand.

### Price sheet

The price sheet opens to the left of the panel and lists sell and buy prices with a Buy button for what the village sells:

| Goods | Sold by |
| --- | --- |
| Single steel tools | One |
| Canned meat stew | Crate of 12 |
| Filled bottle, empty bottle | One bottle |
| Wheat seeds, fertilizer | Crate of 4 |

The sheet only shows sale prices as last heard from the village and says how many days old they are.
It updates when a cart returns, or when a worker on Manage deliveries is sent to fetch fresh prices (a one-time errand down the road and back).
That worker also carries what the cart brings back, other than coins, to storage.
Errand runners only walk a short way down the valley before fading from view.

### Prices

A sale pays every unit at the price before it, then lowers the price by what it sold; a day restores one day's worth.
The village pays the full price for three crates of berries and two of each meat a day before food prices slowly slip.
Every steel tool sold lowers its price sharply, and about a crate of each a day keeps them steady ([docs/ECONOMY.md](docs/ECONOMY.md)).

Buying costs a fixed 1.25× the base price, so a buy-and-resell loop never profits.

## Workers

The Workers tab at the trading post offers several hiring deals: more coins up front means a lower daily wage.
Workers are individual entities with assignments, wages, cargo and visible work status.
There are at most forty workers.

A worker gathers as fast as the player does with the same tool, 4 pieces a time, until their hands hold 12, then carries the load off.
A worker gathering a resource moves on to the next nearest spot of the same kind once theirs is spent, such as the next tree after felling one, and goes on filling their hands there.
They only go to spots a player has seen, even if fog hides them now.
With no other spot known they say so and go on to their other jobs, or wait by a campfire.

### Worker page

Clicking a worker, or their card in the crew overview, opens their page.

- **Hands and tool.** The page shows what they carry, 12 items in all, and the tool they hold in a slot of its own.
  While you stand within reach, these slots work like a building's: left click picks a stack up, right click half, Shift or Ctrl sends it to the backpack.
  A stack put on their hands is theirs to carry to where it goes.
  The tool slot takes a tool, bucket or bottle.
  A worker returns what their jobs have no use for to a tool station.
- **Jobs.** Each job is a row with its number in the order, its place, what the place is set to make, mine, pack or grow, and a button to take the job away.
  Drag a row by its grip to move it up or down the order.
- **Haul groups.** "New haul group" adds an empty group at the end of the list.
  Drag haul jobs into it, or out again, by their grip.
  A group takes one place in the order, and its next load is picked by the [hauling](#hauling) rules across all its places.
  Its close button takes the group apart and leaves its hauls as jobs of their own.

The crew overview shows each worker's jobs as numbered chips in that order, with a haul group as one chip holding its places.

### Daily routine

- Workers break for an hour of lunch at noon.
  They take the closest open campfire seats they can reach, up to six workers per fire, and the fire burns while they sit at it, with no wood if it has none.
  Workers without a fire seat sit on the ground facing each other in groups of up to three.
  When the crew changes, a worker keeps their ground seat unless they take an open seat by a fire.
- As dusk falls they walk to the nearest trading post and head off down the valley like a trade cart, fading from view, then walk back in the same way at dawn.
- Assignments and anything they carry stay as they were.
- While they are away a forge cools and an anvil stops.

An unpaid worker strikes, standing with a "No pay - no work" sign, and owed wages stop growing until the strike is settled.

## Mines

A mine is marked for free from the building plans, which list what each of its stages takes and what the finished mine brings out.
The plan snaps to the nearest ore cliff, in front of the middle of its face between its two deposits, and shows a see-through green outline.
Each cliff takes one mine.

Clicking the mine shows its three stages:

1. A worker with a pickaxe digs the entrance in about one working day.
2. 50 wood, 10 iron bars and 50 stone go in, hauled from storage by assigned workers or delivered from the backpack.
3. A worker with a hammer fits the timber frame and the rails, which run into the dark tunnel, in another working day.

You can dig and fit too: hold right mouse on the mine with a pickaxe or a hammer in your backpack, and your work adds to the workers'.
The entrance is dug at the rock face beside the cave mouth, so you and the workers step up to it.

Workers take the pickaxe and hammer from a tool station.

Up to 3 workers can be assigned to Mine. They mine out of sight underground with pickaxes.
Build and Haul jobs do not use those slots. Assigning a worker to a mine with all three slots taken still gives them Build and Haul.
The cart goes underground with them, and the lantern on the frame lights the area like a torch.
When nobody is working, the cart stands outside, loaded once there is output.

Each miner brings out one ore and one rock every 1.6 seconds, about half what a worker gets from a deposit, but a mine never runs out.
Output fills two slots, ore and rocks, of one stack each.
When either is full, mining stops and a miner carries a load of 12 out to the nearest place that takes it.
With none, a message says the mine is full with no output available.

## Wolves and night

Five wolf dens, small caves at the foot of the mountains, each let one wolf out at dusk (20:24), so the hour or two before bed is risky too.
After that one den, picked at random, lets one more wolf out every hour until the wolves head home, so a night sees about thirteen.
They patrol the valley at a walk, each its own way, turning wherever the ground ahead is blocked; wolves walk only where you can.
Wolves are the same every night: one stands two blows of a steel axe and falls to the third or the fourth, and its fourth bite brings a healthy player down to half health.

A wolf that smells you within 30 metres while you are out in the open, but too far off to see you, turns to you where it stands, up to 1.3 seconds for a wolf walking away from you, then howls, and every patrolling wolf within 80 metres runs to the howl, ready to smell you on the way.
After its howl it closes in, and once it is near enough to see you outside firelight it breaks into a sprint.
A wolf that finds you already within sight goes for you without a howl.
A sprinting wolf gallops faster than you walk, but sprinting outruns it.
A wolf that reaches you stops at biting distance, about a metre off, and waits there without biting while another wolf tugs you and it has no arm of yours to take.

### Fighting

A swing keeps the direction you gave it at the start.
Right-click open ground with a weapon selected to aim at the pointer. A shovel still digs and a bug net still catches
firebugs with right-click. Press Space to attack with either tool, or with any other selected weapon.
Overhead, Space aims along the held movement keys or, from a stand, the last direction you walked.
In first person, Space aims where you look. With no weapon selected, Space swings the best axe in the backpack, or a
bare fist if you have no axe.
You can move during the swing, and near a hunting wolf the movement keys reverse you immediately.
Starting a doorway action interrupts a swing before it lands.
A steel axe lands 0.4 seconds into its swing and hits every wolf the blade reaches.
Axe swings take turns:

- the overhead chop comes down on a strip straight ahead, out to 1.35 metres;
- the sideways sweep goes from behind your left shoulder all the way round the front to your right at chest height, out to 1.3 metres.
  It passes over a wolf on the ground, and lands on every wolf in its way that is in the air, leaping for your throat, or reared up on someone's arm.

A steel axe takes 2.2 to 3.2 of a wolf's 8 health a blow, a torch 0.8 to 1.2 and a punch 0.4 to 0.6.
Each other weapon hits with its working end, such as a pickaxe's point or a hammer's face. The rake and Steel bucket
swing overhead and come straight down. The bucket takes longer to wind up and follow through, and clangs only on a hit.
When selected, chisels and hammers stay upright while standing or moving and tip forward to strike.
A miss and a late swing do not hurt the wolf.
A torch and a fist reach only a wolf at biting distance: the flame 1 metre, swung low with you bent over, and the fist 0.7 metres and a little to your right.
A wolf winces, hops sideways and skids if it survives a weapon blow on its first hit. With room, it runs wide and takes a
second hop behind you, lands on its forepaws and slides its hindquarters round to face you. Only then can it bite again.
In tight ground it recoils and growls. A struck wolf runs whenever it moves in the open, and later hits still make it reel.

A wolf at biting distance plants its paws and crouches for 0.3 seconds, then makes a short leap along that line.
Wolves leave room for packmates to land and run aside if they are too close to leap and blocked from backing up.
Its jaws close as it lands. Step sideways during the crouch or leap to get clear.
A blow that lands before the jaws close interrupts the bite.
A wolf that misses lands hard and stands for about half a second, giving you time to counter.
A wolf you have hit then runs in an arc away from you and back in for another bite.
In a cabin's doorway the same snap is how a wolf takes hold of you: only a bite that lands starts a tug.

An hour before dawn every wolf gives up and heads back to the nearest den.

### Fire

With fewer than five wolves nearby, wary hunters stop closing in when you reach a burning campfire and circle outside its light.
They go round the firelight to reach someone standing on the other side.
Like a torch swing, the fire stops a bite a wolf is still gathering for, but one already in the air finishes its leap.
Once five or more wolves are near the fire, they attack through its light just as they do against a torch.
A wolf that you hit or that has already turned on you ignores both kinds of light and keeps attacking, even on its own.
One log burns about 4 in-game hours.
A campfire with no wood left burns anyway while workers sit at it.
Cabin windows glow but do not deter wolves; a campfire whose light reaches a cabin's door keeps them off the door.

### Torch

A torch burns just as long as a log, once.
With a torch selected, the first right-click on open ground strikes flint and steel for 2 seconds to light it.
The lit torch shows in the left hand but stays in the backpack, and burning out uses it up; moving it out of the backpack, such as into a chest, puts it out, and it goes as a fresh torch.
After that each right-click on open ground swings it, which briefly widens its light by a fifth.

Swinging four times within 2 seconds, as fast as swings go, puts it out with a message.
Relighting takes the same 2 seconds and keeps the burn time it had left.

While it burns, a lone wolf that you have yet to hit still creeps in slowly. A swing toward it scares it into follow mode and stops a bite it was gathering itself for.
Every wave sends the wolves it reaches walking back out to their circle, however near they were, a second wave as much as a first.
Take any step toward one that is still backing off and it stops backing off: alone it runs, and among others it stands its ground, both as below.
Wolves in follow mode keep circling you at a walk at the edge of the light, about 4.5 metres off, each the way round it took at random; those going the same way spread evenly, and each wanders a little in and out and ahead and back, and turns its head to you now and then.
Walk away and they come after you at that walk.
Walk at one and it stops and stands braced where it is. Come within 2 metres and:

- a wolf with no other follower in your line of sight runs until it is out of your line of sight, or for 8 seconds, goes back to patrolling and leaves you alone until it has been out of smell range of you;
- with more than one following in your line of sight, the wolf you walked at stands its ground: it growls again and again from 3 metres, and at 2 metres every wolf in your line of sight turns on you.
  They then attack as if you had no torch, and no wave of the torch scares them again.

Once five are around you they attack together and swings no longer scare them.
A torch that lands on a wolf burns it and sends it running for a moment.

### Lantern

Finally a sustainable source of light.
The crafting bench makes an empty lantern.
Right-click a lantern in the backpack, on the hotbar or on the ground to open it.
Put firebugs onto its slot to add as many as fit, or right-click the slot while holding a stack to add one.
It takes up to ten, shortest lives first.
Putting a firebug in a lantern does not reset its life.
The jar glows from inside.
Its light dims as firebugs die, and you can add more at any time as long as there is room.
A full lantern is 20% brighter than a torch, with a green hue.
Lanterns do not scare wolves.
A green badge at the upper right of a lantern's icon shows how many live firebugs are inside.
Where a station shows several lanterns in one slot, the green number is for the lantern you can take next.

Double-click a lantern to take it in hand and put out any lit torch.
Double-click it again to stow it.
You can still select tools.
While your hands are needed for work, the lantern hangs from your belt and keeps shining.
Hotbar lanterns light the way even when they are not selected.
Slots 1–6 hang from your belt, slot 7 from your backpack, and slots 8 and 9 from your shoulders.
Held and worn lanterns are the same size and swing more as you run.
A lantern tucked away in the backpack or storage does not light the way, but its firebugs still age.
Open a carried lantern and choose **Put it down** to place it, or open a placed one and choose **Pick up**.

### Straw bed

“Not the best sleep score.”

Lay a straw bed on dry, open ground from Building plans for 20 leaves.
A bed holds one sleeper and doesn't shelter them from wolves.
Press W, A, S or D, or click Wake, to get up. You finish standing before you can walk away.
If you already have a burning torch, it stands beside the bed while you sleep.
It falls apart after three full in-game days, whether or not anyone slept in it.
Taking it down less than one in-game hour after building returns 20 leaves. After that it returns nothing.

### Cabins

A click on a cabin opens its page.
Double-click the cabin, or press Enter on its page, and you walk around buildings and other obstacles to its door.
Enter is highlighted while the game finds a path and while you walk.
Click it again or press Escape to cancel and move freely.
If there is no clear path, the walk stops.
At the door you step in and it shuts behind you.
Stepping in takes about 1.3 seconds at a walk, hurrying up the second tread, or 0.7 seconds and a slammed door if you sprinted there with Shift held.
For the first half of it you are still out in the open: a wolf that bites or leaps at you takes you by the waist and tugs you a metre back out in three tugs over 1.5 seconds, less where water or a building is in the way, you bent over, reaching for the door and screaming.
It then lets go and leaves you standing there, free to move: double-click the cabin to go for its door again.
At half health or less, its third tug takes you straight over onto your back about 1.2 seconds in.
Hit the wolf while it tugs someone and it lets go of them.
No other wolf takes you down while one tugs you.
While it tugs, one more wolf can take each of your sides, the side it comes from: it rears up, sets its forepaws on you and takes your forearm in its jaws and holds on until the tug ends, biting once: as it takes hold, or a second after its last bite if that was more recent.
A player a wolf takes out of a cabin has to be out of its door first, about 0.6 seconds into the tug.

You stay in until you press Step outside in its panel.
Stepping out takes about 1.5 seconds, and wolves can reach you for its second half, once you are out of the doorway.
At a cabin from an older save whose door spot is under water or a building, you step out onto open ground beside it.
Anyone knocking on the door steps aside to their right to let you out, and you keep to the left of the step; you step out beside anyone just standing at the door.
Once you are through the doorway, they squeeze past you in, in the order they came: both of you turn sideways, chest to chest, arms at your sides, and the one going in puts their right palm on the door frame.

A cabin holds two players.
While you are at its door, knocking, following or stepping in, anything you try in the world says "You are busy."; you can still pick a hotbar slot.
Someone who walks up behind a player stepping in follows them in once that player is inside.
At the door of a cabin that is full, or locked with someone inside, you wait for the door to swing shut, step up to it, knock on it three times and stand free.

#### The door's key

Once you are inside, the cabin's page shows the door's round lock with its key in it under the Step outside button.
Click the key to lock the door: it turns a quarter turn to the right with the sound of the lock and lies across the lock while the door is locked, and a click turns it back upright.
Either player inside can turn it.
A locked door keeps out other players, who knock, and wolves.
Step outside unlocks the door first and leaves it unlocked.

#### Wolves at the door

A wolf that was hunting you when you got inside comes to the cabin's door, and so does any wolf that passes within smell of a cabin with someone inside; neither howls.
A campfire lighting the door keeps them off it.
The wolf scratches at the door for 3.6 seconds, which you hear from inside.

- From a locked door it goes away, and so does a wolf with no way to a door; it leaves every cabin it gave up on alone until it has been out of smell of that one and comes back.
- You can still turn the key while it scratches.
- At an unlocked door it turns to the door's free edge, hooks its snout behind it and levers the door out in three pries over 1.5 seconds, the door rattling at each; the key no longer turns.
  It squeezes in between the door and its frame over 2.5 seconds, stands in the doorway growling at you with its fangs bared for 2 seconds and launches itself at you.
- It bites and takes you out by the waist, as a wolf tugging you out of the doorway does: at more than half health it lets go and leaves you standing outside.
- With two players inside it takes the one who came in last.
- Other wolves that come to the same door wait a few metres behind it and go for you once you are out, one catching each forearm and drawing it out to the side while the first wolf tugs.
- While a wolf is at the door, Step outside does nothing.
- A wolf at the door turns to a player out in the open within its sight until its snout is in the door, and a hit knocks it off the door.

From the moment the wolf pries until it has you, a window in the left third of the screen, three tenths of its width and 72% of its height, shows the door from inside the cabin, in the dark and as an old tape would: the door creeping open, the wolf's eyes, and its launch at you.
A player left inside goes on seeing the other one dragged out for a moment before the window closes.

### Wolf kills

At half health or less, away from fire and shelter, a nearby wolf crouches for a longer leap at your throat.
You can step clear of this leap too. If it catches you, the wolf pins you, the pack rushes in to feed and you die within seconds.
Wolves that kill you by a pin or a bite eat your food, but leave empty jam bottles in the recovery box. After a pin the pack keeps feeding until you respawn.

A slain wolf falls over and leaves a carcass that lasts until it is butchered: hold right mouse on it for 4 raw meat.

## Multiplayer

### Hosting and joining

Every game, alone or together, runs on a small server the game starts in the background on this computer.
Only this computer can join it until **Host** in the pause menu opens it to others on UDP port 24565, or the next free port if another game on this computer holds that one.
The pause menu then shows the port and the number of guests.
The world stands still while every player has a menu open, which alone means whenever you have one open.

**Join game** on the start menu, or **Join** in the pause menu, opens a popup with the port already filled in and the address of the last game joined. Join is unavailable while hosting, since it would drop the guests. Up to three guests can join.
Leaving your own game, by quitting, loading or joining another, ends it for everyone in it.
If joining another game fails or you cancel it, you go back to your own game, which waits on its server for 30 seconds. It is autosaved just before you try.
A second window on the same computer cannot join a game the first is playing in.

To find out why a join fails, look at the terminal the game runs in on both computers, or the `logs` folder beside the [saves](#saves).
They show each step of joining: the guest reaching the address, the server hearing it, and why it refuses or welcomes it.
No line saying the guest reached the address means nothing answered on that address and UDP port.

### Players in the save

The world's save keeps every player who has played in it (position, health, needs, backpack, hotbar and crafting queue), under a key each computer makes for itself the first time it plays.
Only the player who started the game saves and loads it, and the autosave runs every 5 minutes on the server.
A guest that comes back to the same world, even after the host loaded a save, finds its player as the save had it.

A new game's first player starts at the clearing with a steel axe, an empty bottle, 10 berries and 6 wood; a player who joins a world that has never seen them starts with the axe and the bottle.
A save follows its owner: whoever starts it on another computer plays the player who saved it.
Leaving returns a guest to the start menu.
A guest that loses the connection tries to get back in on its own for up to 120 seconds, with a countdown on screen.
**Stop · Esc** ends the tries, and when the time runs out the guest returns to the start menu.

### Shared world

Everyone shares the world, the market and the workers.
Time runs faster toward dawn when everyone in the game is asleep, in cabins, on straw beds, or both.
The clock waits for the last straw-bed sleeper to finish lying down before speeding up.
Workers, production, trade, needs and wolves all keep running in ordinary simulation steps.
Outside a locked cabin, a wolf within your usual sight radius triggers “You hear something” and returns time to normal speed, even in darkness.
You keep sleeping and healing. Time speeds up again when no sleeper hears a wolf.
A locked cabin prevents both the warning and the slowdown. Its sleepers still wake at dawn.
A wolf that starts scratching at an unlocked cabin door wakes the people sleeping there.
Wolves can also bite and wake an outdoor sleeper. If anyone wakes before dawn, time goes back to normal speed.
Night wolves hunt each player separately.

Thirty times a second the server sends each player only what changed since the last update that player confirmed, usually a few hundred bytes, so it plays over the internet as well as at home.

### Multiplayer limitations

- Actions on items and buildings wait one round trip to the server (walking does not); on your own computer that is a frame or two.
- The host's port must be reachable, as there is no address discovery or NAT traversal.
- Hiring is paid by whoever hires, while daily wages come from the trading post wage slot and then the backpack of the player who started the game.
- The server trusts what a player's position reports and computer key say, so play with people you trust.

## Saves

There are three save slots and a separate autosave the server writes every 5 minutes.
A save is written beside the one it replaces and takes its place only once it is whole, so a save that fails leaves the earlier one.
A save from another version of the game is refused, never converted.
F5 and F9 quick save and load to the current slot.

The launcher keeps its Godot user-data directory under `.tools/xdg-data`.
Launcher-managed saves are in `.tools/xdg-data/godot/app_userdata/Chop Chop Meow/`:

| File | Contents |
| --- | --- |
| `settlement.json` | Slot 1 |
| `settlement-2.json` | Slot 2 |
| `settlement-3.json` | Slot 3 |
| `autosave.json` | Autosave |

A git worktree whose `.tools` links to another checkout's keeps its own user data under `build/xdg-data` instead.
Each `./build.py play` there starts from a fresh copy of that checkout's saves and settings ([docs/RELEASE.md](docs/RELEASE.md)).
A manually exported or otherwise normally launched Godot build uses Godot's standard per-user application-data location instead.
A save that is incomplete, names items the game does not know or holds needs out of range is refused too; the game says why when loading fails.

## Look and sound

### Environment

The environment uses a shared human scale, Terrain3D mountain ranges, a rippled sand beach along the sea, and an irregular lake with a mossy, stony shore, reeds, lily pads and clustered shore stones.
Meadow grass and wildflowers grow in patches, ferns add woodland ground cover, and plain bushes stand among them.
Building footprints and mine approaches keep the ground cover clear, with worn soil around active work areas.
There are three tree types (oak, spruce, birch), from short trees to ones two thirds taller.

Workers wear blue bib overalls, cream shirts and yellow hard hats; the player wears a teal T-shirt, jeans and a daypack.
The sun crosses the sky from dawn to nightfall, so shadows swing round through the day and stretch out at either end, and its light warms near sunrise and sunset.
Daylight shadows meet the ground at boots, tree trunks and the feet of buildings.
The moon takes the same path at night and waxes and wanes over a cycle of `MoonCycleDays` (8): a full moon casts a faint blue light, while a new moon leaves only firelight, torches, lanterns and cabin windows to see by.
Night is lit by the moon, ambient light and real fire light from campfires, furnaces and cabin windows.
Trees in front of the player, a worker or a structure turn see-through.

### Animation

Player and worker actions use a jointed 3D rig with keyframed poses for idling, walking, jogging, sprinting, hand gathering, raking leaves, axe and pick strokes, digging with a shovel in both hands, hammering, packing crates, grinding with a mortar and pestle, bellows pumping, glassblowing, crate carrying, sitting at lunch (cross-legged on the ground or on a rock), knocking on a cabin door, being tugged back out of one and an unarmed punch.
Player tool actions follow the carried tool, while worker actions follow explicit transient work state rather than display text.
A tree is felled with strokes from the side into its trunk, while an axe swung in the air comes down from overhead.

The world reacts too:

- trees shake and topple over the stumps they leave, a tall one slower than a short one;
- a stump or a bush leans further out of the ground as it is dug at, then is thrown up out of a spray of soil and is gone;
- a berry bush thins out all around as its berries are picked;
- deposits shrink and throw chips, and crumble into chips and dust when their last piece is mined;
- a fire that runs out of work or fuel dies down over half a second;
- the bellows board travels with each press, and the heat gauge tracks the forge;
- a bar under the reinforced furnace and its panel dial mark the heat needed for the selected recipe;
- glass swells on the pipe and comes off as a clear jar when it succeeds, or sags, drops and splashes on the ground when it overheats;
- furnaces smoke (the reinforced furnace darker and thicker), and anvils spark;
- storage lids open on transfers, and the cabin door opens for someone stepping in or out and shuts behind them, or as far as a wolf forcing it has pushed it;
- the market mailbox flag rises while a cart is out;
- wolves trot, leap, feed and fall dead, bare their fangs when they growl or attack, scratch at a cabin's door, lever it open and squeeze in, and rear up onto the arm of a player another wolf tugs; a pinned player struggles on their back;
- couriers pull the trade cart.

Animations pause with gameplay.

### Sound and music

Sound effects follow the same actions:

- footsteps change on grass, rock, the lake shore and gravel paths;
- axes, pickaxes, anvils, benches and swings have impacts, and fitting a mine's frame clinks on its rails;
- a bench crushing rocks rings like a pickaxe on stone in place of its knock;
- a felled tree cracks as it breaks, a tall one tearing at length, and crashes as it reaches the ground;
- a rake rustles the leaves as its teeth pull them along the ground.
- a shovel crunches into the soil and throws it aside, and a stump or bush comes out with a heave of earth;
- picking plants by hand and packing crates rustle, butchering tears, and a mortar grinds;
- bellows puff;
- fires catch with a whoomp, including a lit torch and a furnace lighting a new batch, and a torch fizzles out;
- a building knocks into place, and one taken down clatters apart;
- buying jingles coins, and hiring signs the worker on;
- your stomach growls when food runs low;
- wolves howl when they smell you, growl as they break into a sprint, growl again as they close in, bite, and thud when struck;
- a key turns in a cabin's lock, a wolf's claws scratch at its door, and the door rattles at each pry;
- you scream as a wolf tugs you out of a cabin's doorway, and when a wolf takes you down.

Repeated sounds rotate between variants.
A sound is loudest at your character and fades with distance, to silence 16 metres away, whatever the zoom.
A wolf's growl as it breaks into its sprint carries 30 metres, and a howl 80.

Day and night each have their own music playlist.
The music crossfades when the time changes, and each playlist picks up where it left off and wraps after its last track.
The soundtrack gives way to a campfire song as you approach and comes back as you leave or the song ends.
The campfire musicians and listeners stay in time with the recording even in a long-running settlement.
When play pauses, so does the campfire song, and the soundtrack returns until play resumes.
The soundtrack also plays in place of the campfire recordings while sleep speeds up time.
The music volume setting also controls campfire songs and solo sounds.

## Limitations

- Worker automation is limited to the jobs in the [job table](#job-buttons); workers do not hunt, cook by hand, place buildings or trade.
- Workers use simple grid detours around water and buildings; this is not a full navigation system.
- There is no pollution, vampire threat, or technology beyond steel in this POC.

## Licensing

The code is licensed under the GNU GPL v3; see [LICENSE](LICENSE).

Sound credits:

- Kenney ([www.kenney.nl](https://www.kenney.nl))
- Little Robot Sound Factory ([www.littlerobotsoundfactory.com](https://www.littlerobotsoundfactory.com))
- jimmybombimmy for [Pushing Leaves](https://freesound.org/people/jimmybombimmy/sounds/368022/)
- kokoreli777, thellywellyn and freesound_community on [Pixabay](https://pixabay.com/sound-effects/), for the grass
  and gravel footsteps, the digging, the stomach growl and the intro film's train and office recordings
  ([assets/README.md](assets/README.md#intro-film))

Music credits:

- Alexandr Zhelanov ([soundcloud.com/alexandr-zhelanov](https://soundcloud.com/alexandr-zhelanov))
- Augmentality (Brandon Morris)
- [Jazz - Jazz Music](https://pixabay.com/music/traditional-jazz-jazz-jazz-music-630211/) by Andrii Hroza (andriih)
- [Jazz Bar](https://pixabay.com/music/traditional-jazz-jazz-bar-592657/) by Aurec
- [Lofi Soul](https://pixabay.com/music/lofi-lofi-soul-588903/) by ZephiraMusic
- [Groove Soul](https://pixabay.com/music/funk-groove-soul-317798/) by The_Mountain
- [Soft Jazz](https://pixabay.com/music/smooth-jazz-soft-jazz-621606/) by AtlasAudio
