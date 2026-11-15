# Economy rules

Prices, wages and demand live in `src/GameTables.cs`.
`tests/EconomyChecks.cs` runs these rules on every `./build.py check` and prints the numbers it measured as `Economy:` lines in `build/test.log`.

To tune: change the data, run the tests, and read those lines before and after.

## 1. Prices and whole chains

Only the goods the village buys or sells, and the crate they travel in, have a `Price`; the game refuses to start with a price on anything else.
A good in the middle of a chain, such as flour, dough or an iron bar, has no value of its own.
A good is judged by its whole chain: every step from the ground up against what a crate of it sells for.

A crate's chain cost is:

- the worker-days of every step, measured by running real workers through whole days (gathering, water, each station), at a worker-day's cost (section 2);
- the coins spent buying what a harvest uses up: a farm plot sows 1 seed and 1 fertilizer, gets 2 seeds back and buys the fertilizer;
- the crate itself, made from planks.

A farm plot's work comes from its data: a harvest takes 1 worker-day (`FarmDef.HarvestWorkdays`) and gives 4 wheat after growing 3 days (`GrowDays`), which a chain waits for.
One wheat grinds into 1 flour, kneads into 3 dough and bakes into 12 bread, a crate.
Meat comes from wolves the player kills at night, which no worker's day accounts for, so goods made from it are not costed.

The check fails if a crate of any costed good sells for no more than its chain costs.
Measured, at 25 coins a worker-day, a crate of 12 and full price:

| Good | Worker-days | Labor | Bought | Cost | Sells for | Profit | Waits |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Berries | 0.55 | 14 | 0 | 14 | 15 | 2 | |
| Bread | 0.41 | 10 | 0.8 | 11 | 30 | 19 | 3 days |
| Berry jam | 1.52 | 38 | 0 | 38 | 70 | 32 | |
| Canned jam | 1.87 | 47 | 0 | 47 | 143 | 96 | |
| Steel axe, pickaxe, shovel | 1.57 | 39 | 0 | 39 | 471 | 432 | |
| Steel hammer | 0.97 | 24 | 0 | 24 | 243 | 219 | |

Not costed: raw meat, burnt steak, meat stew and canned meat stew.
The `Economy:` lines print this table on every run.

## 2. Hiring

### Cost

A worker-day costs the daily wage plus the up-front payment spread over 10 days.
All offers cost about the same over that time (24 to 26 coins).
More up front means a lower wage, so a worker kept longer is cheaper, and the dearest wage needs nothing up front.

Wages are paid at dawn from the trading posts' wage slots first, where sale coins land, and then from the backpack.
The slot has no limit, so every sale coin lands there.

### Output

Worker output is measured by running one worker through a whole day: walking, hauling, lunch and the commute included, with chests 2.5 tiles away and steel tools at the bench.
A product's worker-days are its materials' worker-days plus one over its station's daily output.

- A hired berry picker barely breaks even: 0.9 to 1.25 times a worker-day's cost. Bushes fruit once a day (`BerryRegrowHours`), so one picker brings in two crates of berries a day, within what the village takes at the full price.
- A worker in a steel tool chain earns at least 4 times their pay.
- A cook earns at least 1.5 times their pay.
- A longer chain earns more per worker-day than a shorter one, comparing tools with tools and food with food.
- Goods made from meat are left out: meat comes from wolves at night, when workers are home.
- A harvest takes 1 worker-day and returns 4 wheat, 1 seed beyond the one sown and 1 grass for 1 fertilizer; each wheat becomes a crate of bread, so a bread chain earns about 3 times its pay.

### Measured output

The `Economy:` lines in `build/test.log` list each job's output per worker-day and each product's coins per worker-day for a crew sized so no one waits.
A starter crew of one worker per job (ore, coal, wood, stone furnace, reinforced furnace, anvil and bench) is held back by the anvil, and still earns several times its pay.

## 3. Demand

Each good the village takes has `Daily`, `Steady` and `Drop` in `Market.Sells`:

- The first `Steady` units sell at the full price. Past them, each crate's worth sold takes `Drop` off the full price, down to a quarter of it.
- A day restores what selling `Daily` units takes, so selling each good's daily amount every day is sustainable: the next day's sale pays the same.

| Good | Daily | Steady | Drop per crate |
|---|---|---|---|
| Berries | 36 (3 crates) | 36 | 5% |
| Raw meat and burnt steak | 24 (2 crates) | 24 | 5% |
| Bread, berry jam, meat stew, canned jam and canned meat stew | 24 (2 crates) | 24 | 5% |
| Steel axe, pickaxe, shovel and hammer | 12 (1 crate) | 0 | 50% |

- Food is a steady staple: its daily amount sells at the full price, and past it the price slips slowly.
- Tools react sharply: a crate of one tool takes half off the price of the last one in it and averages 77%, and a second crate the same day sells for little.
- Selling past the basket keeps that good cheap for days.

Food brings in most of a day's basket, tools somewhat less and the crates a little; the `Economy:` lines give the totals.

## 4. Trade

- Buying costs 1.25 times the base price, crate included for boxed goods. Sale prices never exceed the base, so buying to resell always loses.
- A sale pays back each crate's price. Count crates as packaging that pays for itself, not as profit.
- The cheapest way to recover a lost axe is berries: a steel axe costs about four crates of berries.
