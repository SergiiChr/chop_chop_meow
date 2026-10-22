# Baked actor animation atlases and work-state rendering

## Summary

Replace runtime sprite-sheet seam detection with sixteen named WebP atlases and authored region and foot-pivot metadata. The set contains fifteen four-direction, eight-frame clips and a seven-frame-per-direction unarmed player punch. It adds distinct hand-gathering clips for player and worker, and preserves the existing tool, work, walk, and carry actions.

An optional offline baker uses explicit crop rectangles and preserves every RGBA pixel inside each crop. The game loads only the baked atlases and `data/animations.json`; it does not need Pillow or source art at runtime. Tree canopies now fade over buildings using their alpha masks and draw depth.

Worker animation uses a transient work-state enum updated by the simulation, rather than status strings. The state is excluded from saves and recomputed after loading. The unused player attack selector argument was removed; the actual attack event continues to trigger the punch animation.

## Review

The runtime preview is [animations.gif](screenshots/animations.gif). The animation review fixture supports four-direction capture for inspecting poses and foot alignment. `./build.py test`, `./build.py lint` and `./build.py build` pass after merging `main`, including the exported smoke test. The offline baker reproduces all sixteen committed atlases and `data/animations.json` exactly, and its unit tests pass. The sixteen WebP atlases total about 4.6 MB, down from 21 MB of PNG sheets, and the exported pack is 18.7 MB. A headless smoke launch takes about 1.7 s, against 1.5 s on `main` and 3.5 s with runtime sheet analysis. Canopy fading over buildings adds about 1 ms of draw time per frame in the showcase fixture.
