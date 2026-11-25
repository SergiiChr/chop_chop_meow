using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Moves one kind of liquid at a time through connected steel pipes.</summary>
public sealed class Plumbing(World world, Barrels barrels) {
    public const int InstallCost = 10;
    private static readonly ThingKind[] Consumers = Array.FindAll(Kinds.All, HasPlumbing);

    /// <summary>Whether this kind of building can receive pumped water.</summary>
    public static bool HasPlumbing(ThingKind kind) =>
        kind is ThingKind.Barrel or ThingKind.Hut or ThingKind.Cooktable or ThingKind.Cannery or ThingKind.Farm;

    private sealed class FillRoute {
        public readonly List<int> Tiles = [];
        public int Next;
    }

    private sealed class Network {
        public bool Active, PumpRunning, BarrelRunning;
        public string? Liquid;
        public int TileCount;
        public readonly List<Thing> Tanks = [];
        public double Output, Stored;
        public int Unfilled;
        public readonly Stack<int> Wet = new();
        public readonly Dictionary<int, FillRoute> Routes = new();
        public readonly List<Thing> Receivers = [];
    }

    private readonly Dictionary<int, Network> networks = new();
    private readonly HashSet<int> visited = [];
    private readonly List<int> component = [];
    private readonly List<Network> active = [], groups = [];
    private int revision = -1, contentsRevision = -1;

    internal (float Liters, float Pressure) Reading(Vector2I tile) {
        RefreshNetworks();
        if (NetworkAt(tile) is not Network network) return default;
        float pressure = 0;
        foreach (var pump in world.ThingsOf(ThingKind.Pump))
            if (network.Liquid != BerryJam.Liquid && pump.Pressure > pressure && NetworkAt(Terrain.TileAt(pump.Pos)) == network)
                pressure = pump.Pressure;
        foreach (var barrel in world.ThingsOf(ThingKind.Barrel)) {
            if (!barrel.PlumbingInstalled || NetworkAt(Terrain.TileAt(barrel.Pos)) != network) continue;
            string liquid = Barrels.LiquidIn(barrel);
            if (network.Liquid == null || network.Liquid == liquid)
                pressure = Math.Max(pressure, (float)(BarrelRate(barrel.Inventory.Free(liquid)) / .05));
        }
        return ((float)world.SteelPipes.LitersAt(Terrain.TileIndex(tile)), pressure);
    }

    /// <summary>Whether a pipe would join lines that already hold different liquids.</summary>
    internal bool WouldMix(Vector2I tile, int connections) {
        if (connections == 0 || (connections & (connections - 1)) == 0) return false;
        RefreshNetworks();
        string? liquid = null;
        foreach (var (offset, port, _) in SteelPipes.Sides) {
            if ((connections & port) == 0 || NetworkAt(tile + offset) is not { Stored: > 0, Liquid: string next }) continue;
            if (liquid != null && liquid != next) return true;
            liquid = next;
        }
        return false;
    }

    internal void Tick(float dt) {
        RefreshNetworks();
        foreach (var pump in world.ThingsOf(ThingKind.Pump)) {
            float liters = WaterPumps.Advance(pump, dt);
            var inlet = Terrain.TileAt(pump.Pos);
            if (liters <= 0 || NetworkAt(inlet) is not Network network || !SelectLiquid(network, Water.Liquid)) continue;
            Activate(network);
            network.PumpRunning = true;
            network.Output += Fill(network, Terrain.TileIndex(inlet), liters);
        }

        foreach (var barrel in world.ThingsOf(ThingKind.Barrel)) {
            if (!barrel.PlumbingInstalled || NetworkAt(Terrain.TileAt(barrel.Pos)) is not Network network) continue;
            string liquid = Barrels.LiquidIn(barrel);
            if (barrel.Inventory.Free(liquid) > 0 && SelectLiquid(network, liquid)) network.BarrelRunning = true;
            network.Tanks.Add(barrel);
            Activate(network);
        }

        foreach (var kind in Consumers)
            foreach (var receiver in world.ThingsOf(kind)) {
                receiver.ReceivingWater = false;
                if (NetworkAt(Terrain.TileAt(receiver.Pos)) is not { Liquid: string liquid } network
                    || !network.PumpRunning && network.BarrelRunning && receiver.Kind == ThingKind.Barrel
                    || Demand(receiver, liquid) <= 0) continue;
                Activate(network);
                network.Receivers.Add(receiver);
            }

        foreach (var network in active) {
            if (network.Liquid is string liquid) {
                if (network.PumpRunning) Share(network, network.Output, liquid);
                else if (!FeedFromBarrels(network, liquid, dt)) Drain(network, network.Stored - Share(network, network.Stored, liquid));
                if (!network.PumpRunning) Balance(network, liquid, dt);
            }
            network.Output = 0;
            network.PumpRunning = network.BarrelRunning = false;
            network.Tanks.Clear();
            network.Receivers.Clear();
            network.Active = false;
            if (network.Stored == 0) network.Liquid = null;
        }
        active.Clear();
    }

