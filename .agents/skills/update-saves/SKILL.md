---
name: update-saves
description: Bring the player's own save slots up to the save version on main and fix whatever stops them loading, testing each on a headless server. Use after merging a branch that raises the save version, or when the player's save will not load on main.
---

# Update saves

A save holds `SaveState.Version`, and `World.Deserialize` refuses any other version.
The game holds no code that converts an old save ([CODING.md](../../../CODING.md)), so the player's saves are brought
forward by hand once a branch that raises the version is merged into main.
Do it without asking first, and never commit the conversion.

## Find the slots

The player's slots are in the main checkout's user data, `.tools/xdg-data/godot/app_userdata/Chop Chop Meow/`:
`autosave.json` is slot 0, `settlement.json` slot 1, and `settlement-<n>.json` slot n.
Files named `backup-*`, `net-check-*`, `server-check-*`, `performance-*` and `playtest-*` are not the player's slots.

1. Fast-forward main as [docs/RELEASE.md](../../../docs/RELEASE.md) says, and read its version:
   `git grep -h -o "int Version { get; set; } = [0-9]*" main -- src`.
2. Make a test worktree of main and work from it, so that the scratch files and the test servers stay out of the main
   checkout:

   ```sh
   git worktree add --detach ../worktrees/chop_chop_meow-saves main
   ln -s "$PWD/.tools" ../worktrees/chop_chop_meow-saves/.tools
   ```

3. Read each slot's `Version` and the time it was written.
   Leave slots already at main's version alone.
4. Copy each slot to convert to `backup-<slot>-v<old>-before-v<new>-<yyyymmdd>.json` beside it.
   Never write over an existing backup, and never write over a slot that is newer than its backup.
   Always convert from the backup, so a second try never starts from a half-converted file.

## Find what changed

List where main's save version went up since the slot's version:

```sh
for c in $(git rev-list --first-parent --reverse --since=<date of the slot's version> main); do
  v=$(git grep -h -o "int Version { get; set; } = [0-9]*" $c -- src | grep -o "[0-9]*$" | head -1)
  echo "$v $(git log -1 --format='%h %ad %s' --date=short $c)"
done | awk '$1!=p{print; p=$1}'
```

For each raise, compare the merge with its first parent:

- The saved classes: `Thing`, `Worker` and `SaveState` in `src/world/World.cs`, `Inventory`, `PlayerState` and
  `Wolf`.
  A property that was removed or became `[JsonIgnore]` loses its value, so carry it over into whatever took its place.
- Enums: `ThingKind` is saved by its snake_case name, so a renamed kind needs renaming in the save.
  Every other enum is saved as a number, so a member inserted or moved in the middle shifts the numbers after it.
- Item and recipe ids in `src/world/model/GameTables.cs`.
- The load checks in `World.Deserialize`, `ValidPlayer` and `ValidSettlement`, and the `Valid` methods they call.
  A new rule can refuse an old building that lacks a new part.
  For example, the reinforced furnace gained a one-slot holder for the glassblowing pipe, so older furnaces had none.

Write the conversion as a throwaway Python script in the test worktree's `tmp/saves/`, run it with `python3 -I`, and
give it one step per change.
Name copies of C# code saved there `*.cs.txt`: the build compiles every `.cs` file in the worktree, `tmp/` included.

## Test each slot on a headless server

Load each converted slot in the test worktree, never in the main checkout or in the merged branch's worktree, which
may lack main's other changes.
A worktree's `./build.py play` copies the player's saves in first, so a server's autosave never reaches the real slots.

```sh
rm -f tmp/port.txt
(TMPDIR=$PWD/tmp ./build.py play -- --server --slot <n> --port-file $PWD/tmp/port.txt > tmp/server.log 2>&1 &)
timeout 300 bash -c 'until [ -s tmp/port.txt ] || grep -q "ERROR\|Exception" tmp/server.log; do sleep 1; done'
```

The slot loaded once the server writes its port to `tmp/port.txt`.
`Cannot load slot <n>` in the log means a load check refused it.
Let a server that loaded run for about ten seconds, check its log for errors, then stop it by the process id that
`pgrep -a ChopChopMeow` shows.

When a slot is refused, find the check that refused it.
Temporarily make every `return false` in the load checks print its line, run the server again, then put
`src/world/World.cs` back and confirm `git status` is clean:

```sh
cp src/world/World.cs tmp/World.cs.clean
python3 - <<'EOF'
p = 'src/world/World.cs'
lines = open(p).read().split('\n')
start = next(i for i, l in enumerate(lines) if 'private bool ValidCounts(' in l)
end = next(i for i, l in enumerate(lines) if 'private bool ValidToolSlot(' in l)
for i in range(start, end):
    lines[i] = lines[i].replace('return false', 'return Fail()')
lines.insert(start, '    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)'
             ' { GD.PrintErr($"SAVECHECK failed at original World.cs:{line - 1}"); return false; }')
open(p, 'w').write('\n'.join(lines))
EOF
# run the server as above, read the SAVECHECK line, then:
cp tmp/World.cs.clean src/world/World.cs
```

A refusal while the JSON is read names the field instead, such as `could not be converted to ChopChopMeow.ThingKind.
Path: $.Things[410].Kind`.
Fix the cause in the conversion script, convert again from the backup and test again, until every slot loads.

## Clean up

Once every slot loads:

1. Stop any server still running: `pgrep -a ChopChopMeow`, then `kill` its process id.
2. Remove the test worktree with its scratch files: `git worktree remove ../worktrees/chop_chop_meow-saves`.
3. Remove the merged branch's worktree and its local branch.
   `git worktree remove` refuses a worktree with changes, and `git branch -d` a branch that is not merged into main:
   stop and ask when either refuses, never force them.
   Remove only the worktree and branch of the merge this run is for, never another session's.
4. Keep the backups beside the slots.

## Report

For each slot, give its version before and after, each change made to it, and its backup's file name.
Name any slot left alone and why, and what the clean up removed.
