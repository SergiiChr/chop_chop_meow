using System;
using System.Collections.Generic;
using ChopChopMeow;
using Godot;

/// <summary>Focused checks for deterministic actor animation selection and timing.</summary>
public static class AnimationChecks
{
    public static void Run()
    {
        CheckFrameTiming();
        CheckDirections();
        CheckPlayerActions();
        CheckWorkerActions();
        CheckClipAssets();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Animation check failed: {message}");
    }

    private static void CheckFrameTiming()
    {
        var frames = new HashSet<int>();
        for (int i = 0; i < ActorAnimation.Frames; i++)
            frames.Add(ActorAnimation.Frame(ActorAction.Chop, i / 8f));
        Require(frames.Count == 8, "action timing should cover all eight frames");
        Require(ActorAnimation.Frame(ActorAction.Chop, 1) == 0, "action animation should wrap after one second");
        Require(ActorAnimation.Frame(ActorAction.Chop, -.125f) == 7, "negative elapsed time should wrap safely");
        Require(ActorAnimation.Frame(ActorAction.Walk, .1f) == 1, "walking should advance at ten frames per second");
        Require(ActorAnimation.Frame(ActorAction.Attack, .875f, 7) == 0, "seven-frame punching clips should wrap at their actual length");
        Require(ActorAnimation.Frame(ActorAction.Idle, .25f) == 1, "idle should advance at four frames per second");
    }

    private static void CheckDirections()
    {
        Require(ActorAnimation.Direction(new Vector2(1, 0), 1) == 0, "eastward world movement should face southeast");
        Require(ActorAnimation.Direction(new Vector2(0, 1), 0) == 1, "southward world movement should face southwest");
        Require(ActorAnimation.Direction(new Vector2(0, -1), 0) == 2, "northward world movement should face northeast");
        Require(ActorAnimation.Direction(new Vector2(-1, 0), 0) == 3, "westward world movement should face northwest");
        Require(ActorAnimation.Direction(Vector2.Zero, 2) == 2, "zero movement should retain the previous direction");
    }

    private static void CheckPlayerActions()
    {
        var bag = new Inventory();
        bag.Add("iron_axe", 1);
        bag.Add("iron_pick", 1);
        bag.Add("steel_tools", 1);
        var ironTools = new Inventory();
        ironTools.Add("iron_axe", 1);
        ironTools.Add("iron_pick", 1);

        Require(ActorAnimation.PlayerAction("tree", true, true, bag, "iron_axe") == ActorAction.Walk,
            "movement toward a resource should animate as walking");
        Require(ActorAnimation.PlayerAction("tree", true, false, bag, "iron_axe") == ActorAction.Chop,
            "a stationary player with an iron axe should chop a tree");
        Require(ActorAnimation.PlayerAction("tree", true, false, bag, "steel_tools") == ActorAction.SteelChop,
            "selected carried steel tools should use the steel chopping action");
        Require(ActorAnimation.PlayerAction("iron", true, false, bag, "iron_pick") == ActorAction.Mine,
            "selected carried iron pick should mine iron deposits");
        Require(ActorAnimation.PlayerAction("iron", true, false, ironTools, "iron_axe") == ActorAction.Mine,
            "an irrelevant default axe selection should fall back to a carried pick for iron");
        Require(ActorAnimation.PlayerAction("coal", true, false, bag, "steel_tools") == ActorAction.SteelMine,
            "selected carried steel tools should use steel mining action");
        Require(ActorAnimation.PlayerAction("stone", true, false, bag, "iron_axe") == ActorAction.SteelMine,
            "steel tools should use the steel mining action on stone");
        Require(ActorAnimation.PlayerAction("tree", true, false, ironTools, "iron_pick") == ActorAction.Chop,
            "an irrelevant selected pick should fall back to the carried axe for trees");

        var empty = new Inventory();
        Require(ActorAnimation.PlayerAction("tree", true, false, empty, "iron_axe") == ActorAction.Gather,
            "selected tools not carried should not animate as tool use");
        var axeOnly = new Inventory();
        axeOnly.Add("iron_axe", 1);
        Require(ActorAnimation.PlayerAction("coal", true, false, axeOnly, "iron_axe") == ActorAction.Gather,
            "a carried but unsuitable axe should not animate as mining");
        Require(ActorAnimation.PlayerAction("berries", true, false, empty, null) == ActorAction.Gather,
            "hand gathering berries should use the gather action");
        Require(ActorAnimation.PlayerAction("tree", false, false, bag, "iron_axe") == ActorAction.Idle,
            "inactive gathering should leave the player idle");
    }