    private static bool SelectLiquid(Network network, string liquid) {
        if (network.Liquid != null) return network.Liquid == liquid;
        network.Liquid = liquid;
        return true;
    }

    private static double BarrelRate(int liters) => liters > 0 ? 1 + liters / 10.0 : 0;

    /// <summary>Each barrel can feed a machine through the pipes, faster when it is fuller.</summary>
    private bool FeedFromBarrels(Network network, string liquid, float dt) {
        bool feeding = false;
        foreach (var tank in network.Tanks) {
            int available = tank.Inventory.Free(liquid);
            if (available == 0 || network.Receivers.Count == 0) continue;
            double capacity = network.TileCount * SteelPipes.Capacity - network.Stored;
            foreach (var receiver in network.Receivers) capacity += Demand(receiver, liquid);
            // With less than a liter of room, keep delivering from the standing liquid.
            if (capacity < 1) continue;
            feeding = true;
            double credit = tank.PipeFeedCredit + BarrelRate(available) * dt;
            double whole = Math.Floor(credit);
            int units = (int)Math.Min(Math.Floor(capacity), Math.Min(available, whole));
            tank.PipeFeedCredit = credit - whole;
            if (units <= 0) continue;
            int sent = tank.Inventory.Remove(liquid, units);
            Share(network, Fill(network, Terrain.TileIndex(Terrain.TileAt(tank.Pos)), sent), liquid);
        }
        return feeding;
    }

    /// <summary>Pressure difference moves whole liters from the fullest barrel to the emptiest, without overshooting their midpoint.</summary>
    private void Balance(Network network, string liquid, float dt) {
        Thing? from = null, to = null;
        int high = 0, low = Barrel.Capacity;
        foreach (var tank in network.Tanks) {
            int liters = tank.Inventory[liquid];
            if (liters > high && tank.Inventory.Free(liquid) > 0) { high = liters; from = tank; }
            if (liters < low && TankAccepts(tank, liquid)) { low = liters; to = tank; }
        }
        if (from == null || to == null || high - low < 2) return;
        double credit = from.PipeBalanceTarget == to.Id ? from.PipeBalanceCredit : 0;
        from.PipeBalanceTarget = to.Id;
        credit += (BarrelRate(high) - BarrelRate(low)) * dt;
        double whole = Math.Floor(credit);
        from.PipeBalanceCredit = credit - whole;
        int units = (int)Math.Min((high - low) / 2, Math.Min(from.Inventory.Free(liquid), whole));
        if (units == 0) return;
        int put = to.Inventory.Add(liquid, units);
        from.Inventory.Remove(liquid, put);
        world.PileFilters(to);
    }

    private static bool TankAccepts(Thing tank, string liquid) => Barrels.CanHold(tank, liquid)
        && (tank.Inventory.InputFilter.Count == 0 || tank.Inventory.Accepts(liquid));

    private void Activate(Network network) {
        if (network.Active) return;
        network.Active = true;
        active.Add(network);
    }

