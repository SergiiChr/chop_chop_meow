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
- Human poses are in [ActorAnimation.cs](../../../src/actors/display/ActorAnimation.cs) and are applied by
  [ActorRig](../../../src/actors/display/ActorRig.cs).
  Use [ArmReach.ArmTo](../../../src/actors/display/ArmReach.cs) for all arm reach calculations.
  It takes a target point in the trunk's frame.
  For a point in the intro rig's own space, use `Reaching` in [WorldIntro.cs](../../../src/intro/WorldIntro.cs).
  Keep work hands forward or to their own side so elbows can bend without pushing the arms into the torso.
  Check grips from the front as well as the side.
  Check feet at steep shin angles.
  Work done in place gives only a hip height: `ActorAnimation.FeetOf` names where its boots stand, and `OnFeet` bends
  the legs to keep them there, flat, with a heel lifting only past 30° of ankle bend.
  Do not key leg angles for work: a keyed leg sinks or floats a boot as the hips move.
- Take left and right from [BodySide](../../../src/actors/model/BodySide.cs).
  Set angles and their speed so they carry smoothly between poses.
- Read [Locomotion](references/locomotion.md) for gait timing, foot placement, and the movement clock.
  Read [Wolves and cabin doors](references/wolves-and-cabin-doors.md) for wolf poses, contact with another body, and
  recordings at a cabin's door.
  Read [Blows and things that move](references/blows-and-moving-things.md) for a tool landing on what it works, and for
  falls, throws, doors and other moving things.

## Review the Godot render

1. Record at a fixed frame rate.
   Make every motion for 60 frames a second, the rate the game is drawn at, and judge it at that rate and real speed.
   Neighbouring frames join without a jump, and a fast stroke still has frames between its key poses.
   Slow motion is for looking closer, not for passing a motion.
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
./tools/animation_review.py "player run" --base origin/main --views side --slow 4
```

- The default views are `side`, `front`, `game`, and `facing`.
  Select `top` in `--views` for an 89-degree view from overhead.
  The default is 120 frames per view at 60 frames a second and real speed: two seconds of game time.
  Set `--frames` for a longer action and `--slow` to look closer.
  At 4× slow motion, record 240 frames for each second of game time.
- Movies go in `build/animation-review/<clip>/<current or ref>/<view>.avi`.
  Fetch origin before comparing with `origin/main` so the recording uses the current remote base.
  The base must support close-up review and contain the action.
  The base uses its own launcher and may have a shorter recording limit.
  A base from before the rate could be set records 30 frames a second over the same game time, so its frames fall
  between the branch's.
  If a base recording fails, its build logs are kept beside the view logs.
  When it has a different name there, select it with `--base-clip`.
  Matching recordings use the same view, frame count, and slow-motion rate.
  Key any variation in motion off saved IDs so repeated recordings of the same action stay aligned.
  Keep the mouse off the recording window, since the wheel zooms the view.
  Record the base twice before blaming the branch for a difference.
  Compare frames by the largest change of any one pixel and the count of pixels changed by more than about 32 of 255.
  Scattered small changes come from compression or a reordered sum; an edge that moves shows as large ones.
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
./build.py record build/recordings/queue.avi -- --showcase --hide-ui --doorway queue --zoom 5 --frames 240
./build.py record build/recordings/queue-slow.avi -- --showcase --hide-ui --doorway queue --slow 4 --frames 960
```

Each frame moves the game on by exactly 1/60 of a second, however long it takes to draw.
`--fps` on `./build.py record` sets another capture rate, and `--slow` slows game time.
Showcase and animation review run the world in the window, so their frames advance on a fixed clock.
A normal game uses a separate server that keeps running in real time with `--slow`.
Its recordings vary from frame to frame between takes.
The recording excludes the mouse pointer and includes the game's sound.
