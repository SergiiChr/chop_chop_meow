---
name: intro-film
description: Change the intro film the game opens with, check it shot by shot and make its movie file again. Use when changing a shot, pose, set, camera or sound of the intro, or after changing a model the film shows.
---

# Intro film

The film's code lives in `src/intro/`.
The game opens by playing `assets/intro.ogv` over the start menu (`IntroMovie.cs`); Esc skips it.
The game draws the film's frames itself (`WorldIntro.cs`, `IntroSets.cs`, `IntroPoses.cs`), and a movie tool outside
this project turns them into the movie.
[docs/DESIGN.md](../../../docs/DESIGN.md#intro-film) describes how the film is built.

## Workflow

1. Change the shot in `src/intro/WorldIntro.cs`: each shot is one method given the seconds since its cut.
2. Look at single moments first; a stills run takes about 15 seconds:

   ```sh
   ./tools/intro_film.py --stills 12.4,13.1,16.9      # build/intro/stills/still_12.40.png ...
   ```

   Put them on one sheet and look at the sheet, not at each still (see [Tools](#tools)).
3. When the stills are right, draw the whole film (about 4.5 minutes) and look at each changed shot as a strip of
   frames:

   ```sh
   ./tools/intro_film.py                               # build/intro/frames/00000.png ... and cues.tsv
   ```

4. Make the movie the game plays, and a large one to show:

   ```sh
   T=~/projects/cgi_tools
   $T/retro_movie.py build/intro/frames --cues build/intro/frames/cues.tsv --sounds assets/sounds --sounds assets \
       --gain 6 --ogv assets/intro.ogv --mp4 build/intro/intro.mp4
   ```

5. Play it in the real window with the `playtest` skill: a shot of the film right after launch, Esc, a shot of the menu.
6. Run `./build.py check`; `CheckIntroFilmCoversTheStartMenuUntilEsc` covers the film taking input and Esc.

## Tools

The movie, sound and sheet tools live in `~/projects/cgi_tools`, with skills of their own (`retro-movie`,
`sound-cues`, `shot-review`).
They are not part of this project and nothing in it may call them: only `assets/intro.ogv` is committed.

```sh
T=~/projects/cgi_tools
$T/contact_sheet.py build/intro/stills sheet.png --cols 3                       # every still, named
$T/contact_sheet.py build/intro/frames strip.png --from 12 --to 17 --count 12   # a shot as a strip, timed
$T/loudness.py assets/intro/bag.mp3 --from 5.4 --length 1.6 --step 0.02         # where a sound's parts are
```

## Shots

Times are from the film's start; a change to a shot's length moves every later shot.

| Starts | Shot | Method |
| --- | --- | --- |
| 0 | Bored at the desk | `DeskShot` |
| 5 | Boss yelling | `BossShot` |
| 9 | Bat and run | `SmashShot` |
| 12 | Packing at home | `PackingShot` |
| 17 | Train | `TrainShot` |
| 20 | Forest trail | `ForestShot` |
| 23 | Arrival at the shore | `ArrivalShot` |
| 27.4 | Touching the sea, fade | `ShoreShot` |
| 31 | Black | |
| 33 | Night by the fire, fade | `NightShot` |

## What goes wrong

- **Something stands between the camera and the hero.** A prop, a partition or a tree hides him in a still that
  looked fine in the head. Check the first and last frame of every shot whose camera or set changed.
- **A hand misses what it reaches for.** `Reaching` bends an arm to a point but an arm is only so long: lower the squat,
  lean the body further or move the thing closer.
- **An arm will not cross the body.** A pose's elbow folds only forward; `Folding` with `ActorRig.RollArm` turns the
  upper arm too, as the crossed arms by the fire do.
- **Only the cue sheet changed.** A stills run writes the whole `cues.tsv` too: copy `build/intro/stills/cues.tsv` over
  `build/intro/frames/cues.tsv` and make the movie again, without drawing the frames again.
- **Sound cannot be heard from here.** Place sounds by measuring them (`loudness.py`) and check the mix by its loudness
  per shot; say that it was measured, not heard, and what to check by ear.
- **The film is drawn by its own clock.** Anything that moves by real time, such as a shader's `TIME`, runs at the wrong
  speed in the frames; give it the film's time, as the sea's waves get.
- **A new model in the film.** The film's own sets belong in `IntroSets`; anything the game already has is taken from
  the game's builders, so it stays the same model.
