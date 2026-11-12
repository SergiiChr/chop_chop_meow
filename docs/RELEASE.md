# Development and release

## Development

1. Start from fresh `main` in a worktree, since other sessions may be editing the main checkout:

   ```sh
   git fetch origin main:main
   git worktree add -b <branch> ../worktrees/chop_chop_meow-<name> main
   ln -s "$PWD/.tools" ../worktrees/chop_chop_meow-<name>/.tools
   ```

   Worktrees live together in a `worktrees` folder next to the main checkout. The symlink reuses the downloaded Godot,
   .NET and Python tools and is ignored by git. Leave the main checkout on whatever branch it is on:
   `git fetch origin main:main` fast-forwards `main` without switching to it. Git refuses that while a checkout is on
   `main`: run `git fetch origin` and `git merge --ff-only origin/main` in that checkout instead.
2. Make the change and update the docs it affects in the same commit.
3. Run `./build.py check`. For a visual change, also inspect a rendered screenshot.
4. Review the branch against `main` with the `self-review` skill and apply what improves it.
5. Run `./build.py build` before opening the pull request.
6. Open a pull request against `main`. Describe what changed, how it was checked and what was not checked. CI runs `./build.py check` on it.

## Release bundle

`./build.py build` produces the portable bundle described in the [README](../README.md#release-bundle).
