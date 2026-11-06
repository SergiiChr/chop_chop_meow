---
name: frame-compare
description: Turn folders of numbered frames into a labelled side-by-side MP4, GIF or contact sheet. Use to show an animation, a before/after change or several camera views of rendered frames from any project.
---

# Frame compare

`compare.py` in this folder puts folders of numbered frames side by side, each under its label, with ffmpeg.
It needs only Python 3.10+ and ffmpeg with `libx264` and `drawtext`.

```sh
S=~/.claude/skills/frame-compare  # or the copy in a project, such as .claude/skills/frame-compare
# Before/after video: 66 frames from frame 20, each panel cropped to 460x700 around its centre, 70 px lower.
$S/compare.py out.mp4 before/:Before after/:After --first 20 --count 66 --crop 460x700 --shift 70 --scale .7
# The same as a GIF, for places that do not play video.
$S/compare.py out.gif before/:Before after/:After --first 20 --count 66 --crop 460x700 --shift 70 --scale .7
# Contact sheet: every 6th of 48 frames, numbered, 8 a row.
$S/compare.py sheet.png after/ --count 48 --step 6 --numbers --crop 440x680 --shift 70
```

- The output's extension picks the format: `.mp4` (default choice, small and easy to share), `.gif` or `.png`.
- Each panel is `folder:label`; the label defaults to the folder's name. Frames are `%03d.png` unless `--pattern` says
  otherwise.
- `--fps` sets playback speed; frames captured in slow motion play slowly at the capture rate.
- `--numbers` prints each frame's number, which helps to name a frame when describing a problem.
- Look at the result before sharing: pull a frame with `ffmpeg -ss 1 -i out.mp4 -frames:v 1 frame.png` and read it.
