# Economy rules

Prices, wages and demand live in `data/game.json`. `tests/EconomyChecks.cs` runs these rules on every `./build.py test` and prints the numbers it measured as `Economy:` lines in `build/test.log`. Change the data, run the tests, and read those lines before and after.

## 1. Value ladder

Every item has a `price`. For goods the village trades, it is the trade price; for the rest it is a reference value used only to keep recipes in line.

- Raw goods: wood, stone, berries, wheat seeds and raw meat are worth 1; iron ore, mined coal and tin ore 1.5, since they need a pickaxe. Leaves and grass are worth a quarter: they come by the handful and four make one fertilizer. Wheat is 1.5, what one bag of seeds and one of fertilizer return with the first-step markup.
- A recipe's output is worth its inputs plus the cheapest fuel its station burns, plus a markup that shrinks with each step from raw goods: 50% for the first step, then 40%, 30% and 20%.
- So lower tiers keep a bigger share of their price as profit, and deeper goods are worth more coins in total. A steel axe earns 12 coins over its parts and an iron bar 2, though the bar's margin is far higher.

| Step | Markup | Recipes |
|---|---|---|
| 1 | 50% | iron bar, tin bar, burnt steak, planks, wood parts, stone chisel, torch, mortar and pestle, crushed berries, flour, meat stew |
| 2 | 40% | steel bar, iron parts, crate, wooden mallet, tin cans, berry jam, dough |
| 3 | 30% | steel parts, iron axe, iron pickaxe, iron hammer, bread, canned meat stew, canned jam, canned food |
| 4 | 20% | steel axe, steel pickaxe, steel hammer, steel chisel, rake |

Canned food, which the village also sells, is canned raw meat and now costs 5.35 instead of 4.

A new recipe gets its price from this rule. The check fails if any recipe loses value or a deeper recipe marks up more than a shallower one.

## 2. Hiring

A worker-day costs the daily wage plus the up-front payment spread over 10 days. All offers cost about the same over that time (24 to 26 coins); more up front means a lower wage, so a worker kept longer is cheaper, and the dearest wage needs nothing up front.

Worker output is measured by running one worker through a whole day: walking, hauling, lunch and the commute included, with chests 2.5 tiles away and steel tools at the bench. A product's worker-days are its materials' worker-days plus one over its station's daily output.

- A hired berry picker barely breaks even: 0.9 to 1.25 times a worker-day's cost. Bushes fruit once a day (`berryRegrowHours`), so one picker brings in two crates of berries a day, within what the village takes at the full price.
- A worker in a steel tool chain earns at least 4 times their pay.
- A cook earns at least 1.5 times their pay.
- A longer chain earns more per worker-day than a shorter one, comparing tools with tools and food with food.
- Goods made from meat or wheat are left out: meat comes from wolves at night, when workers are home, and wheat from farm plots.
- Farming does not pay yet. A harvest takes 2 worker-days and returns 1 wheat (1.5), 1 seed beyond the one sown (1) and 1 grass (0.25) for 1 fertilizer (1.5): about 1.25 coins, against 50 coins of wages. The yields and harvest time in `farm` need tuning before wheat goods can be priced against labour.

Measured with the current data:

| Job | Per worker-day |
|---|---|
| Mining ore or coal, chopping wood | 585 units |
| Picking berries | 24 berries |
| Stone furnace, iron bars | 62 |
| Bellows furnace, steel bars | 45 |
| Anvil, steel parts | 57 |
| Bench, wood parts | 114 |
| Bench, steel tools | 58 |
| Stone furnace, tin bars | 62 |
| Bench, tin cans | 300 |
| Cooking table, crushed berries | 140 |
| Cooking table, berry jam | 42 |
| Canning station, canned jam | 49 |

| Product | Worker-days each | Coins per worker-day | Times pay |
|---|---|---|---|
| Berries | 0.042 | 24 | 1.0× |
| Berry jam | 0.123 | 45 | 1.8× |
| Canned jam | 0.154 | 73 | 2.9× |
| Steel hammer | 0.114 | 324 | 13× |
| Steel axe or pickaxe | 0.202 | 362 | 14.5× |

These are for a crew sized so no one waits. A starter crew of one worker per job (ore, coal, wood, stone furnace, bellows furnace, anvil and bench) is held back by the stone furnace: about 15 axes or 31 hammers a day, around 160 coins per worker-day, still 6 to 7 times pay.

## 3. Demand

Each good the village takes has `daily`, `steady` and `drop` in `market.sells`:

- The first `steady` units sell at the full price. Past them, each crate's worth sold takes `drop` off the full price, down to a quarter of it.
- A day restores what selling `daily` units takes, so selling each good's daily amount every day is sustainable: the next day's sale pays the same.

| Good | Daily | Steady | Drop per crate |
|---|---|---|---|
| Berries | 36 (3 crates) | 36 | 5% |
| Raw meat and burnt steak | 24 (2 crates) | 24 | 5% |
| Bread, berry jam, meat stew, canned jam and canned meat stew | 24 (2 crates) | 24 | 5% |
| Steel axe, pickaxe and hammer | 12 (1 crate) | 0 | 50% |

Food is a steady staple: its daily amount sells at the full price, and past it the price slips slowly. Tools react sharply: a crate of one tool takes half off the price of the last one in it and averages 77%, and a second crate the same day sells for little. Selling past the basket keeps that good cheap for days.

A day's basket sells for about 2,920 coins: 1,690 from tools, 1,165 from food and 63 for the crates.

## 4. Trade

- Buying costs 1.25 times the base price, crate included for boxed goods. Sale prices never exceed the base, so buying to resell always loses.
- A sale pays back each crate's price. Count crates as packaging that pays for itself, not as profit.
- The cheapest way to recover a lost axe is still berries: a steel axe costs 92 coins, about seven crates of berries.
