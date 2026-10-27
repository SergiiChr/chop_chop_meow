# Manual playthrough review — 27 October 2026

Reviewed main at `6b8ca01`, with the mine-unloading and torch-carcass fixes in the working tree.

## What was actually played

A rendered, normal New game, using keyboard and mouse events against the game window. No showcase, resource grants, time changes, or simulation fixtures were used for this run.

- Gathered a whole starter tree, stone, and a berry bush.
- Crafted a stone chisel, planks, wood parts, and wooden mallet.
- Built a bench, fitted its tools, loaded and crafted a crate.
- Built a trading post, placed the crate, packed 12 berries, picked it up, and sent it to the village.
- The sale returned 15 coins into the bank and advanced the tutorial to buying a pickaxe.
- Searched for water, reached the visible lake, tried the shore interaction, and collapsed from thirst.
- Observed respawn in the clearing and two subsequent wolf maulings during nighttime recovery attempts. The last observation was around 00:23 on the game clock.

This was an opening-through-first-night playthrough, not a completed steel chain, worker settlement, or ending. Screenshot/inspection delays add idle time; the run does not establish a fair thirst-duration or speedrun benchmark. Interaction and navigation failures remain relevant independently of that timing.

## Findings, in suggested priority order

### 1. Water is an invisible target on a visibly much larger lake

**Observed:** Reaching the lake and clicking a plausible stretch of shore did not let the player drink. There was no visible drinking point to aim for.

**Confirmed in code:** NewGame places one water resource at one point on the shore (`World.cs`). Its picking cylinder is only 1.2 tiles in radius, it has no model (`WorldModels.cs`), and picking does not map the rest of the lake onto that resource (`WorldView.Pick`). The welcome card and guide simply say to drink at the lake.

**Suggested POC change:** Let nearby shoreline clicks gather water, or give the actual interaction a visible marker and explicit approach prompt. This is the most urgent follow-up.

### 2. Survival guidance comes too late, and nighttime recovery is unforgiving

**Observed:** After the thirst collapse, the player respawned empty-handed in the clearing and was mauled. Another attempt to move back toward the lake ended in another mauling.

The goal chain mentions a cabin and campfire only after the pickaxe purchase. A player concentrating on the prescribed bench/crate/trade sequence can reach night without protection. The intro mentions shelter, but does not make preparation an actionable early goal. Respawn provides no visible recovery grace period.

**Suggested POC change:** Introduce water and shelter as early goals; consider brief respawn protection or a safe fallback respawn. The intended difficulty is a design decision, rather than an asserted implementation bug.

### 3. Inventory reflow makes consecutive actions error-prone

**Observed:** Slotting the chisel removed its backpack entry and shifted the mallet and other items left. The next click needed a different target. The same packing applies to ingredient and output transfers.

**Suggested change:** Preserve inventory slot positions, or otherwise avoid moving unrelated items under the pointer during transfers.

### 4. Station loading and storage clicks mean different quantities

**Observed:** A backpack click loaded one plank into the bench, while a crate transfer moved a stack. The crate's process bar stayed at zero with one of its six required planks loaded; there was no prominent text explaining the stall. It initially looked like production was failing to start.

**Confirmed:** The bench deliberately calls LoadInput with quantity 1; storage uses MoveStack. Ctrl and recipe double-click shortcuts exist, but are easier to discover from code than from the visible process row.

**Suggested change:** Show a short status such as “Needs 5 more planks,” and make “load one batch” discoverable. Keep quantity behavior consistent where practical.

### 5. Onboarding text disagrees with the current progression

**Observed:** The welcome card's second instruction says to build the stone furnace. The actual goal chain instead requires the chisel, bench, mallet, crate, and trading post before buying a pickaxe and building the furnace.

The same card describes a “10–15 minute POC,” while the pause menu includes mines and seven profitable days as end goals. Clarify whether that estimate describes only the starter loop.

**Additional source-backed text mismatch:** The current pickaxe objective says it returns in a crate. The market buys one pickaxe per lot and only boxes lots larger than one. This purchase was not reached manually in this run.

### 6. Recipe and building identification relies heavily on hovering

**Observed:** Stone/steel chisel and hammer categories use similar unlabeled icons. The compact building grid also makes a mine and trading post easy to confuse before hovering. Tooltips exist, so this is friction rather than missing functionality.

**Suggested change:** Give the active category a short visible name and make the current tutorial's recipe/building easier to identify.

## Fix verification

- Mine output selection now requires an on-duty miner assigned to that mine, holding a pickaxe with empty hands. The regression includes an earlier builder and a worker with a mine duty currently working elsewhere.
- Torch kills now clamp carcasses to the current map bounds, matching axe kills. The regression kills a wolf near the new map edge.
- `./build.py test` passed with SELF-TEST, UI-TEST, NET-HOST and NET-GUEST markers, including the existing material-driven progression and allocation checks.
- `./build.py lint` and `git diff --check` passed.

Only the two requested gameplay defects were fixed. The findings above are follow-up work, not silently applied design changes.
