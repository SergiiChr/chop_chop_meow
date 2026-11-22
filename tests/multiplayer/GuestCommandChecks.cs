using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

public static class GuestCommandChecks {
    /// <summary>One sequence checks that guest actions reach the host in order and only the host applies them.</summary>
    public static void CheckGuestCopyForwardsActions(GameData data) {
        var (host, guest, _) = Hosted(data);
        var copy = new World(data, false);
        var sent = new List<Command>();
        copy.Host = sent.Add;
        foreach (var world in new[] { host, copy }) world.Add(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 2));
        foreach (var p in new[] { copy.Players[0], guest }) { Give(p, "berries", 3); Give(p, "wood", 20); Give(p, "stone", 4); SetNeeds(p, hunger: 50); }
        var chest = copy.Things.Single(t => t.Kind == ThingKind.Chest);

        copy.Consume("berries");
        Require(copy.Construction.Build(ThingKind.Campfire, Terrain.Spawn + new Vector2(30, 0)) == null && sent.Count == 1, "the copy should refuse a building out of reach itself");
        copy.Construction.Build(ThingKind.Campfire, Terrain.Spawn + new Vector2(-2, 2), Mathf.Pi / 2);
        copy.Transfer(copy.Backpack, chest.Inventory, "wood", 5);
        copy.SetFilter(chest, "wood", false, true);
        Require(copy.Backpack["berries"] == 3, "a guest's copy should keep its own backpack berries");
        Require(copy.Hunger == 50, "a guest's copy should keep its own hunger");
        Require(copy.Things.All(t => t.Kind != ThingKind.Campfire), "a guest's copy should not add a campfire to its world");
        Require(chest.Inventory.Count == 0, "a guest's copy should not put anything in the host's chest");
        Require(chest.Inventory.InputFilter.Count == 0, "a guest's copy should not set a filter on the host's chest");
        Require(sent.Select(c => c.Verb).SequenceEqual(new[] { Verb.Consume, Verb.Build, Verb.Transfer, Verb.SetFilter }), $"the copy should send each action once, in order: {string.Join(", ", sent.Select(c => c.Verb))}");

        foreach (var command in sent) host.Run(guest, command);
        var hostChest = host.Find(chest.Id)!;
        Require(guest.Backpack["berries"] == 2, "the host should run the guest's eating on the guest's berries");
        Require(guest.Hunger > 50, "the host should run the guest's eating on the guest's hunger");
        Require(host.Things.Any(t => t.Kind == ThingKind.Campfire && t.Rotation == Mathf.Pi / 2), "the host should put up the guest's building at the angle the guest chose");
        Require(guest.Backpack["wood"] == 9, "the host should take the guest's wood for the building and the transfer");
        Require(hostChest.Inventory["wood"] == 5 && hostChest.Inventory.InputFilter.Contains("wood"), "the guest's transfer and filter should land in the host's chest");
        Require(host.Backpack["berries"] == 0 && host.Hunger == 100, "the host's own player should be untouched by a guest's actions");
    }
}
