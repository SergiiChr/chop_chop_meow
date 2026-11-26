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

Run every command from the main checkout until the test worktree is made, and the clean up from it again.

1. Fast-forward main as [docs/RELEASE.md](../../../docs/RELEASE.md) says, and read its version:
   `git grep -h -o "int Version { get; set; } = [0-9]*" main -- src`.
2. Make a test worktree of main and work from it, so that the scratch files and the test servers stay out of the main
   checkout.
   Its `tmp/` must be ignored: the list `git rev-parse --git-path info/exclude` names holds `tmp/` for every worktree.

   ```sh
   git worktree add --detach ../worktrees/chop_chop_meow-saves main
   ln -s "$PWD/.tools" ../worktrees/chop_chop_meow-saves/.tools
   ```

3. Test every slot on a server as below, at whatever version it holds.
   Leave alone each slot that loads.
   A slot refused although it holds main's version was converted wrongly before: convert it again from the backup that
   conversion left.
4. Read each slot's `Version` and the time it was written.
   Copy each slot to convert to `backup-<file name without .json>-v<old>-before-v<new>-<yyyymmdd>.json` beside it,
   such as `backup-settlement-v17-before-v23-20261126.json`.
   Never write over an existing backup.
   Always convert from the backup, so a second try never starts from a half-converted file.

## Find what changed

List where main's save version went up, starting a few days before the slot was written:

```sh
for c in $(git rev-list --first-parent --reverse --since=<a few days before the slot was written> main); do
  v=$(git grep -h -o "int Version { get; set; } = [0-9]*" $c -- src | grep -o "[0-9]*$" | head -1)
  echo "$v $(git log -1 --format='%h %ad %s' --date=short $c)"
done | awk '$1!=p{print; p=$1}'
```

The first row must hold the slot's version or an older one; otherwise start earlier.
For each raise after the slot's version, compare the merge with its first parent:

- Every type `SaveState` reaches: `Thing`, `Worker`, `TradeOrder` and `SaveState` itself in `src/world/World.cs`,
  `Inventory`, `PlayerState` with its crafting queue (`Queued`, written by `QueueJson`), `Wolf`, the worker jobs in
  `src/jobs/`, and the pipe and sand data `SaveState` holds.
  A property that was removed or became `[JsonIgnore]` loses its value, so carry it over into whatever took its place.
- Enums: `ThingKind` and `JobKind` are saved by their snake_case names, so a renamed member needs renaming in the
  save.
  Every other enum is saved as a number, so a member inserted or moved in the middle shifts the numbers after it.
- Item and recipe ids in `src/world/model/GameTables.cs`.
- The load checks in `World.Deserialize`, `ValidPlayer` and `ValidSettlement`, and the `Valid` methods they call.
  A new rule can refuse an old building that lacks a new part.
  For example, the reinforced furnace gained a one-slot holder for the glassblowing pipe, so older furnaces had none.

Write the conversion as a throwaway Python script in the test worktree's `tmp/saves/`, run it with `python3 -I`, and
give it one step per change.
It reads the backup and writes the result over the real slot in the main checkout's user data.
First check that the slot's time is still the one you read, so a game the player saved since is never written over.
Name copies of C# code saved in `tmp/` `*.cs.txt`: the build compiles every C# file in the worktree, `tmp/` included.

## Test each slot on a server

Load each slot in the test worktree, never in the main checkout or in the merged branch's worktree, which may lack
main's other changes.
A worktree's `./build.py play` copies the player's saves in first, so the test loads the real slot as it is now, and a
server's autosave never reaches the real slots.

```sh
rm -f tmp/port.txt
TMPDIR=$PWD/tmp ./build.py play -- --server --slot <n> --port-file $PWD/tmp/port.txt > tmp/server.log 2>&1 &
echo $! > tmp/server.pid
timeout 900 bash -c 'until [ -s tmp/port.txt ] || grep -q "ERROR\|Exception" tmp/server.log; do sleep 1; done'
```

`build.py` turns into the game it starts, so `tmp/server.pid` holds the server's process id.
With `--server`, `./build.py play` starts the game without a window, as the game starts its own server.
The first run in a fresh worktree builds and exports the game first; a `timeout` exit of 124 means it took longer than
15 minutes, so look at `tmp/server.log` before trying again.

The slot loaded once the server writes its port to `tmp/port.txt`.
This shows only that the slot loads: with nobody joined, the server does not run the world, and it ends by itself
after 30 seconds.
Wait for that process to end and check its log for errors.

`Cannot load slot <n>.` in the log means the slot was refused, and the rest of that line says why.
A refused server keeps running: stop it with `kill $(cat tmp/server.pid)`.
Never stop a server found by its name alone, since the player's own game and its server run under the same name.

When the reason is `This save holds unknown items or values out of range`, find the check that refused it.
Temporarily make every `return false` in the load checks print its line, run the server again, then put
`src/world/World.cs` back and confirm `git status` is clean:

```sh
cp src/world/World.cs tmp/World.cs.clean.txt
python3 - <<'EOF'
p = 'src/world/World.cs'
lines = open(p).read().split('\n')
start = next(i for i, l in enumerate(lines) if '// ---------- Validation of saves' in l)
end = next(i for i, l in enumerate(lines) if 'private bool ValidToolSlot(' in l)
for i in range(start, end):
    lines[i] = lines[i].replace('return false', 'return Fail()')
lines.insert(start, '    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)'
             ' { GD.PrintErr($"SAVECHECK failed at original World.cs:{line - 1}"); return false; }')
open(p, 'w').write('\n'.join(lines))
EOF
# run the server as above, read the SAVECHECK line, then:
cp tmp/World.cs.clean.txt src/world/World.cs
```

- A line can join several conditions or call a check in another file, such as `GlassBlowing.Valid`: read each one
  against the save.
- No `SAVECHECK` line means the last `return` of `ValidPlayer` refused it: a player's health, hunger, thirst, torch or
  respawn time is out of its range.
- A refusal while the JSON is read names the field instead, such as `could not be converted to
  ChopChopMeow.ThingKind. Path: $.Things[410].Kind`.
- `Unsupported or incomplete save` and `invalid wolf movement` come from the conditions in `World.Deserialize` itself:
  compare the fields they require with the save.

Fix the cause in the conversion script, convert again from the backup and test again, until every slot loads.

## Clean up

Once every slot loads:

1. Stop any server of this run still running: `kill $(cat tmp/server.pid)`.
2. From the main checkout, remove the test worktree with its scratch files and clear git's record of it:
   `git worktree remove ../worktrees/chop_chop_meow-saves`, then `git worktree prune`.
3. Remove the merged branch's worktree and its local branch, only when `git merge-base --is-ancestor <branch> main`
   says it is merged.
   `git worktree remove` refuses a worktree with changes: stop and ask when it or the merge test refuses, never force
   them.
   Then `git branch -d <branch>` and `git worktree prune`.
   Remove only the worktree and branch of the merge this run is for, never another session's.
4. Keep the backups beside the slots.

## Report

For each slot, give its version before and after, each change made to it, and its backup's file name.
Name any slot left alone and why, and what the clean up removed.
