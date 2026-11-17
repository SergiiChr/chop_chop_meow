---
name: animation
description: Make a character or creature animation look physically right, check every frame of it and show a before/after video. Use when changing or reviewing any motion: a walk, run, work action, idle, wolf motion, or bodies meeting each other or the scene.
---

# Animation

## Workflow

1. Record the clip as it is now, before changing it (see [Review](#review)).
2. Find references for the motion and write down its key poses and timings: contact, mid-stance, push-off, mid-swing,
   how long a foot is down, how long the body is in the air.
3. Work out the geometry first: leg reach, foot placement, hip height.
   A short script in the scratchpad that solves the poses is quicker to iterate than the game.
4. Change the poses, record again and go through it as [Frame by frame](#frame-by-frame) says, until every frame passes.
5. Add or extend a check in `tests/AnimationChecks.cs` for what can be measured: feet on the ground, no sliding, nothing
   below it, time in the air. `CheckWolfGaitsPlantEachPaw` and
   `CheckEveryGaitPlantsItsFeetAndLeavesTheGroundOnlyWhenRunning` show how.
6. Play it in the real window with the `playtest` skill: speeds, blends into and out of the motion, and turns.
7. Share a before/after video made with the `frame-compare` skill, only once every frame passes.

## Frame by frame

Every new or changed motion is checked on every frame before anyone is shown it.
A frame that fails sends the motion back for another pass, as many passes as it takes.

- Record at a fixed frame rate with `./build.py record` (see [Review](#review)), so a frame number is the same moment in
  every recording, before and after a change.
  When recording is slow or unreliable, fix the tooling first, then carry on.
- Cover every scenario the motion plays in, not only the one asked about: each way into and out of it, one player and
  two, with and without wolves, on flat ground and on steps.
- Look at every frame of the new motion, not samples, from more than one side: the game camera, from above for where
  bodies stand, and low from the side for heights.
  Two bodies that overlap in one view may still be apart; judge from a view where they would show apart.
- Every frame must pass all of these:
  - Nothing clips through anything.
  - Physiologically possible poses.
  - The right part touches the target: feet touch the ground and steps, a palm touches the target described, a wolf's
    jaws touch the bite target.
  - Smooth motion: no jumps between frames; every animation is evenly filled with intermediate motion.
  - Each body acts on the other where that one is drawn: a leap lands on the player as shown, not on where the world
    keeps their place.
- What can be measured, such as a sole's height over the tread under it or the gap between two bodies, is measured on
  every frame of the real recording with a temporary print, and kept as a check where a seam allows.
- Show the motion only when every frame of it passes. Say what was checked and from which views, and name anything still
  failing as open.

## References

Start from these, and search freely for more: photos and slow-motion footage of the exact motion beat any list.

- Eadweard Muybridge, *Animal Locomotion* (1887): photo plates of people walking, running and working, and of dogs,
  horses and other animals, on Wikimedia Commons.
- Richard Williams, *The Animator's Survival Kit*: key poses and timing for walks and runs.
- Biomechanics papers give joint angles and timings, such as Novacheck, "The biomechanics of running" (Gait & Posture,
  1998).
- Slow-motion sports footage, for sprints, jumps, swings and throws.
- Anime and other stylised animation show which poses read well, not physics: Sakugabooru clips can be stepped through
  frame by frame.

## Any body

- **Gravity:** in the air the body's centre follows a parabola: a flight of `t` seconds rises and falls `g·t²/8`, and
  lands at `g·t/2`.
- **Contact:** a foot or paw on the ground does not slide. Tie the cycle to the distance moved, not to time, so stride
  length always matches speed.
- **Ground:** nothing sinks into it, and whatever bears weight touches it.
- **Timing:** in a walk each foot is down more than half the cycle, so both are down at once for a while. In a run each
  foot is down less than half, leaving time in the air: about a quarter of a second for a jog, a tenth for a sprint.
- **Bounce:** a walk vaults over a stiff leg, highest over the planted foot. A run compresses like a spring, lowest over
  the planted foot.
- **Balance:** standing still, the body's centre stays over the feet. Speeding up, it leans ahead of them by about the
  angle of acceleration against gravity.
- **Opposition:** parts swing against each other so the body does not spin: arms against legs, shoulders against hips.
- **Smoothness:** joint angles and their speed carry on without a jump from one phase to the next. Join keyed curves
  with matched slopes, not straight lines.
- **Overlap:** loose parts such as hair, tails, bags and ears follow the body a moment late, then settle.

## Legs and feet

- The foot lands a little ahead of the body: about a third of a metre ahead of the hips in a sprint, closer at slower
  speeds. A straight leg landing far ahead looks like braking.
- The knee bends a little on landing and more under the weight: about 20° to 40° in a run, less in a walk.
- To push off, the heel lifts and the foot rolls onto its ball, the ankle pointing 20–30°. The leg extends behind, but
  the knee never locks.
- In the swing, the heel folds up toward the hip, more the faster the run. The knee drives forward and up, then the shin
  reaches out and sweeps back just before landing.
- Running feet land close to one line; walking feet a little apart.

## Arms

- Each arm swings with the opposite leg, more the faster the motion.
- A running elbow stays bent about 90°: tighter in front with the hand rising toward the chin, more open behind. Hands
  barely cross the body's middle.
- Time the arms from the legs' phase, not their own clock, so the two never drift apart.
- Work actions such as chopping move the whole body: the weight shifts from the back foot to the front as the tool comes
  down.
- A hand held close to its own shoulder or at the middle of the chest folds the arm into the torso, since an elbow folds
  only forward. Keep work hands out in front or to their own side, and look at the `facing` view: a buried arm does not
  show from the side.

## Trunk and head

- The trunk leans into a run, about 5–15° at a steady speed and more while speeding up, and tips a little over each
  planted foot.
- The shoulders turn against the hips. In a walk the hip on the swinging side drops a few degrees.
- The head keeps the eyes level: it tilts against the trunk's lean and its bounce.

## Four legs and tails

- A walk sets each foot down in turn, a trot moves diagonal pairs together, and a gallop lands the feet in order with
  time in the air. Wolves trot for distance and gallop to chase.
- In a gallop the spine flexes to gather the legs under the body and extends to stretch them out.
- The head bobs to balance the front legs. The tail trails the body and swings out to balance turns.
- The contact, ground and timing rules above apply to every paw.

## Two bodies meeting

- Place one body from the other's contact point, not from a tuned distance. A leaping wolf is moved so `WolfModel.Bite`
  lands on `WorldView.Throat`, which follows the knocked-down player's tip, so the jaws stay on the throat as both go down.
- Time a reaction to the moment of contact, not the start of the action: a struck wolf recoils at
  `ActorAnimation.Contact`, when the swing lands.
- A body moves across the ground only while its feet are planted and stepping, or while it is in the air. A knockback
  played as a slide reads as ice: cover it with a hop or a throw, and keep the distance short.
- Check proportions against a side-on reference photo: render the model side-on and orthographic at the photo's scale and
  put the two side by side.
- Look at anything that glows, such as eyes, in a dark scene as well.

## This game's rigs

- People: `Pose` in `src/ActorAnimation.cs` lists joint angles, and `src/ActorRig.cs` applies them to hips, torso, head,
  shoulders, elbows, thighs, knees and ankles. [docs/DESIGN.md](../../../docs/DESIGN.md) describes the walk, jog and
  sprint.
- To put a hand on a point, use `ActorAnimation.ArmTo`: it gives the shoulder and elbow angles for a fist, or for what
  the fist holds, at a point in the trunk's frame. Grinding places both arms with it. The intro film's `Reaching` in
  `src/intro/WorldIntro.cs` wraps it for a point in the rig's own space. Do not write another arm solver.
- Wolves: `src/WolfShape.cs` builds one skinned mesh on a skeleton, and `WolfModel` in `src/WolfModel.cs` poses it from
  a `WolfPose` of spine, neck, head, jaw, ears, tail and leg angles. Gait legs are solved by IK from where each paw stands
  (`Reach`), and `WolfModel.Paw` and `WolfModel.Bite` give where a paw and the jaws are under the pose shown.
  `SyncWolves` in `src/WorldView.cs` times the wolf against the player.
- The pelvis is part of the torso model, so it cannot turn or drop on its own.
- Only the walk, jog and sprint set the ankle. Other actions leave the boot square to the shin, so toes can dip into the
  ground at a steep shin.
- The walk's, jog's and sprint's clocks move with distance through `ActorAnimation.Advance`, each `Stride` matched to
  the speed it is timed for: a change of a worker's, walking or sprint speed fails a check until the gait is retimed.
- A gait's planted leg is solved from where its foot touches the ground, and its swinging leg is keyed. A key change can
  push a toe through the ground: the gait check in `tests/AnimationChecks.cs` catches that.

## Review

`tools/animation_review.py` records one clip close up, moving over marked ground, from the side, the front, the game
camera and the game camera with the actor facing it (`facing`):

```sh
./tools/animation_review.py "player run"                       # all four views at 4x slow motion
git fetch origin
./tools/animation_review.py "player run" --base origin/main --views side --slow 1  # add the same views from main
```

- Movies land in `build/animation-review/<clip>/<current or ref>/<view>.avi`, 96 frames a view at 30 a second of
  slowed game time. Recordings of the same sources match frame for frame, and so do a branch and its base; anything
  that varies by run, such as an object's hash code, breaks that, so key motion off saved ids instead.
- The first frames show the start from standing: pick frames after it for a steady cycle.
- `--base` records a ref as git has it here: fetch first and use `origin/main`, since a local `main` may be old.
  A ref without the close-up review (`--animation-clip`) cannot be recorded.
- `--base-clip` names the clip in `--base` when it was renamed since, such as `--base-clip "player walk"` for
  `"player jog"`; both land in the current clip's folder.
- Wolves have close-ups too: `wolf idle`, `wolf walk`, `wolf trot`, `wolf gallop`, `wolf speeds` (stand to gallop and
  back, for blends), `wolf leap`, `wolf hit`, `wolf feed` and `wolf fall`. The last four play against a player, as
  `SyncWolves` sets them up. `./build.py play -- --animation-review` shows every clip on one screen.
- A machine without a display, such as a cloud container, records under `xvfb-run -a ./tools/animation_review.py ...`.
- Turn the movies into a side-by-side video or a numbered contact sheet with the `frame-compare` skill, which takes
  them as they are.

Any scene records the same way, such as a showcase, with `./build.py record`:

```sh
./build.py record build/recordings/queue.avi -- --showcase --hide-ui --doorway queue --zoom 5 --frames 120
./build.py record build/recordings/queue-slow.avi -- --showcase --hide-ui --doorway queue --slow 4 --frames 480
```

- Each frame moves the game on by exactly 1/30 of a second (`--fps` sets another rate), however long it takes to draw,
  and `--slow` slows game time. The movie carries the game's sound too.
- That holds for scenes whose world runs inside the window: `--showcase`, its `--doorway` scenes and
  `--animation-review`. A normal game's world runs in its own server process on real time, so a recording of one
  neither repeats frame for frame nor slows down.
- A recording leaves the mouse pointer out: wherever it sits on the desktop, nothing in the world shows as hovered.
- The first recording after a change exports the game; later ones reuse that export while the sources stay the same,
  and take seconds. Doc and tool edits do not count as changes.
- Record into `build/`, not a temporary folder: frames of many recordings fill a small `/tmp` fast.
