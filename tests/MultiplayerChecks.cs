using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

/// <summary>
/// Deterministic checks for players sharing one world: guests act through commands the host runs as them,
/// each player has their own needs, pack and messages, and a guest's saved player is checked before it comes in.
/// The real two-process session is covered by the network check.
/// </summary>
public static class MultiplayerChecks {
    public static void Run(GameData data) {
        CheckGuestActsThroughCommands(data);
        CheckGuestCopyForwardsActions(data);
        CheckGuestGathersAndHammers(data);
        CheckEachPlayerHasTheirOwnPack(data);
        CheckSleepWaitsForEveryone(data);
        CheckGuestComesBackFromSaveWithTheirPackAndSpot(data);
        CheckBrokenGuestSavesStartEmpty(data);
        CheckGuestInventoryAndRecoveryCommandsLeaveOthersAlone(data);
    }

    private static void CheckGuestInventoryAndRecoveryCommandsLeaveOthersAlone(GameData data) {
        var (world, guest, _) = Hosted(data);
        guest.Backpack.Add("wood", 10); guest.Backpack.Add("stone", 2);
        world.Run(guest, new Command(Verb.TransferSlot, 0, 0, "wood", 4, new Vector2(0, 7)));
        Require(guest.Backpack.Layout[7].Item == "wood" && guest.Backpack.Layout[7].Count == 4 && guest.Backpack["wood"] == 10,
            "a guest's explicit slot move should preserve all units and land in the requested slot");
        world.Run(guest, new Command(Verb.TransferSlot, 0, 0, "stone", 1, new Vector2(0, 8)));
        Require(guest.Backpack.Layout[8].Item == "", "a stale drag must not move a different item now in its source slot");
        world.Run(guest, new Command(Verb.SortBag, 0, 0, "", 0, default));
        Require(guest.Backpack.Layout[0].Item == "stone" && guest.Backpack.Layout[1].Item == "wood" && guest.Backpack.Layout[1].Count == 10,
            "guest sorting should stack and sort only that player's backpack");
        Require(world.Backpack.Count == 0, "a guest's inventory commands must not change the host's bag");
        world.Run(guest, new Command(Verb.TransferSlot, 0, 0, "wood", 10, new Vector2(1, 9)));
        var returningWorld = new World(data, false);
        var returning = returningWorld.AddGuest(GuestId, world.ExportPlayer(guest));
        Require(returning.Backpack.Layout[9].Item == "wood" && returning.Backpack.Layout[0].Item == "stone",
            "a guest leaving and rejoining must retain their exact inventory positions");
        var legacy = new PlayerState { Backpack = { SavedItems = new Dictionary<string, int> { ["water"] = 64, ["wood"] = 1 } } };
        // An old save holds item totals and no stacks.
        var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(legacy, GameData.Json))!;
        Require(legacyJson["Backpack"]!.AsObject().Remove("Layout"), "the legacy fixture must drop the saved stacks");
        var legacyWorld = new World(data, false);
        var migrated = legacyWorld.AddGuest(GuestId, legacyJson.ToJsonString());
        Require(migrated.Backpack["wood"] == 1 && migrated.Backpack["water"] + legacyWorld.Things.Where(t => t.Kind == ThingKind.Grave).Sum(t => t.Inventory["water"]) == 64,
            "a legacy guest's stacked bottles must migrate without rejecting or losing the rest of their bag");

        var copy = new World(data, false); Give(copy, "wood", 10);
        var sent = new List<Command>(); copy.Host = sent.Add;
        copy.TransferSlot(copy.Backpack, copy.Backpack, 0, 4, "wood", 3); copy.SortBackpack();
        Require(sent.Select(c => c.Verb).SequenceEqual(new[] { Verb.TransferSlot, Verb.SortBag }) && copy.Backpack.Layout[0].Count == 10,
            "a guest copy must forward layout actions without changing its local mirror");

        var dropper = new World(data, false); var shelf = dropper.Add(ThingKind.Chest, dropper.Player + new Vector2(1, 0));
        shelf.Inventory.Add("stone", 1); Give(dropper, "wood", 3);
        int woodSlot = dropper.Backpack.Layout.FindIndex(slot => slot.Item == "wood");
        Require(dropper.TransferSlot(dropper.Backpack, shelf.Inventory, woodSlot, 0, "wood", 3) == 3 && shelf.Inventory["wood"] == 3 && shelf.Inventory.Layout[0].Item == "stone",
            "a drop on another item's cell in a chest should store the stack in a free cell");