    private Network? NetworkAt(Vector2I tile) =>
        Terrain.OnMap(tile) ? networks.GetValueOrDefault(Terrain.TileIndex(tile)) : null;

    /// <summary>Labels each connected set of pipe tiles again when the underground layer changes.</summary>
    private void RefreshNetworks() {
        var pipes = world.SteelPipes;
        int topology = pipes.Revision, contents = world.Remote ? pipes.WaterRevision : -1;
        if (revision == topology) {
            if (contentsRevision != contents) ReadGuestContents(pipes);
            contentsRevision = contents;
            return;
        }
        revision = topology;
        contentsRevision = contents;
        networks.Clear();
        groups.Clear();
        foreach (int index in pipes.Tiles) {
            if (networks.ContainsKey(index)) continue;
            var network = new Network();
            groups.Add(network);
            ConnectedTiles(index, component);
            network.TileCount = component.Count;
            foreach (int tile in component) {
                networks.Add(tile, network);
                double stored = pipes.LitersAt(tile);
                network.Liquid ??= pipes.LiquidAt(tile);
                network.Stored += stored;
                if (stored > 0) network.Wet.Push(tile);
                if (stored < SteelPipes.Capacity) network.Unfilled++;
            }
        }
    }

    /// <summary>A guest receives standing amounts without rebuilding the connected groups.</summary>
    private void ReadGuestContents(SteelPipes pipes) {
        foreach (var network in groups) { network.Stored = 0; network.Liquid = null; }
        foreach (int index in pipes.Tiles) if (pipes.LiquidAt(index) is string liquid) {
            var network = networks[index];
            network.Stored += pipes.LitersAt(index);
            network.Liquid = liquid;
        }
    }

    /// <summary>Lists connected pipes in order of distance along the line from the starting tile.</summary>
    private void ConnectedTiles(int start, List<int> tiles) {
        tiles.Clear(); tiles.Add(start);
        visited.Clear(); visited.Add(start);
        for (int i = 0; i < tiles.Count; i++) {
            var tile = new Vector2I(tiles[i] % Terrain.Size, tiles[i] / Terrain.Size);
            int links = world.SteelPipes.ConnectionMask(tile);
            foreach (var (offset, port, _) in SteelPipes.Sides) {
                if ((links & port) == 0) continue;
                int next = Terrain.TileIndex(tile + offset);
                if (visited.Add(next)) tiles.Add(next);
            }
        }
    }

    /// <summary>Fills pipes outward from this source and returns the output left for consumers.</summary>
    private double Fill(Network network, int inlet, double water) {
        if (network.Unfilled == 0) return water;
        if (!network.Routes.TryGetValue(inlet, out var route)) {
            network.Routes.Add(inlet, route = new FillRoute());
            ConnectedTiles(inlet, route.Tiles);
        }
        var pipes = world.SteelPipes;
        while (route.Next < route.Tiles.Count && water > 0) {
            int index = route.Tiles[route.Next];
            double current = pipes.LitersAt(index), put = Math.Min(water, SteelPipes.Capacity - current);
            if (put > 0) {
                if (current == 0) network.Wet.Push(index);
                pipes.Store(index, network.Liquid!, current + put);
                network.Stored += put;
                water -= put;
                if (current + put < SteelPipes.Capacity) break;
                network.Unfilled--;
            }
            route.Next++;
        }
        return water;
    }

    /// <summary>Takes the amount used by consumers out of the tiles that still hold standing water.</summary>
    private void Drain(Network network, double used) {
        if (used <= 0) return;
        foreach (var route in network.Routes.Values) route.Next = 0;
        var pipes = world.SteelPipes;
        while (used > 0 && network.Wet.TryPeek(out int index)) {
            double current = pipes.LitersAt(index), take = Math.Min(current, used);
            double left = current - take;
            if (current == SteelPipes.Capacity && left < current) network.Unfilled++;
            pipes.Store(index, network.Liquid!, left);
            network.Stored -= take;
            used -= take;
            if (left > 0) break;
            network.Wet.Pop();
        }
        if (network.Wet.Count == 0) network.Stored = 0;
    }

