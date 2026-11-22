# Blows and things that move

Use these notes when a tool strikes what it works, or when something other than a body moves: a felled tree, a thrown
stump or chip, a door, a cart, a crate or a fire.

## Blows and tools

- A tool's working end lands on what it works at the moment its sound plays: an axe's head on the trunk, a pick's
  point on the rock, a hammer's face on the work top, a shovel's blade at the stump's foot.
  `StrikePoints` finds the spot, and `ActorAnimation` cuts the swing short there or reaches the arm out to it.
- Measure the tool's end, not the fist: `ActorRig.Holds` with `ActorRig.AxeHeadPoint`, `PickPoint` or `HammerFace`, and
  `ActorRig.ShovelTip`.
  `CheckEveryGatheringBlowLandsItsToolOnWhatItWorks` and `CheckEveryHammerBlowLandsOnTheStationsWorkTop` do it in the
  real view.
- Review a blow against its target with `--animation-target` (`tools/animation_review.py --target`): the close-up builds
  the real tree, rock, anvil or stump that far away.
- A blow speeds up all the way into its contact and stops there; a swing in the air eases out.
- The arm solver flips where a hand points straight down or an elbow folds tight.
  Keep marks in the range play gives, and step the aimed loop at 60 frames a second for joints that jump.

## Things that move

These follow the rules for [any body](locomotion.md#any-body) too.

- A fall, a throw or a drop follows gravity; a tree falls as a stiff pole hinged at its foot, slow at first.
- A slam reaches its frame at its fastest; time a sound so its loudest moment lands on the contact, not its start.
- Nothing rests in the ground: measure the lowest point of the mesh, not the node's origin.
  Euler angles read off a scaled node are wrong near 90°: measure from points.
- Move by `dt`, never by a fixed step or a floor on it, so a motion runs the same at 30, 60 and 144 frames a second and
  stops while paused.
- In a normal game the window applies the server's state 30 times a second.
  A motion drawn from a count the server keeps steps every other frame unless the window runs its own clock between
  updates (`LocalClock`).
  Check an update that brings a big change, such as a bellows pump or a new stage, and the frame after it too.