        RunUntilPhase(world, .9f); guest.RespawnIn = 20;
        float time = world.Time;
        world.Run(guest, new Command(Verb.SkipMorning, 0, 0, "", 0, default));
        Require(world.Time == time, "a guest must not skip the night while the host lives");
        world.Die();
        world.Run(guest, new Command(Verb.SkipMorning, 0, 0, "", 0, default));
        Require(world.Time > time && guest.RespawnIn == 0 && world.RespawnIn == 0,
            "a downed guest should be able to skip when the host is down too, in that guest's action context");
    }

    private static void CheckGuestActsThroughCommands(GameData data) {
        var (world, guest, heard) = Hosted(data);
        Require(heard.Contains("A neighbor joined the settlement."), "everyone should hear that a guest joined");
        Require(guest.Backpack.Count == 0 && guest.Pos == Terrain.Spawn && guest.Warp == 1, "a guest without a saved player should start with an empty backpack at the clearing");
        var lump = world.Add(ThingKind.LooseCoal, Terrain.Spawn + new Vector2(5, 0), 2);
        var chest = world.Add(ThingKind.Chest, Terrain.Spawn + new Vector2(5, 2));
        int hostCoal = world.Carried("coal");
        string? hostHeard = null; world.Notify = message => hostHeard = message;

        world.Run(guest, new Command(Verb.PickUp, lump.Id, 0, "", 0, default));
        Require(guest.Backpack["coal"] == 0 && world.Find(lump.Id) != null, "the host should refuse a pick-up the guest cannot reach");
        world.As(guest, () => world.MoveTo(lump.Pos + new Vector2(.5f, 0)));
        world.Run(guest, new Command(Verb.PickUp, lump.Id, 0, "", 0, default));
        Require(guest.Backpack["coal"] == 2 && world.Carried("coal") == hostCoal && world.Find(lump.Id) == null, "a guest's pick-up should go into the guest's backpack only");
        world.Run(guest, new Command(Verb.Consume, 0, 0, "raw_meat", 0, default));
        Require(heard.Contains($"{data.Name("raw_meat")} needs cooking first.") && hostHeard == null, "what a guest's action says should reach the guest, not the host");

        world.Run(guest, new Command(Verb.Transfer, 0, chest.Id * World.BoxCodes + 1, "coal", 99, default));
        Require(chest.Inventory["coal"] == 2 && guest.Backpack["coal"] == 0, "a guest should store into a chest within reach");
        world.Player = chest.Pos + new Vector2(1, 0);
        Require(world.Transfer(chest.Inventory, world.Backpack, "coal", 99) == 2 && world.Carried("coal") == hostCoal + 2, "the host should take what a guest stored");
        world.Run(guest, new Command(Verb.Transfer, chest.Id * World.BoxCodes + 2, 0, "coal", 99, default));
        world.Run(guest, new Command(Verb.Hire, chest.Id, 0, "", 99, default));
        Require(guest.Backpack.Count == 0 && world.Workers.Count == 0, "commands naming boxes or offers that do not exist should do nothing");

        world.RemoveGuest(GuestId);
        Require(world.Players.Count == 1 && heard.Contains("A neighbor left the settlement."), "a leaving guest should leave the world");
    }

    private static void CheckGuestCopyForwardsActions(GameData data) {
        var (host, guest, _) = Hosted(data);
        var copy = new World(data, false);
        var sent = new List<Command>();
        copy.Host = sent.Add;
        foreach (var world in new[] { host, copy }) world.Add(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 2));
        foreach (var p in new[] { copy.Players[0], guest }) { p.Backpack.Add("berries", 3); p.Backpack.Add("wood", 20); p.Backpack.Add("stone", 4); p.Hunger = 50; }
        var chest = copy.Things.Single(t => t.Kind == ThingKind.Chest);

        copy.Consume("berries");
        Require(copy.Build(ThingKind.Campfire, Terrain.Spawn + new Vector2(30, 0)) == null && sent.Count == 1, "the copy should refuse a building out of reach itself");
        copy.Build(ThingKind.Campfire, Terrain.Spawn + new Vector2(-2, 2));
        copy.Transfer(copy.Backpack, chest.Inventory, "wood", 5);
        copy.SetFilter(chest, "wood", false, true);
        Require(copy.Carried("berries") == 3, "a guest's copy should keep its own backpack berries");
        Require(copy.Hunger == 50, "a guest's copy should keep its own hunger");
        Require(copy.Things.All(t => t.Kind != ThingKind.Campfire), "a guest's copy should not add a campfire to its world");
        Require(chest.Inventory.Count == 0, "a guest's copy should not put anything in the host's chest");
        Require(chest.Inventory.InputFilter.Count == 0, "a guest's copy should not set a filter on the host's chest");
        Require(sent.Select(c => c.Verb).SequenceEqual(new[] { Verb.Consume, Verb.Build, Verb.Transfer, Verb.SetFilter }), $"the copy should send each action once, in order: {string.Join(", ", sent.Select(c => c.Verb))}");

        foreach (var command in sent) host.Run(guest, command);
        var hostChest = host.Find(chest.Id)!;
        Require(guest.Backpack["berries"] == 2, "the host should run the guest's eating on the guest's berries");
        Require(guest.Hunger > 50, "the host should run the guest's eating on the guest's hunger");
        Require(host.Things.Any(t => t.Kind == ThingKind.Campfire), "the host should run the guest's building in the host's world");
        Require(guest.Backpack["wood"] == 9, "the host should take the guest's wood for the building and the transfer");
        Require(hostChest.Inventory["wood"] == 5 && hostChest.Inventory.InputFilter.Contains("wood"), "the guest's transfer and filter should land in the host's chest");
        Require(host.Carried("berries") == 0 && host.Hunger == 100, "the host's own player should be untouched by a guest's actions");
    }

    private static void CheckGuestGathersAndHammers(GameData data) {
        var (world, guest, _) = Hosted(data);
        var tree = world.Add(ThingKind.Tree, Terrain.Spawn + new Vector2(4, 0), 40);
        guest.Backpack.Add("steel_axe", 1);
        world.As(guest, () => world.MoveTo(tree.Pos + new Vector2(.8f, 0)));
        guest.GatherTarget = tree.Id;
        FedPlayersTick(world, data.GatherSeconds * 2 + .5f);
        Require(guest.Backpack["wood"] >= 4 && world.Carried("wood") == 0 && tree.Remaining < 40, "a guest holding on to a tree should chop wood into their own backpack");
        guest.GatherTarget = 0;

        var anvil = world.Add(ThingKind.Anvil, Terrain.Spawn + new Vector2(-6, 0));
        world.SetRecipe(anvil, "steel_parts");
        anvil.Inventory.Add("steel_bar", 1);
        world.Player = Terrain.Spawn + new Vector2(20, 0);
        FedPlayersTick(world, 1);
        Require(!anvil.Processing, "an anvil nobody stands at should not start");
        world.As(guest, () => world.MoveTo(anvil.Pos + new Vector2(1, 0)));
        FedPlayersTick(world, data.Recipes["steel_parts"].Seconds + 1);
        Require(anvil.Inventory["steel_parts"] == 1, "a guest standing at an anvil should hammer it");
    }

    private static void CheckEachPlayerHasTheirOwnPack(GameData data) {
        var (world, guest, _) = Hosted(data);
        world.Player = Terrain.Spawn;
        var far = Enumerable.Range(0, 63).Select(i => Terrain.Spawn + Vector2.Right.Rotated(i * .1f) * 35).First(world.CanStand);
        world.As(guest, () => world.MoveTo(far));
        RunUntilNight(world);
        world.Tick(.25f);
        Require(world.Wolves.Count > 0 && world.Wolves.All(w => world.Players.All(p => p.Pos.DistanceTo(w.Pos) > data.SightRadius - 1)),
                "wolves should come out beyond every player's sight, never on screen");
        for (int i = 0; i < 3; i++) { world.Tick(.25f); SetNeeds(world, health: 100); guest.Health = 100; }
        var hostPack = world.Wolves.Where(w => w.Target == world.Me.Id).ToList();
        var guestPack = world.Wolves.Where(w => w.Target == GuestId).ToList();
        Require(hostPack.Count > 0 && guestPack.Count > 0, "each player out at night should draw their own wolves");
        Require(guestPack.All(w => w.Pos.DistanceTo(guest.Pos) < data.SightRadius + 8) && hostPack.All(w => w.Pos.DistanceTo(world.Player) < data.SightRadius + 8), "wolves should come out near the player they hunt");

        guest.Backpack.Add("stone", 2); guest.Health = -1;
        world.Tick(.25f);
        Require(guest.RespawnIn > 0 && world.RespawnIn == 0, "only the guest should die");
        Require(world.Wolves.All(w => w.Target != GuestId) && world.Wolves.Any(w => w.Target == world.Me.Id), "a guest's collapse should send off only their own pack");
        Require(world.Things.Any(t => t.Kind == ThingKind.Grave && t.Inventory["stone"] == 2 && t.Pos.DistanceTo(far) < 2) && guest.Backpack.Count == 0, "a guest's belongings should stay where they fell");
        int warp = guest.Warp;
        for (float t = 0; t <= data.RespawnSeconds + .5f; t += .25f) { world.Tick(.25f); SetNeeds(world, health: 100); }
        Require(guest.RespawnIn == 0 && guest.Pos == Terrain.Spawn && guest.Warp == warp + 1, "a guest should respawn at the clearing, and the move should tell their machine");
    }

    private static void CheckSleepWaitsForEveryone(GameData data) {
        var (world, guest, _) = Hosted(data);
        RunUntilNight(world);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(3, 0));
        world.Player = hut.Pos + new Vector2(1.5f, 0);
        Require(world.Enter(hut), "the host should step into the cabin");
        Require(world.SleepBlock() == "Everyone has to be inside a cabin to sleep.", $"sleeping should wait for a guest still outside: {world.SleepBlock()}");
        world.As(guest, () => world.MoveTo(hut.Pos + new Vector2(0, 2)));
        world.Run(guest, new Command(Verb.Enter, hut.Id, 0, "", 0, default));
        guest.Hunger = 1;
        Require(world.SleepBlock() == "Someone is too hungry or thirsty to sleep.", "sleeping should wait for a hungry guest");
        guest.Hunger = 100;
        AddWorker(world, Terrain.Spawn).Wage = 10;
        Give(world, Inventory.Coins, 50); guest.Backpack.Add(Inventory.Coins, 50);
        int day = world.Day;
        world.Run(guest, new Command(Verb.Sleep, hut.Id, 0, "", 0, default));
        Require(world.Day == day + 1 && !world.Night && guest.Hunger < 100, "with everyone inside, a guest's sleep should pass the night for all of them");
        Require(world.Carried(Inventory.Coins) == 40 && guest.Backpack[Inventory.Coins] == 50, "the host's backpack should pay the wages even when a guest sleeps");
    }

    private static void CheckGuestComesBackFromSaveWithTheirPackAndSpot(GameData data) {
        var (world, guest, _) = Hosted(data);
        guest.Backpack.Add("coal", 5);
        world.As(guest, () => world.MoveTo(Terrain.Spawn + new Vector2(6, 1)));
        var crate = world.Add(ThingKind.Crate, guest.Pos + new Vector2(1, 0));
        crate.Inventory.Add("steel_axe", 2);
        Require(world.As(guest, () => world.PickUpCrate(crate)), "a guest should pick up a crate");
        string packed = guest.Backpack.Items.Keys.Single(World.IsCrate);
        guest.HotbarSlots[3] = packed;
        string saved = world.ExportPlayer(guest);

        var hostSave = world.Serialize();
        Require(!hostSave.Contains("\"coal\": 5") && guest.Crates == null, "the host's save should keep the guest out, and exporting should not change the guest");
        world.Deserialize(hostSave);
        Require(world.Players.Count == 1, "a loaded world should start without guests");

        world.RemoveGuest(GuestId);
        var other = new World(data, false);
        for (int i = 0; i < 50; i++) other.Add(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 2));
        var back = other.AddGuest(GuestId, saved);
        string renamed = back.Backpack.Items.Keys.Single(World.IsCrate);
        Require(back.Backpack["coal"] == 5 && back.Pos == Terrain.Spawn + new Vector2(6, 1), "a guest should come back with what they carried, where they were");
        Require(renamed != packed && other.Packed(renamed)?["steel_axe"] == 2 && back.HotbarSlots[3] == renamed, "a guest's packed crate should come back with its contents under a number free in this world");
    }

    private static void CheckBrokenGuestSavesStartEmpty(GameData data) {
        var world = new World(data, false);
        var good = new PlayerState { Health = 80 };
        good.Backpack.Add("wood", 3);
        string Json(PlayerState p) => System.Text.Json.JsonSerializer.Serialize(p, GameData.Json);
        Require(world.AddGuest(10, Json(good)).Backpack["wood"] == 3, "a sound saved player should come back as saved");
        var unknown = new PlayerState(); unknown.Backpack.SavedItems = new Dictionary<string, int> { ["diamond"] = 2 };
        var crowded = new PlayerState(); crowded.Backpack.SavedItems = Enumerable.Range(0, 30).ToDictionary(i => $"{World.PackedPrefix}{900 + i}", _ => 1);
        var underwater = new PlayerState { PlayerX = Terrain.Lake.X, PlayerY = Terrain.Lake.Y }; underwater.Backpack.Add("stone", 1);
        foreach (var (saved, why) in new[] { ("{ not json", "garbage"), ("null", "an empty save"), (Json(unknown), "an unknown item"), (Json(crowded), "crates without contents"), (Json(new PlayerState { Health = 0 }), "a dead player"), (Json(new PlayerState { Hunger = 900 }), "hunger out of range") }) {
            var p = world.AddGuest(20, saved);
            Require(p.Backpack.Count == 0 && p.Pos == Terrain.Spawn && p.Health == 100, $"{why} should give an empty backpack at the clearing");
            world.RemoveGuest(20);
        }
        var drowned = world.AddGuest(21, Json(underwater));
        Require(drowned.Backpack["stone"] == 1 && drowned.Pos == Terrain.Spawn, "a saved spot nobody can stand on should move the guest to the clearing");
    }
}