    private static void CheckWorkerActions()
    {
        var worker = new Worker();
        worker.Bag.Add("wood", 2);
        worker.Status = "Carrying wood → chest";
        Require(ActorAnimation.WorkerAction(worker, null, true) == ActorAction.Carry,
            "a worker hauling a load while moving should retain the carry action");
        Require(ActorAnimation.WorkerAction(worker, null, false) == ActorAction.Carry,
            "a worker holding a load while stationary should use carry pose");

        worker.Bag.Remove("wood", 2);
        worker.Status = "Text can vary";
        worker.WorkState = WorkerWorkState.Pumping;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "reinforced" }, false) == ActorAction.Pump,
            "bellows work state should select pumping animation");
        worker.Status = "Different text";
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "reinforced" }, false) == ActorAction.Pump,
            "rewording status should not change the pumping animation");

        worker.Status = "Station status text";
        worker.WorkState = WorkerWorkState.Hammering;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "anvil" }, false) == ActorAction.Hammer,
            "anvil work should select hammering animation");
        worker.Status = "On strike · wages owed";
        worker.WorkState = WorkerWorkState.Idle;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "anvil" }, false) == ActorAction.Idle,
            "striking workers should remain idle");
        worker.Status = "Gathering wood";
        worker.WorkState = WorkerWorkState.Blocked;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "tree" }, false) == ActorAction.Idle,
            "waiting workers should not swing tools just because they have an assignment");

        worker.Status = "Arbitrary status";
        worker.WorkState = WorkerWorkState.Gathering;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "tree" }, false) == ActorAction.Chop,
            "workers gathering trees should chop");
        worker.Status = "Arbitrary status";
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "iron" }, false) == ActorAction.Mine,
            "workers gathering iron should mine");
        worker.Status = "Available";
        worker.WorkState = WorkerWorkState.Idle;
        Require(ActorAnimation.WorkerAction(worker, null, false) == ActorAction.Idle,
            "available workers should remain idle");
    }

    private static void CheckClipAssets()
    {
        ActorClips clips = ActorClips.Load();
        Require(clips.NamedClips.Count == 16, "the animation catalog should expose all sixteen named clips");
        foreach (var entry in clips.NamedClips)
        {
            Require(entry.Value.BodyHeight > 0, $"{entry.Key} should have a positive body height");
            for (int direction = 0; direction < ActorClip.Directions; direction++)
            {
                for (int frame = 0; frame < entry.Value.FrameCount; frame++)
                    Require(entry.Value.Get(direction, frame).Texture is AtlasTexture, $"{entry.Key} should reference a baked region at direction {direction}, frame {frame}");
                Require(DistinctFrameCount(entry.Value, direction) >= 6,
                    $"{entry.Key} direction {direction} should contain at least six distinct frames");
            }
        }

        Require(ReferenceEquals(clips.Get(false, ActorAction.Idle), clips.NamedClips["player-walk"]),
            "player idle should use the walk clip as a source atlas");
        Require(ReferenceEquals(clips.Get(true, ActorAction.Gather), clips.NamedClips["worker-gather"]),
            "worker hand gathering should use its dedicated clip");
        Require(ReferenceEquals(clips.Get(false, ActorAction.Gather), clips.NamedClips["player-gather"]),
            "player hand gathering should use its dedicated clip");
        Require(ReferenceEquals(clips.Get(false, ActorAction.Attack), clips.NamedClips["player-attack"]),
            "unarmed player attacks should use their dedicated punch clip");
        Require(ReferenceEquals(clips.Get(false, ActorAction.SteelChop), clips.NamedClips["player-steel-axe"]),
            "player steel chopping should select the steel axe clip");
        Require(ReferenceEquals(clips.Get(true, ActorAction.SteelMine), clips.NamedClips["worker-pick"]),
            "worker steel mining should use the available pick clip");
    }
    private static int DistinctFrameCount(ActorClip clip, int direction)
    {
        var hashes = new HashSet<string>();
        for (int frame = 0; frame < clip.FrameCount; frame++)
        {
            using var image = clip.Get(direction, frame).Texture.GetImage();
            if (image.IsCompressed()) image.Decompress();
            hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.GetData())));
        }
        return hashes.Count;
    }

}
