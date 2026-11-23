using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Moves water from pumps to consumers over connected steel pipe tiles.</summary>
public sealed class Plumbing(World world, Barrels barrels) {
    public const int InstallCost = 10;
    private static readonly ThingKind[] Consumers = Array.FindAll(Kinds.All, HasPlumbing);

    /// <summary>Whether this kind of building can receive pumped water.</summary>
    public static bool HasPlumbing(ThingKind kind) =>
        kind is ThingKind.Barrel or ThingKind.Hut or ThingKind.Cooktable or ThingKind.Farm;

    private sealed class FillRoute {
        public readonly List<int> Tiles = [];
        public int Next;
    }

    private sealed class Network {
        public bool Active, PumpRunning;
        public double Output, Stored;
        public int Unfilled;
        public readonly Stack<int> Wet = new();
        public readonly Dictionary<int, FillRoute> Routes = new();
        public readonly List<Thing> Receivers = [];
    }

    private readonly Dictionary<int, Network> networks = new();
    private readonly HashSet<int> visited = [];
    private readonly List<int> component = [];
    private readonly List<Network> active = [];
    private int revision = -1;

    internal (float Liters, float Pressure) Reading(Vector2I tile) {
        RefreshNetworks();
        if (NetworkAt(tile) is not Network network) return default;
        float pressure = 0;
        foreach (var pump in world.ThingsOf(ThingKind.Pump))
            if (pump.Pressure > pressure && NetworkAt(Terrain.TileAt(pump.Pos)) == network) pressure = pump.Pressure;
        return ((float)world.SteelPipes.WaterAt(Terrain.TileIndex(tile)), pressure);
    }

    internal void Tick(float dt) {
        RefreshNetworks();
        foreach (var pump in world.ThingsOf(ThingKind.Pump)) {
            float liters = WaterPumps.Advance(pump, dt);
            var inlet = Terrain.TileAt(pump.Pos);
            if (liters <= 0 || NetworkAt(inlet) is not Network network) continue;
            Activate(network);
            network.PumpRunning = true;
            network.Output += Fill(network, Terrain.TileIndex(inlet), liters);
        }

        foreach (var kind in Consumers)
            foreach (var receiver in world.ThingsOf(kind)) {
                receiver.ReceivingWater = false;
                if (NetworkAt(Terrain.TileAt(receiver.Pos)) is not Network network
                    || (network.PumpRunning ? network.Output <= 0 : network.Stored <= 0)
                    || Demand(receiver) <= 0) continue;
                Activate(network);
                network.Receivers.Add(receiver);
            }

        foreach (var network in active) {
            if (network.PumpRunning) Share(network, network.Output);
            else Drain(network, network.Stored - Share(network, network.Stored));
            network.Output = 0;
            network.PumpRunning = false;
            network.Receivers.Clear();
            network.Active = false;
        }
        active.Clear();
    }

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
        if (revision == pipes.Revision) return;
        revision = pipes.Revision;
        networks.Clear();
        foreach (int index in pipes.Tiles) {
            if (networks.ContainsKey(index)) continue;
            var network = new Network();
            ConnectedTiles(index, component);
            foreach (int tile in component) {
                networks.Add(tile, network);
                double stored = pipes.WaterAt(tile);
                network.Stored += stored;
                if (stored > 0) network.Wet.Push(tile);
                if (stored < SteelPipes.Capacity) network.Unfilled++;
            }
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

    /// <summary>Fills pipes outward from this pump and returns the output left for consumers.</summary>
    private double Fill(Network network, int inlet, double water) {
        if (network.Unfilled == 0) return water;
        if (!network.Routes.TryGetValue(inlet, out var route)) {
            network.Routes.Add(inlet, route = new FillRoute());
            ConnectedTiles(inlet, route.Tiles);
        }
        var pipes = world.SteelPipes;
        while (route.Next < route.Tiles.Count && water > 0) {
            int index = route.Tiles[route.Next];
            double current = pipes.WaterAt(index), put = Math.Min(water, SteelPipes.Capacity - current);
            if (put > 0) {
                if (current == 0) network.Wet.Push(index);
                pipes.StoreWater(index, current + put);
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
            double current = pipes.WaterAt(index), take = Math.Min(current, used);
            double left = current - take;
            if (current == SteelPipes.Capacity && left < current) network.Unfilled++;
            pipes.StoreWater(index, left);
            network.Stored -= take;
            used -= take;
            if (left > 0) break;
            network.Wet.Pop();
        }
        if (network.Wet.Count == 0) network.Stored = 0;
    }

    /// <summary>Whole liters the building can store from its plumbing.</summary>
    private int StorageRoom(Thing receiver) {
        if (!receiver.PlumbingInstalled) return 0;
        return receiver.Kind switch {
            ThingKind.Hut or ThingKind.Barrel => Barrels.Room(receiver),
            ThingKind.Farm => Math.Min(world.Farms.WaterRoom(receiver), receiver.Inventory.Room(Water.Liquid)),
            ThingKind.Cooktable when world.Data.Recipes.TryGetValue(receiver.NextRecipe, out var recipe) =>
                Math.Clamp(recipe.Inputs.GetValueOrDefault(Water.Liquid) - receiver.Inventory[Water.Liquid], 0, receiver.Inventory.Room(Water.Liquid)),
            _ => 0
        };
    }

    /// <summary>Sprinklers can discharge their entire share even when the soil is full.</summary>
    private double Demand(Thing receiver) =>
        receiver.Kind == ThingKind.Farm && receiver.PlumbingInstalled ? double.PositiveInfinity
        : StorageRoom(receiver) - (double)receiver.PipedWaterRemainder;

    /// <summary>Splits the water among buildings that are drawing it and returns any water they cannot take.</summary>
    private double Share(Network network, double water) {
        var receivers = network.Receivers;
        while (receivers.Count > 0 && water > 0) {
            int count = receivers.Count;
            double share = water / count;
            for (int i = count - 1; i >= 0; i--) {
                var receiver = receivers[i];
                double demand = Demand(receiver);
                if (demand > share) continue;
                Deliver(receiver, demand);
                water -= demand;
                receivers[i] = receivers[^1];
                receivers.RemoveAt(receivers.Count - 1);
            }
            if (receivers.Count != count) continue;
            foreach (var receiver in receivers) Deliver(receiver, share);
            return 0;
        }
        return Math.Max(0, water);
    }

    private void Deliver(Thing receiver, double share) {
        double liters = Math.Min(StorageRoom(receiver), receiver.PipedWaterRemainder + share);
        int whole = (int)liters;
        int received = whole == 0 ? 0 : StoreWater(receiver, whole);
        receiver.PipedWaterRemainder = received == whole ? Math.Min(MathF.BitDecrement(1), (float)(liters - whole)) : 0;
        receiver.ReceivingWater = share > 0 && Demand(receiver) > 0;
    }

    private int StoreWater(Thing receiver, int liters) => receiver.Kind is ThingKind.Hut or ThingKind.Barrel
        ? barrels.AddWater(receiver, liters) : receiver.Inventory.Add(Water.Liquid, liters);

    internal static bool ValidReceiver(Thing thing) =>
        thing.PipedWaterRemainder is >= 0 and < 1
        && (HasPlumbing(thing.Kind) || !thing.ReceivingWater && thing.PipedWaterRemainder == 0);
}
