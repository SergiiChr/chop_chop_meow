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
  Time reactions from contact.
  Shift weight through a work action, such as from the back foot to the front as a chopping tool comes down.
- Human poses are in [ActorAnimation.cs](../../../src/ActorAnimation.cs) and are applied by
  [ActorRig](../../../src/actors/display/ActorRig.cs).
  Use [ArmReach.ArmTo](../../../src/actors/display/ArmReach.cs) to reach a point in the trunk's frame.
  For a point in the intro rig's own space, use `Reaching` in [WorldIntro.cs](../../../src/intro/WorldIntro.cs).
  Keep work hands forward or to their own side so elbows can bend without pushing the arms into the torso.
  Check grips from the front as well as the side.
  Check feet at steep shin angles. A pose that leaves the ankle at its default keeps the boot square to the shin.
- Take left and right from [BodySide](../../../src/actors/model/BodySide.cs).
  Set angles and their speed so they carry smoothly between poses.
- Read [Locomotion](references/locomotion.md) for gait timing, foot placement, and the movement clock.
  Read [Wolves and cabin doors](references/wolves-and-cabin-doors.md) for wolf poses, contact with another body, and
  recordings at a cabin's door.

## Review the Godot render

1. Record at a fixed frame rate. Review every frame of the changed motion in the game-camera view and from the angles
   that expose its contacts. Use overhead for placement, low side for height, and facing for grip or overlap.
   Cover the entrances and exits of the action and the game conditions that change its pose or contacts.
2. Check for clipping, unreachable poses, missed or sliding contacts, and jumps in the motion.
   Check contacts at the bodies and props as drawn, using their posed positions.
   Measure contacts on every frame where you can, such as a boot's height over a tread.
   Add or extend a check in [AnimationChecks.cs](../../../tests/AnimationChecks.cs) for measurable rules.
3. Use the [playtest skill](../playtest/SKILL.md) to check the action in the real window, including its speeds, turns,
   and transitions that apply.
4. Use the shared `frame-compare` skill to review numbered contact sheets and share the finished recording.
   Share an aligned before/after video when there is a baseline.
   Share the finished motion once every frame passes, and report the views checked and any problems still open.

## Record

```sh
./tools/animation_review.py "player run"
./tools/animation_review.py "player run" --views game side top --frames 240
./tools/animation_review.py "player run" --base origin/main --views side --slow 1
```

- The default views are `side`, `front`, `game`, and `facing`.
  Select `top` in `--views` for an 89-degree view from overhead.
  The default is 96 frames per view at 30 frames a second and 4× slow motion.
  Set `--frames` for a longer action.
  At 4× slow motion, record 120 frames for each second of game time.
- Movies go in `build/animation-review/<clip>/<current or ref>/<view>.avi`.
  For a comparison, use an up-to-date Git ref that supports close-up review and contains the action.
  When it has a different name there, select it with `--base-clip`.
  Matching recordings use the same view, frame count, and slow-motion rate.
  Key any variation in motion off saved IDs so repeated recordings of the same action stay aligned.
- For a close crop use `--animation-zoom` in a direct `./build.py record` command.
  Use `xvfb-run -a` on a machine without a display.
  Save recordings in `build/`, where there is space for large movies.
- To review every frame in manageable sheets, use `frame-compare` with `--numbers --step 1` and advance `--first` by
  `--count` for each sheet.
  The close-up recordings start from standing and the first frames can be used to check the transition into a cycle.

For an action that depends on the world around it, record its showcase scene directly.

```sh
./build.py record build/recordings/queue.avi -- --showcase --hide-ui --doorway queue --zoom 5 --frames 120
```

`--slow` slows game time and `--fps` on `./build.py record` sets the capture rate.
Showcase and animation review run the world in the window, so their frames advance on a fixed clock.
A normal game uses a separate server on real time.
The recording excludes the mouse pointer and includes the game's sound.
