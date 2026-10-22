# 3D models instead of sprites

## Summary

The world is now a 3D scene under an orthographic isometric camera, and every object in the POC is a low-poly model built in code from Godot primitive meshes. There are no world or character sprite sheets left to maintain; changing a model means editing its builder in `src/WorldModels.cs` or `src/ActorRig.cs`.

- **Look:** natural woodland, no fantasy styling. Buildings are modern DIY: cinder-block furnace with a stovepipe, propane-tank forge on an angle-iron stand with a plywood lever bellows and heat gauge, OSB cabin with a corrugated shed roof and solar panel, plywood storage box, orange recovery tote, anvil on a welded stand, market post with a mailbox, wheel-rim fire pit. The trade cart is a garden cart pulled by a courier in a hi-vis vest.
- **Trees:** three variants (oak, spruce, birch), chosen by object id with size and rotation jitter, so saves need no new fields.
- **Actors:** one jointed rig for player, worker and courier. The player wears a teal T-shirt, jeans and a daypack; workers keep blue bib overalls over cream shirts and add yellow hard hats.
- **Animations:** keyframed poses for idle, walk, hand gathering, iron and steel axe and pick strokes, hammering, bellows pumping, crate carrying and the punch, with the matching tool in hand. World objects react: trees shake and topple into stumps, deposits shrink and throw chips, bushes lose their berries, the bellows board follows each press, the heat gauge tracks the forge, anvils spark, storage lids open on transfers, the cabin door opens as you approach, the mailbox flag rises while a cart is out, wolves trot, and fires, furnaces and chimneys flicker and smoke.
- **Lighting:** sun, ambient and real fire light from campfires, furnaces and cabin windows replace the old night overlay.
- **Icons:** hotbar, building-plan and toolbar icons are unchanged.

Picking, hover rings and canopy fading use the same per-kind hit cylinders, so selection matches what is drawn. Fading swaps in cached alpha-blended material copies because the Compatibility renderer ignores per-instance transparency.

## Small game-facing changes

- The hut is now called **Cabin** in data and help text, to match the new model.
- When you hold right mouse on a resource, the player now walks to 1.35 tiles instead of stopping at 2.5, so tools visibly reach the model. Gather range, timing and yields are unchanged; the pond keeps its wider reach.

## Removed

The sprite atlases, sixteen baked animation sheets, `data/animations.json`, the offline baker, its Pillow requirement and art sources. The exported pack drops from 18.7 MB to 2.4 MB.

## Checks

`./build.py lint`, `./build.py test` (`SELF-TEST PASS`, `UI-TEST PASS`) and `./build.py build` with the exported smoke test pass. Animation checks now cover loop timing, facing, seamless loops and at least six distinct poses per action. Visual review used rendered captures of the fresh start, the daylight and night showcase, close-ups of every model, the animation review, and chopping, gathering and pumping during a rendered UI run. Screenshots in `docs/screenshots/` come from the exported executable; the settlement shots use the showcase fixture, not a played-through game.