    /// <summary>Whole liters the building can store from its plumbing.</summary>
    private int StorageRoom(Thing receiver, string liquid) {
        if (!receiver.PlumbingInstalled) return 0;
        return receiver.Kind switch {
            ThingKind.Hut when liquid == Water.Liquid => Barrels.Room(receiver),
            ThingKind.Barrel when TankAccepts(receiver, liquid) => receiver.Inventory.Room(liquid),
            ThingKind.Farm when liquid == Water.Liquid => Math.Min(world.Farms.WaterRoom(receiver), receiver.Inventory.Room(liquid)),
            ThingKind.Cooktable or ThingKind.Cannery when world.Data.Recipes.TryGetValue(receiver.NextRecipe, out var recipe) =>
                Math.Clamp(recipe.Inputs.GetValueOrDefault(liquid) - receiver.Inventory[liquid], 0, Math.Max(0, receiver.Inventory.SlotRoom(liquid))),
            _ => 0
        };
    }

    /// <summary>Sprinklers can discharge their entire share even when the soil is full.</summary>
    private double Demand(Thing receiver, string liquid) =>
        receiver.Kind == ThingKind.Farm && receiver.PlumbingInstalled && liquid == Water.Liquid ? double.PositiveInfinity
        : StorageRoom(receiver, liquid) - (double)Remainder(receiver, liquid);

    private static float Remainder(Thing receiver, string liquid) =>
        liquid == BerryJam.Liquid ? receiver.PipedJamRemainder : receiver.PipedWaterRemainder;

    /// <summary>Splits the water among buildings that are drawing it and returns any water they cannot take.</summary>
    private double Share(Network network, double water, string liquid) {
        var receivers = network.Receivers;
        while (receivers.Count > 0 && water > 0) {
            int count = receivers.Count;
            double share = water / count;
            for (int i = count - 1; i >= 0; i--) {
                var receiver = receivers[i];
                double demand = Demand(receiver, liquid);
                if (demand > share) continue;
                Deliver(receiver, demand, liquid);
                water -= demand;
                receivers[i] = receivers[^1];
                receivers.RemoveAt(receivers.Count - 1);
            }
            if (receivers.Count != count) continue;
            foreach (var receiver in receivers) Deliver(receiver, share, liquid);
            return 0;
        }
        return Math.Max(0, water);
    }

    private void Deliver(Thing receiver, double share, string liquid) {
        double liters = Math.Min(StorageRoom(receiver, liquid), Remainder(receiver, liquid) + share);
        int whole = (int)liters;
        int received = whole == 0 ? 0 : Store(receiver, whole, liquid);
        float kept = received == whole ? Math.Min(MathF.BitDecrement(1), (float)(liters - whole)) : 0;
        if (liquid == BerryJam.Liquid) receiver.PipedJamRemainder = kept;
        else receiver.PipedWaterRemainder = kept;
        receiver.ReceivingWater = liquid == Water.Liquid && share > 0 && Demand(receiver, liquid) > 0;
    }

    private int Store(Thing receiver, int liters, string liquid) {
        if (receiver.Kind == ThingKind.Hut) return barrels.AddWater(receiver, liters);
        int put = receiver.Inventory.Add(liquid, liters);
        if (receiver.Kind == ThingKind.Barrel) world.PileFilters(receiver);
        return put;
    }

    internal static bool ValidReceiver(Thing thing) =>
        thing.PipedWaterRemainder is >= 0 and < 1 && thing.PipedJamRemainder is >= 0 and < 1
        && (HasPlumbing(thing.Kind) || !thing.ReceivingWater && thing.PipedWaterRemainder == 0 && thing.PipedJamRemainder == 0)
        && thing.PipeFeedCredit is >= 0 and < 1 && thing.PipeBalanceCredit is >= 0 and < 1 && thing.PipeBalanceTarget >= 0
        && (thing.Kind == ThingKind.Barrel || thing.PipeFeedCredit == 0 && thing.PipeBalanceCredit == 0 && thing.PipeBalanceTarget == 0);
}
