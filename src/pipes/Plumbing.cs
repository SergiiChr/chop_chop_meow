using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Moves water from pumps to consumers over connected steel pipe tiles.</summary>
public sealed class Plumbing(World world) {
    public const int InstallCost = 10;
    private static readonly ThingKind[] Consumers = Array.FindAll(Kinds.All, HasPlumbing);

    /// <summary>Whether this kind of building can receive pumped water.</summary>
    public static bool HasPlumbing(ThingKind kind) =>
        kind is ThingKind.Barrel or ThingKind.Hut or ThingKind.Cooktable or ThingKind.Farm;

    private sealed class Network {
        public bool Active;
        public double Pumped, Stored;
        public int NextToFill, LastWet = -1;
        public readonly List<int> Tiles = [];
        public readonly List<Thing> Receivers = [];

        public void AddTile(int index, double liters) {
            if (liters >= SteelPipes.Capacity && NextToFill == Tiles.Count) NextToFill++;
            if (liters > 0) LastWet = Tiles.Count;
            Tiles.Add(index);
            Stored += liters;
        }
    }

    private readonly Dictionary<int, Network> networks = new();
    private readonly Queue<Vector2I> open = new();
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
            if (liters <= 0 || NetworkAt(Terrain.TileAt(pump.Pos)) is not Network network) continue;
            Activate(network);
            network.Pumped += liters;
        }

        foreach (var kind in Consumers)
            foreach (var receiver in world.ThingsOf(kind)) {
                receiver.ReceivingWater = false;
                if (NetworkAt(Terrain.TileAt(receiver.Pos)) is not Network network
                    || (network.Pumped <= 0 && network.Stored <= 0)
                    || world.PlumbingRoom(receiver) <= 0) continue;
                Activate(network);
                network.Receivers.Add(receiver);
            }

        foreach (var network in active) {
            if (network.Pumped > 0) Share(network, Fill(network, network.Pumped));
            else Drain(network, network.Stored - Share(network, network.Stored));
            network.Pumped = 0;
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
            networks.Add(index, network);
            open.Enqueue(new Vector2I(index % Terrain.Size, index / Terrain.Size));
            while (open.TryDequeue(out var tile)) {
                int at = Terrain.TileIndex(tile);
                network.AddTile(at, pipes.WaterAt(at));
                int links = pipes.ConnectionMask(tile);
                for (int direction = 0; direction < 8; direction++) {
                    if ((links & (1 << direction)) == 0) continue;
                    var next = tile + SteelPipes.Offset(direction);
                    if (networks.TryAdd(Terrain.TileIndex(next), network)) open.Enqueue(next);
                }
            }
        }
    }

    /// <summary>Fills the pipe tiles and returns the pump output left for consumers.</summary>
    private double Fill(Network network, double water) {
        var pipes = world.SteelPipes;
        while (network.NextToFill < network.Tiles.Count && water > 0) {
            int index = network.Tiles[network.NextToFill];
            double current = pipes.WaterAt(index), put = Math.Min(water, SteelPipes.Capacity - current);
            if (put > 0) {
                pipes.StoreWater(index, current + put);
                network.Stored += put;
                network.LastWet = Math.Max(network.LastWet, network.NextToFill);
                water -= put;
            }
            if (current + put < SteelPipes.Capacity) break;
            network.NextToFill++;
        }
        return water;
    }

    /// <summary>Takes the amount used by consumers out of the tiles that still hold standing water.</summary>
    private void Drain(Network network, double used) {
        var pipes = world.SteelPipes;
        while (network.LastWet >= 0 && used > 0) {
            int index = network.Tiles[network.LastWet];
            double current = pipes.WaterAt(index), take = Math.Min(current, used);
            if (take > 0) {
                pipes.StoreWater(index, current - take);
                network.Stored -= take;
                network.NextToFill = Math.Min(network.NextToFill, network.LastWet);
                used -= take;
            }
            if (current > take) break;
            network.LastWet--;
        }
        if (network.LastWet < 0) network.Stored = 0;
    }

    /// <summary>Splits the water among consumers with room and returns any water they cannot take.</summary>
    private double Share(Network network, double water) {
        var receivers = network.Receivers;
        while (receivers.Count > 0 && water > 0) {
            int count = receivers.Count;
            double share = water / count;
            for (int i = count - 1; i >= 0; i--) {
                var receiver = receivers[i];
                double room = world.PlumbingRoom(receiver) - (double)receiver.PipedWaterRemainder;
                if (room > share) continue;
                Deliver(receiver, room);
                water -= room;
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
        double liters = receiver.PipedWaterRemainder + share;
        int whole = (int)liters;
        int received = whole == 0 ? 0 : world.SupplyPlumbing(receiver, whole);
        receiver.PipedWaterRemainder = received == whole ? Math.Min(MathF.BitDecrement(1), (float)(liters - whole)) : 0;
        receiver.ReceivingWater = share > 0 && world.PlumbingRoom(receiver) > 0;
    }

    internal static bool ValidReceiver(Thing thing) =>
        thing.PipedWaterRemainder is >= 0 and < 1
        && (HasPlumbing(thing.Kind) || !thing.ReceivingWater && thing.PipedWaterRemainder == 0);
}
