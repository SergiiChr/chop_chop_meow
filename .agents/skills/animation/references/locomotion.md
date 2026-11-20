# Locomotion

Use these notes when changing a walk, jog, sprint, or the transitions between them.

## Motion references

- Eadweard Muybridge's *Animal Locomotion* photo plates on Wikimedia Commons show people and animals in motion.
- Richard Williams's *The Animator's Survival Kit* lays out key poses and timing for walks and runs.
- Slow-motion sports footage and biomechanics papers show joint angles and timing.

## Any body

- In the air the body's centre follows a parabola.
  A flight of `t` seconds rises and falls `g·t²/8` and lands at `g·t/2`.
- A foot or paw stays in place while planted.
  Tie the cycle to distance travelled so stride length matches speed.
- Keep the body above ground and the parts that bear weight in contact with it.
- In a walk each foot is down for more than half the cycle, so both are down at once for a while.
  In a run each foot is down for less than half the cycle.
  A jog spends about a quarter of a second in the air, and a sprint about a tenth.
- A walk vaults over a stiff leg and is highest over the planted foot.
  A run compresses like a spring and is lowest over the planted foot.
- When standing still, the body's centre stays over the feet.
  As the body speeds up, it leans forward by about the angle of acceleration against gravity.
- Swing arms against legs and shoulders against hips to keep the body from spinning.
- Keep joint angles and their speed smooth between phases.
  Match the slopes where the keyed curves join.
- Loose parts such as hair, tails, bags and ears follow the body a moment late, then settle.

## Legs and feet

- The foot lands about a third of a metre ahead of the hips in a sprint and closer at lower speeds.
  A straight leg landing far ahead looks like braking.
- The knee bends a little on landing and more as it bears weight.
  It bends about 20° to 40° in a run and less in a walk.
- To push off, the heel lifts and the foot rolls onto its ball, with the ankle pointing 20–30°.
  The leg extends behind while the knee stays slightly bent.
- In the swing, the heel folds up toward the hip and rises higher as the run speeds up.
  The knee drives forward and up, then the shin reaches out and sweeps back just before landing.
- Running feet land close to one line.
  Walking feet land a little apart.

## Arms

- Each arm swings with the opposite leg, more the faster the motion.
- A running elbow stays bent about 90°, closing more in front as the hand rises toward the chin and opening behind.
  Hands barely cross the body's middle.
- Time the arms from the legs' phase so the two stay in step.

## Trunk and head

- The trunk leans into a run, about 5–15° at a steady speed and more while speeding up, and tips a little over each
  planted foot.
- The shoulders turn against the hips.
  In a walk the hip on the swinging side drops a few degrees.
- The head tilts against the trunk's lean and bounce to keep the eyes level.

## This game's gait rig

- The pelvis is part of the torso model, so it cannot turn or drop on its own.
- `ActorAnimation.Advance` moves the walk, jog and sprint clocks with distance.
  `ActorAnimation.Stride` matches each gait's speed.
  A change to worker, walking or sprint speed fails a check until the gait is retimed.
- The planted leg is solved from where its foot touches the ground, and the swinging leg is keyed.
  A key change can push a toe through the ground.
  `CheckEveryGaitPlantsItsFeetAndLeavesTheGroundOnlyWhenRunning` in
  [AnimationChecks.cs](../../../../tests/AnimationChecks.cs) catches that for people.
