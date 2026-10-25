# Economy rules

Prices, wages and demand live in `data/game.json`. `tests/EconomyChecks.cs` runs these rules on every `./build.py test` and prints the numbers it measured as `Economy:` lines in `build/test.log`. Change the data, run the tests, and read those lines before and after.

## 1. Value ladder

Every item has a `price`. For goods the village trades, it is the trade price; for the rest it is a reference value used only to keep recipes in line.

- Raw goods: wood, stone, berries and raw meat are worth 1; iron ore and mined coal 1.5, since they need a pickaxe.
- A recipe's output is worth its inputs plus the cheapest fuel its station burns, plus a markup that shrinks with each step from raw goods: 50% for the first step, then 40%, 30% and 20%.
- So lower tiers keep a bigger share of their price as profit, and deeper goods are worth more coins in total. A steel axe earns 12 coins over its parts and an iron bar 2, though the bar's margin is far higher.

| Step | Markup | Recipes |
|---|---|---|
| 1 | 50% | iron bar, cooked meat, planks, wood parts, stone chisel, torch |
| 2 | 40% | steel bar, iron parts, crate, wooden mallet |
| 3 | 30% | steel parts, iron axe, iron pickaxe, iron hammer |
| 4 | 20% | steel axe, steel pickaxe, steel hammer |

A new recipe gets its price from this rule. The check fails if any recipe loses value or a deeper recipe marks up more than a shallower one.

## 2. Hiring

A worker-day costs the daily wage plus the up-front payment spread over 10 days. All offers cost about the same over that time (24 to 26 coins); more up front means a lower wage, so a worker kept longer is cheaper.

Worker output is measured by running one worker through a whole day: walking, hauling, lunch and the commute included, with chests 2.5 tiles away and steel tools at the bench. A product's worker-days are its materials' worker-days plus one over its station's daily output.

- A hired berry picker barely breaks even: 0.9 to 1.25 times a worker-day's cost. Bushes fruit once a day (`berryRegrowHours`), so one picker brings in about two crates of berries a day.
- A worker in a steel tool chain earns at least 4 times their pay.
- A longer chain earns more per worker-day than a shorter one.
- Meat is left out: it comes from wolves at night, when workers are home.

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

| Product | Worker-days each | Coins per worker-day | Times pay |
|---|---|---|---|
| Berries | 0.042 | 24 | 1.0× |
| Steel hammer | 0.114 | 324 | 13× |
| Steel axe or pickaxe | 0.202 | 362 | 14.5× |

These are for a crew sized so no one waits. A starter crew of one worker per job (ore, coal, wood, stone furnace, bellows furnace, anvil and bench) is held back by the stone furnace: about 15 axes or 31 hammers a day, around 160 coins per worker-day, still 6 to 7 times pay.

## 3. Demand

Each good the village takes has a `daily` amount and a `drop` in `market.sells`. Selling one unit lowers its price by `drop / daily`, down to a quarter of the base price, and the price climbs back by `drop` over a day.

- Selling each good's daily amount every day is sustainable: the next day's sale pays the same.
- The basket is about two crates of each food (24) and a full crate of each steel tool (12) a day.
- Food is a steady staple: its daily amount takes 10% off its price. Tools swing more: a crate of one tool takes 30% off. Selling past the basket keeps that good cheap for days.

A basket sells for about 2,020 coins a day: 1,890 from tools, 100 from food and 30 for the crates.

## 4. Trade

- Buying costs 1.25 times the base price, crate included for boxed goods. Sale prices never exceed the base, so buying to resell always loses.
- A sale pays back each crate's price. Count crates as packaging that pays for itself, not as profit.
- The cheapest way to recover a lost axe is still berries: a steel axe costs 92 coins, about seven crates of berries.
