---
name: modeling
description: Build, change, or review 3D models and their in-game icons for Chop Chop Meow in Godot.
  Use for props, tools, buildings, creatures, and objects fitted to another model or held by a character.
---

# Modeling

1. Read the [asset guide](../../../assets/README.md#3d-models) for art direction, world scale, and triangle ceilings.
   Find the existing builder or the closest comparable shape through the guide.
   When fitting a new part to an existing model, start from a render of that model and check the builder's dimensions.
2. When you need concept images, use ImageGen if available, otherwise look up references online.
   Keep the source links for references found online.
   Show the same design from the front, side, and three-quarter view when shape or fit depends on the angle.
   Mark the scale, materials, and points that have to line up, such as grips, mounting feet, and openings.
   For a roof-mounted barrel, use the cabin render to show the mount footprint and roof clearance.
   For a bucket, show where the hand grips its handle.
3. Build the model with Godot meshes in the existing shape builder or alongside related shapes.
   Reuse [ModelKit](../../../src/ModelKit.cs) primitives and materials and share meshes across uses when the shape is
   the same.
   For a prop visible in several states, give moving or optional parts their own nodes.
   Keep each assembled model within its triangle ceiling, including visible attachments.
   Use [ModelSteps.Triangles](../../../tests/ModelSteps.cs) when checking the cost of a built model or heap.
   Register world models through [WorldModels](../../../src/WorldModels.cs) and use its warm-up path for new
   meshes and materials that would otherwise first draw during play.
4. Render a game-camera screenshot with `./build.py play -- --showcase --screenshot /absolute/path.png`.
   Check the silhouette at play distance and use a close view to check fit, clearances, and contact points.
   Keep the camera fixed for comparisons.
   When matching a side-on reference photo, compare to a side-on orthographic render at the same scale.
   Render item and building icons from the models with `./build.py icons` and inspect the affected icons at hotbar
   size.
   Follow [AGENTS.md](../../../AGENTS.md#working-rules) for icon regeneration.

For moving parts or a character using a prop, use the [animation skill](../animation/SKILL.md).
If a changed model appears in the intro, follow the [intro-film skill](../intro-film/SKILL.md).
