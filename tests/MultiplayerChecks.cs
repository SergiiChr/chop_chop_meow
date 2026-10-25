using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;

/// <summary>
/// Deterministic checks for players sharing one world: guests act through commands the host runs as them,
/// each player has their own needs, pack and messages, and a guest's saved player is checked before it comes in.
/// The real two-process session is covered by the network check.
/// </summary>
public static class MultiplayerChecks {
    private const int GuestId = 7;

    public static void Run(GameData data) {
        CheckGuestActsThroughCommands(data);
        CheckGuestCopyForwardsActions(data);
        CheckGuestGathersAndHammers(data);
        CheckEachPlayerHasTheirOwnPack(data);
        CheckSleepWaitsForEveryone(data);
        CheckGuestSaveRoundTrip(data);
        CheckBrokenGuestSavesStartEmpty(data);
    }

    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException($"Multiplayer check failed: {message}");
    }

    private static (World World, PlayerState Guest, List<string> Heard) Hosted(GameData data) {
        var world = new World(data, false);
        var heard = new List<string>();
        world.Relay = (to, message) => { if (to == null || to.Id == GuestId) heard.Add(message); };
        var guest = world.AddGuest(GuestId, null);
        return (world, guest, heard);
    }

    private static void Tick(World world, float seconds) {
        for (; seconds > 0; seconds -= .25f) {
            foreach (var p in world.S.Players) { p.Hunger = 100; p.Thirst = 100; }
            world.Tick(.25f);
        }
    }

    private static void CheckGuestActsThroughCommands(GameData data) {
        var (world, guest, heard) = Hosted(data);
        Require(heard.Contains("A neighbor joined the settlement."), "everyone should hear that a guest joined");
        Require(guest.Backpack.Count == 0 && guest.Pos == World.Spawn && guest.Warp == 1, "a guest without a saved player should start with an empty backpack at the clearing");
        var ore = world.Add("loose_iron", World.Spawn + new Vector2(5, 0), 2);
        var chest = world.Add("chest", World.Spawn + new Vector2(5, 2));
        int hostOre = world.S.Backpack["iron_ore"];
        string? hostHeard = null; world.Notify = message => hostHeard = message;

        world.Run(guest, new Command("pick_up", ore.Id, 0, "", 0, default));
        Require(guest.Backpack["iron_ore"] == 0 && world.Find(ore.Id) != null, "the host should refuse a pick-up the guest cannot reach");
        world.As(guest, () => world.MoveTo(ore.Pos + new Vector2(.5f, 0)));
        world.Run(guest, new Command("pick_up", ore.Id, 0, "", 0, default));
        Require(guest.Backpack["iron_ore"] == 2 && world.S.Backpack["iron_ore"] == hostOre && world.Find(ore.Id) == null, "a guest's pick-up should go into the guest's backpack only");
        Require(heard.Contains($"+2 {data.Name("iron_ore")}.") && hostHeard == null, "what a guest's action says should reach the guest, not the host");

        world.Run(guest, new Command("transfer", 0, chest.Id * World.BoxCodes + 1, "iron_ore", 99, default));
        Require(chest.Inventory["iron_ore"] == 2 && guest.Backpack["iron_ore"] == 0, "a guest should store into a chest within reach");
        world.Player = chest.Pos + new Vector2(1, 0);
        Require(world.Transfer(chest.Inventory, world.S.Backpack, "iron_ore", 99) == 2 && world.S.Backpack["iron_ore"] == hostOre + 2, "the host should take what a guest stored");
        world.Run(guest, new Command("transfer", chest.Id * World.BoxCodes + 2, 0, "iron_ore", 99, default));
        world.Run(guest, new Command("hire", chest.Id, 0, "", 99, default));
        Require(guest.Backpack.Count == 0 && world.S.Workers.Count == 0, "commands naming boxes or offers that do not exist should do nothing");

        world.RemoveGuest(GuestId);
        Require(world.S.Players.Count == 1 && heard.Contains("A neighbor left the settlement."), "a leaving guest should leave the world");
    }

    private static void CheckGuestCopyForwardsActions(GameData data) {
        var (host, guest, _) = Hosted(data);
        var copy = new World(data, false);
        var sent = new List<Command>();
        copy.Host = sent.Add;
        foreach (var world in new[] { host, copy }) world.Add("chest", World.Spawn + new Vector2(2, 2));
        foreach (var p in new[] { copy.S.Players[0], guest }) { p.Backpack.Add("berries", 3); p.Backpack.Add("wood", 20); p.Backpack.Add("stone", 4); p.Hunger = 50; }
        var chest = copy.S.Things.Single(t => t.Kind == "chest");

        copy.Consume("berries");
        Require(copy.Build("campfire", World.Spawn + new Vector2(30, 0)) == null && sent.Count == 1, "the copy should refuse a building out of reach itself");
        copy.Build("campfire", World.Spawn + new Vector2(-2, 2));
        copy.Transfer(copy.S.Backpack, chest.Inventory, "wood", 5);
        copy.SetFilter(chest, "wood", false, true);
        Require(copy.S.Backpack["berries"] == 3 && copy.S.Hunger == 50 && copy.S.Things.All(t => t.Kind != "campfire") && chest.Inventory.Count == 0 && chest.Inventory.InputFilter.Count == 0,
            "a guest's copy should leave its world to the host's updates");
        Require(sent.Select(c => c.Verb).SequenceEqual(new[] { "consume", "build", "transfer", "set_filter" }), $"the copy should send each action once, in order: {string.Join(", ", sent.Select(c => c.Verb))}");

        foreach (var command in sent) host.Run(guest, command);
        var hostChest = host.Find(chest.Id)!;
        Require(guest.Backpack["berries"] == 2 && guest.Hunger > 50 && host.S.Things.Any(t => t.Kind == "campfire") && guest.Backpack["wood"] == 20 - data.Buildings["campfire"].Cost["wood"] - 5,
            "the host should run the guest's actions as the guest");
        Require(hostChest.Inventory["wood"] == 5 && hostChest.Inventory.InputFilter.Contains("wood"), "the guest's transfer and filter should land in the host's chest");
        Require(host.S.Backpack["berries"] == 0 && host.S.Hunger == 100, "the host's own player should be untouched by a guest's actions");
    }

    private static void CheckGuestGathersAndHammers(GameData data) {
        var (world, guest, _) = Hosted(data);
        var tree = world.Add("tree", World.Spawn + new Vector2(4, 0), 40);
        guest.Backpack.Add("iron_axe", 1);
        world.As(guest, () => world.MoveTo(tree.Pos + new Vector2(.8f, 0)));
        guest.GatherTarget = tree.Id;
        Tick(world, data.GatherSeconds * 2 + .5f);
        Require(guest.Backpack["wood"] >= 4 && world.S.Backpack["wood"] == 0 && tree.Remaining < 40, "a guest holding on to a tree should chop wood into their own backpack");
        guest.GatherTarget = 0;

        var anvil = world.Add("anvil", World.Spawn + new Vector2(-6, 0));
        world.SetRecipe(anvil, "iron_parts");
        anvil.Inventory.Add("iron_bar", 1);
        world.Player = World.Spawn + new Vector2(20, 0);
        Tick(world, 1);
        Require(!anvil.Processing, "an anvil nobody stands at should not start");
        world.As(guest, () => world.MoveTo(anvil.Pos + new Vector2(1, 0)));
        Tick(world, data.Recipes["iron_parts"].Seconds + 1);
        Require(anvil.Inventory["iron_parts"] == 1, "a guest standing at an anvil should hammer it");
    }

    private static void CheckEachPlayerHasTheirOwnPack(GameData data) {
        var (world, guest, heard) = Hosted(data);
        world.Player = World.Spawn;
        var far = Enumerable.Range(0, 63).Select(i => World.Spawn + Vector2.Right.Rotated(i * .1f) * 35).First(world.CanStand);
        world.As(guest, () => world.MoveTo(far));
        world.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        for (int i = 0; i < 4; i++) { world.Tick(.25f); world.S.Health = 100; guest.Health = 100; }
        var hostPack = world.S.Wolves.Where(w => w.Target == world.Me.Id).ToList();
        var guestPack = world.S.Wolves.Where(w => w.Target == GuestId).ToList();
        Require(hostPack.Count > 0 && guestPack.Count > 0, "each player out at night should draw their own wolves");
        Require(guestPack.All(w => w.Pos.DistanceTo(guest.Pos) < 13) && hostPack.All(w => w.Pos.DistanceTo(world.Player) < 13), "wolves should come out near the player they hunt");
        Require(heard.Any(m => m.StartsWith("A wolf is prowling")), "the guest should be warned about their own wolves");

        guest.Backpack.Add("stone", 2); guest.Health = -1;
        world.Tick(.25f);
        Require(guest.RespawnIn > 0 && world.S.RespawnIn == 0, "only the guest should die");
        Require(world.S.Wolves.All(w => w.Target != GuestId) && world.S.Wolves.Any(w => w.Target == world.Me.Id), "a guest's collapse should send off only their own pack");
        Require(world.S.Things.Any(t => t.Kind == "grave" && t.Inventory["stone"] == 2 && t.Pos.DistanceTo(far) < 2) && guest.Backpack.Count == 0, "a guest's belongings should stay where they fell");
        int warp = guest.Warp;
        for (float t = 0; t <= data.RespawnSeconds + .5f; t += .25f) { world.Tick(.25f); world.S.Health = 100; }
        Require(guest.RespawnIn == 0 && guest.Pos == World.Spawn && guest.Warp == warp + 1, "a guest should respawn at the clearing, and the move should tell their machine");
    }

    private static void CheckSleepWaitsForEveryone(GameData data) {
        var (world, guest, _) = Hosted(data);
        world.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        var hut = world.Add("hut", World.Spawn + new Vector2(3, 0));
        world.Player = hut.Pos + new Vector2(1.5f, 0);
        Require(world.Enter(hut), "the host should step into the cabin");
        Require(world.SleepBlock() == "Everyone has to be inside a cabin to sleep.", $"sleeping should wait for a guest still outside: {world.SleepBlock()}");
        world.As(guest, () => world.MoveTo(hut.Pos + new Vector2(0, 2)));
        world.Run(guest, new Command("enter", hut.Id, 0, "", 0, default));
        guest.Hunger = 1;
        Require(world.SleepBlock() == "Someone is too hungry or thirsty to sleep.", "sleeping should wait for a hungry guest");
        guest.Hunger = 100;
        world.S.Workers.Add(new Worker { Id = world.S.NextId++, Name = "Paid", Wage = 10, Pos = World.Spawn });
        world.S.Backpack.Add(Inventory.Coins, 50); guest.Backpack.Add(Inventory.Coins, 50);
        int day = world.Day;
        world.Run(guest, new Command("sleep", hut.Id, 0, "", 0, default));
        Require(world.Day == day + 1 && !world.Night && guest.Hunger < 100, "with everyone inside, a guest's sleep should pass the night for all of them");
        Require(world.S.Backpack[Inventory.Coins] == 40 && guest.Backpack[Inventory.Coins] == 50, "the host's backpack should pay the wages even when a guest sleeps");
    }

    private static void CheckGuestSaveRoundTrip(GameData data) {
        var (world, guest, _) = Hosted(data);
        guest.Backpack.Add("coal", 5);
        world.As(guest, () => world.MoveTo(World.Spawn + new Vector2(6, 1)));
        var crate = world.Add("crate", guest.Pos + new Vector2(1, 0));
        crate.Inventory.Add("steel_axe", 2);
        Require(world.As(guest, () => world.PickUpCrate(crate)), "a guest should pick up a crate");
        string packed = guest.Backpack.Items.Keys.Single(World.IsCrate);
        guest.HotbarSlots[3] = packed;
        string saved = world.ExportPlayer(guest);

        var hostSave = world.Serialize();
        Require(!hostSave.Contains("\"coal\": 5") && guest.Crates == null, "the host's save should keep the guest out, and exporting should not change the guest");
        world.Deserialize(hostSave);
        Require(world.S.Players.Count == 1, "a loaded world should start without guests");

        world.RemoveGuest(GuestId);
        var other = new World(data, false);
        other.Add("chest", World.Spawn + new Vector2(2, 2)); other.S.NextId += 50;
        var back = other.AddGuest(GuestId, saved);
        string renamed = back.Backpack.Items.Keys.Single(World.IsCrate);
        Require(back.Backpack["coal"] == 5 && back.Pos == World.Spawn + new Vector2(6, 1), "a guest should come back with what they carried, where they were");
        Require(renamed != packed && other.Packed(renamed)?["steel_axe"] == 2 && back.HotbarSlots[3] == renamed, "a guest's packed crate should come back with its contents under a number free in this world");
    }

    private static void CheckBrokenGuestSavesStartEmpty(GameData data) {
        var world = new World(data, false);
        var good = new PlayerState { Health = 80 };
        good.Backpack.Add("wood", 3);
        string Json(PlayerState p) => System.Text.Json.JsonSerializer.Serialize(p, GameData.Json);
        Require(world.AddGuest(10, Json(good)).Backpack["wood"] == 3, "a sound saved player should come back as saved");
        var unknown = new PlayerState(); unknown.Backpack.Items["diamond"] = 2;
        var crowded = new PlayerState(); for (int i = 0; i < 30; i++) crowded.Backpack.Items[$"{World.PackedPrefix}{900 + i}"] = 1;
        var underwater = new PlayerState { PlayerX = Terrain.Lake.X, PlayerY = Terrain.Lake.Y }; underwater.Backpack.Add("stone", 1);
        foreach (var (saved, why) in new[] { ("{ not json", "garbage"), ("null", "an empty save"), (Json(unknown), "an unknown item"), (Json(crowded), "crates without contents"), (Json(new PlayerState { Health = 0 }), "a dead player"), (Json(new PlayerState { Hunger = 900 }), "hunger out of range") }) {
            var p = world.AddGuest(20, saved);
            Require(p.Backpack.Count == 0 && p.Pos == World.Spawn && p.Health == 100, $"{why} should give an empty backpack at the clearing");
            world.RemoveGuest(20);
        }
        var drowned = world.AddGuest(21, Json(underwater));
        Require(drowned.Backpack["stone"] == 1 && drowned.Pos == World.Spawn, "a saved spot nobody can stand on should move the guest to the clearing");
    }
}
