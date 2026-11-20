# Wolves and cabin doors

Use these notes for wolf motion and scenes at a cabin's door.

## Four legs and tails

- A walk sets each foot down in turn, a trot moves diagonal pairs together, and a gallop lands the feet in order with
  time in the air.
  Wolves trot for distance and gallop to chase.
- In a gallop the spine flexes to gather the legs under the body and extends to stretch them out.
- The head bobs to balance the front legs.
  The tail trails the body and swings out to balance turns.
- Apply the [contact, ground and timing rules](locomotion.md#any-body) to every paw.
  `CheckWolfGaitsPlantEachPaw` in
  [WolfGaitChecks.cs](../../../../tests/actors/enemies/wolf/WolfGaitChecks.cs) checks paw contact through each gait.

## Contacts

- Move a leaping wolf so `WolfModel.Bite` lands on `FallBackView.Throat`, which follows the knocked-down player's tip.
  The jaws stay on the throat as both go down.
- A struck wolf recoils at `ActorAnimation.Contact`, when the swing lands.
- For a side attack, use an overhead view to check which limb it takes and measure how far to that side it is drawn.
  For doorway and wolf showcase scenes use `--camera-pitch 89`.
  For a close-up clip use `./tools/animation_review.py "wolf leap" --views top`.
- Check anything that glows, such as wolf eyes, in a dark scene as well.

## Rig

- `WolfShape` builds one skinned mesh on a skeleton.
  `WolfModel` poses its spine, neck, head, jaw, ears, tail and legs from a `WolfPose`.
  `Reach` solves the leg angles from where each paw stands.
  `WolfModel.Paw` and `WolfModel.Bite` give where a paw and the jaws are in the pose shown.
  `SyncWolves` in `src/WorldView.cs` times the wolf against the player.

## Recording

- Close-ups include `wolf idle`, `wolf howl`, `wolf walk`, `wolf trot`, `wolf gallop`, `wolf speeds` and `wolf turn`.
  `wolf speeds` goes from standing to a gallop and back.
  `wolf turn` reverses a gallop every two seconds and shows the head leading the turn and the braking step.
  The other clips are `wolf leap`, `wolf hit`, `wolf feed`, `wolf fall`, `wolf snap` (the bite) and `wolf brace`.
  Leap, hit, feed, snap and brace play against a player, as `SyncWolves` sets them up.
  `./build.py play -- --animation-review` shows seven wolf clips alongside the human clips.
  Use `--animation-clip` to see any of the 13 wolf close-ups individually.
- Wolves moved by the game's own rules play in `--showcase --wolf-scene patrol` (turns on a patrol), `howl` (turning to
  a player and howling), `feast` (a takedown and feeding), `follow`, `rout` and `stand` (follow mode behind a torch)
  and `fight` (snaps against axe swings), and at a cabin's door in the `--doorway` scenes
  (README, command-line flags).
  Record them with `./build.py record` as shown in the [animation skill](../SKILL.md#record).

For a close look at a face or paw, set `--animation-zoom` in a direct recording.
For example, turn the idle wolf toward the camera and record through its wary half to see the snarl.

```sh
./build.py record build/recordings/face.avi -- --animation-review --animation-clip "wolf idle" \
    --animation-pitch 3 --animation-turn 90 --animation-zoom 2.6 --frames 200
```
