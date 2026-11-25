# Economy rules

Prices, wages and demand live in `src/world/model/GameTables.cs`.
`tests/EconomyChecks.cs` runs these rules on every `./build.py check` and prints the numbers it measured as `Economy:` lines in `build/test.log`.

To tune: change the data, run the tests, and read those lines before and after.

One steel bar makes one stackable steel pipe at the anvil with a steel hammer in 10 base seconds.
A pipe tile costs one steel pipe.
A building takes ten steel pipes in its Plumbing slot.
The pump costs four steel bars, ten stone and four wood parts. A reusable bottle takes one steel sheet and one wood part at a bench with a steel hammer.

## 1. Prices and whole chains

Prices live only in the market: each good the village trades has its `Price` in `Market.Goods`, and the crate has `Market.CratePrice`, so nothing else can have one.
A good in the middle of a chain, such as flour, dough or an iron bar, has no value of its own.
A good is judged by its whole chain: every step from the ground up against what a crate of it sells for.

A crate's chain cost is:

- the worker-days of every step, measured by running real workers through whole days (gathering, water, each station), at a worker-day's cost (section 2);
- the coins spent buying what a harvest uses up: a farm plot sows 1 seed and 1 fertilizer, gets 2 seeds back and buys the fertilizer;
- the crate itself, made from planks, and packing it at a packing station, measured the same way: one worker packs 19 crates a day.

The estimate leaves out deliveries between stations, watering trips to farm plots and hauling to market.

A farm plot's work comes from its data: a harvest takes 1 worker-day (`FarmDef.HarvestWorkdays`) and gives 4 wheat after 3 days of watered growth (`GrowDays`).
The table shows the growth wait and the days one worker needs to make a crate from nothing, including a quarter of a harvest for a crate of bread.
One wheat grinds into 1 flour, kneads into 3 dough and bakes into 12 bread, a crate.
Meat comes from wolves the player kills at night, which no worker's day accounts for, so goods made from it are not costed.

The check fails if a crate of any costed good sells for no more than its chain costs.
A crate is judged at the full price, which a whole sale gets (section 3).
Measured, at 18 coins a worker-day, a crate of 12 and full price:

| Good | Worker-days | Labor | Bought | Cost | Sells for | Profit | Waits | One worker |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Berries | 0.58 | 10 | 0 | 10 | 13 | 3 | | 0.6 days |
| Bread | 0.42 | 8 | 0.8 | 8 | 30 | 22 | 3 days | 3.4 days |
| Canned jam | 11.11 | 200 | 0 | 200 | 963 | 763 | | 11.1 days |
| Steel axe, pickaxe, shovel | 1.11 | 20 | 0 | 20 | 471 | 451 | | 1.1 days |
| Steel hammer | 0.71 | 13 | 0 | 13 | 243 | 230 | | 0.7 days |

Not costed: raw meat, burnt steak, meat stew and canned meat stew.
A crate of canned jam needs 240 berries and 6 tin ore. The village buys the cans, not loose or bottled jam.
The `Economy:` lines print this table on every run.

## 2. Hiring

### Cost

A worker-day costs the lowest daily wage among the hire offers, 18 coins; what an offer pays up front is left out.
More up front means a lower wage, so a worker kept longer is cheaper, and the dearest wage needs nothing up front.

Wages are paid at dawn from the trading posts' wage slots first, where sale coins land, and then from the backpack.
The slot has no limit, so every sale coin lands there.

### Output

Worker output is measured by running one worker through a whole day: walking, hauling, lunch and the commute included, with chests 2.5 tiles away and steel tools at the bench.
A product's worker-days are its materials' worker-days plus one over its station's daily output.
Each good is judged by a full crate of it, making and packing the crate included.

- A hired berry picker barely breaks even: 0.9 to 1.25 times a worker-day's cost. Bushes fruit once a day (`BerryRegrowHours`), so one picker brings in two crates of berries a day, within what the village takes at the full price.
- A worker in a steel tool chain earns at least 4 times their pay.
- A cook earns at least 1.5 times their pay.
- A longer chain earns more per worker-day than a shorter one, comparing tools with tools and food with food.
- Goods made from meat are left out: meat comes from wolves at night, when workers are home.
- A harvest takes 1 worker-day and returns 4 wheat, 1 seed beyond the one sown and 1 grass for 1 fertilizer; each wheat becomes a crate of bread, so a bread chain earns about 4 times its pay.

### Measured output

The `Economy:` lines in `build/test.log` list each job's output per worker-day and each product's coins per worker-day for a crew sized so no one waits.
A starter crew of one worker per job (ore, coal, wood, stone furnace, reinforced furnace, anvil and bench) is held back
by the reinforced furnace, and still earns several times its pay.

## 3. Demand

Each good the village takes has `Daily`, `Steady` and `Drop` in its `Sells` entry in `Market.Goods`:

- A sale pays every unit at the price before it, so selling in bulk loses nothing; the sale then lowers the price.
- The first `Steady` units sold keep the full price. Past them, each crate's worth sold takes `Drop` off the full price, down to a quarter of it.
- A day restores what selling `Daily` units takes, so selling each good's daily amount every day is sustainable: the next day's sale pays the same.

| Good | Daily | Steady | Drop per crate |
|---|---|---|---|
| Berries | 36 (3 crates) | 36 | 5% |
| Raw meat and burnt steak | 24 (2 crates) | 24 | 5% |
| Bread, meat stew, canned jam and canned meat stew | 24 (2 crates) | 24 | 5% |
| Steel axe, pickaxe, shovel and hammer | 12 (1 crate) | 0 | 50% |

- Food is a steady staple: its daily amount sells at the full price, and past it the price slips slowly.
- Tools react sharply: a crate of one tool sells at the full price and takes half off it, so a second sale straight after fetches half. A day wins back half the full price.
- Selling past the basket keeps that good cheap for days.

Tools bring in most of a day's basket, food somewhat less and the crates a little; the `Economy:` lines give the totals.

## 4. Trade

- Buying costs 1.25 times the base price, crate included for boxed goods. Sale prices never exceed the base, so buying to resell always loses.
- A sale pays back each crate's price. Count crates as packaging that pays for itself, not as profit.
- The cheapest way to recover a lost axe is berries: a steel axe costs about four crates of berries.
