# Locomotion

Use these notes when changing a walk, jog, sprint, or the transitions between them.

## Motion references

- Eadweard Muybridge's *Animal Locomotion* photo plates on Wikimedia Commons show people and animals in motion.
- Richard Williams's *The Animator's Survival Kit* lays out key poses and timing for walks and runs.
- Slow-motion sports footage and biomechanics papers show joint angles and timing.

## Any body

- **Gravity:** in the air the body's centre follows a parabola: a flight of `t` seconds rises and falls `g·t²/8`, and
  lands at `g·t/2`.
- **Contact:** a foot or paw on the ground does not slide. Tie the cycle to the distance moved, not to time, so stride
  length always matches speed.
- **Ground:** nothing sinks into it, and whatever bears weight touches it.
- **Knockback:** a short hop or throw shows how a hit moves a body across the ground.
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
- Time the arms from the legs' phase so the two stay in step.
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

## This game's gait rig

- The pelvis is part of the torso model, so it cannot turn or drop on its own.
- Only the walk, jog and sprint set the ankle. Other actions leave the boot square to the shin, so toes can dip into the
  ground at a steep shin.
- The walk's, jog's and sprint's clocks move with distance through `ActorAnimation.Advance`, each `Stride` matched to
  the speed it is timed for: a change of a worker's, walking or sprint speed fails a check until the gait is retimed.
- A gait's planted leg is solved from where its foot touches the ground, and its swinging leg is keyed. A key change can
  push a toe through the ground: the gait check in `tests/AnimationChecks.cs` catches that.
