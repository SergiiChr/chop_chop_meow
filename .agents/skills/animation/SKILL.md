---
name: animation
description: Animate players, workers, or creatures in Chop Chop Meow and check the Godot render frame by frame.
  Use for walks, work actions, idle motion, and contact with a body, prop, or part of the scene.
---

# Animation

## Plan the motion

- **Existing motion.** Record the current clip before editing, with the same start conditions and views you will use
  for the new recording.
- **New motion.** Make a numbered pose sheet from the start pose through the contact, weight shifts, release, and
  return to the next action.
  Mark the time or frame for each pose and what each hand, foot, and prop touches.
  For a ladder climb and pour, show the rung contacts, the step onto the roof, the vessel tipped over its target,
  and the way back down.
- For illustrated pose references, use ImageGen if available, otherwise look up references online.
  Keep source links for references found online.
  Check contact and timing against real photo sequences or slow-motion footage of the action.
  If a prop still needs to be built or fitted, use the [modeling skill](../modeling/SKILL.md) for its dimensions.

## Pose the rig

- Work out geometry first, such as limb reach, support points, and hip height.
  Use the point on the model the hand, foot, or prop must touch to set its pose.
  When two bodies meet, position one from the actual contact point on the other.
  Time reactions from contact.
  Shift weight through a work action, such as from the back foot to the front as a chopping tool comes down.
  A body moves across the ground only while stepping or in the air.
  Sliding after a knockback looks like sliding on ice.
  Keep a knockback short and show it as a hop or a throw.
- Human poses are in [ActorAnimation.cs](../../../src/ActorAnimation.cs) and are applied by
  [ActorRig](../../../src/actors/display/ActorRig.cs).
  Use the existing arm solver [ArmReach.ArmTo](../../../src/actors/display/ArmReach.cs) for every reach in the
  trunk's frame.
  For a point in the intro rig's own space, use `Reaching` in [WorldIntro.cs](../../../src/intro/WorldIntro.cs).
  Keep work hands forward or to their own side so elbows can bend without pushing the arms into the torso.
  Check grips from the front as well as the side.
  Check feet at steep shin angles.
  A pose that leaves the ankle at its default keeps the boot square to the shin.
- Take left and right from [BodySide](../../../src/actors/model/BodySide.cs).
  Set angles and their speed so they carry smoothly between poses.
- Read [Locomotion](references/locomotion.md) for gait timing, foot placement, and the movement clock.
  Read [Wolves and cabin doors](references/wolves-and-cabin-doors.md) for wolf poses, contact with another body, and
  recordings at a cabin's door.

## Review the Godot render

1. Record at a fixed frame rate.
   Fix slow or unreliable recording tools before continuing.
   Review every frame of the changed motion in the game-camera view and from the angles that expose its contacts.
   Use overhead for placement, low side for height, and facing for grip.
   Two bodies that overlap in one view may still be apart.
   Check from a view where the gap would show.
   Cover every scenario the motion plays in, including each way into and out of it.
   Cover one player and two, with and without wolves, on flat ground and on steps wherever the motion plays there.
2. Check for clipping, unreachable poses, missed or sliding contacts, and jumps in the motion.
   Check contacts at the bodies and props as drawn, using their posed positions.
   Use a temporary print to measure contacts on every frame of the recording where you can, such as a boot's height
   over a tread.
   Add or extend a check for measurable rules in the feature's tests.
3. Use the [playtest skill](../playtest/SKILL.md) to check the action in the real window, including its speeds, turns,
   and transitions that apply.
4. Use the shared `frame-compare` skill to review numbered contact sheets and share the finished recording.
   Share an aligned before/after video when there is a baseline.
   Share the finished motion once every frame passes, and report the views checked and any problems still open.

## Record

```sh
./tools/animation_review.py "player run"
./tools/animation_review.py "player run" --views game side top --frames 240
git fetch origin
./tools/animation_review.py "player run" --base origin/main --views side --slow 1
```

- The default views are `side`, `front`, `game`, and `facing`.
  Select `top` in `--views` for an 89-degree view from overhead.
  The default is 96 frames per view at 30 frames a second and 4× slow motion.
  Set `--frames` for a longer action.
  At 4× slow motion, record 120 frames for each second of game time.
- Movies go in `build/animation-review/<clip>/<current or ref>/<view>.avi`.
  Fetch origin before comparing with `origin/main` so the recording uses the current remote base.
  The base must support close-up review and contain the action.
  The base uses its own launcher and may have a shorter recording limit.
  When it has a different name there, select it with `--base-clip`.
  Matching recordings use the same view, frame count, and slow-motion rate.
  Key any variation in motion off saved IDs so repeated recordings of the same action stay aligned.
- For a close crop use `--animation-zoom` in a direct `./build.py record` command.
  Use `xvfb-run -a` on a machine without a display.
  The first recording after a game source change exports the game.
  Later recordings reuse that export while the game sources match.
  Save recordings in `build/`, where there is space for large movies.
  If space in `/tmp` is tight, set `TMPDIR` to an existing directory on disk for the base checkout.

Use the shared `frame-compare` skill to make small sheets that show every frame.
Advance the starting frame by 12 for each sheet.
The close-up recordings start from standing, so use the first frames to check the transition into a cycle.

```sh
~/projects/agents/skills/frame-compare/compare.py build/run-000.png \
    build/animation-review/player-run/current/top.avi --first 0 --count 12 --step 1 --numbers --columns 4 --scale .5
```

For an action that depends on the world around it, record its showcase scene directly.

```sh
./build.py record build/recordings/queue.avi -- --showcase --hide-ui --doorway queue --zoom 5 --frames 120
```

`--slow` slows game time and `--fps` on `./build.py record` sets the capture rate.
Showcase and animation review run the world in the window, so their frames advance on a fixed clock.
A normal game uses a separate server that keeps running in real time with `--slow`.
Its recordings vary from frame to frame between takes.
The recording excludes the mouse pointer and includes the game's sound.
