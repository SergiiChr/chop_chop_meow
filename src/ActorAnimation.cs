using System;
using Godot;

namespace ChopChopMeow;

public enum ActorAction
{
    Idle,
    Walk,
    Chop,
    Mine,
    SteelChop,
    SteelMine,
    Gather,
    Carry,
    Pump,
    Hammer,
    Attack
}

/// <summary>Pure actor animation selection and timing helpers.</summary>
public static class ActorAnimation
{
    public const int Frames = 8;

    public static int Frame(ActorAction action, float elapsed)
    {
        float fps = action switch
        {
            ActorAction.Idle => 4,
            ActorAction.Walk => 10,
            _ => 8
        };
        if (!float.IsFinite(elapsed)) return 0;
        float cycle = (elapsed * fps) % Frames;
        if (cycle < 0) cycle += Frames;
        return (int)cycle;
    }

    /// <summary>Maps a world movement vector into SE, SW, NE, NW sprite directions.</summary>
    public static int Direction(Vector2 movement, int previous)
    {
        if (movement.LengthSquared() < 0.0001f) return previous is >= 0 and <= 3 ? previous : 0;
        float x = (movement.X - movement.Y) * 44;
        float y = (movement.X + movement.Y) * 22;
        if (x >= 0) return y >= 0 ? 0 : 2;
        return y >= 0 ? 1 : 3;
    }

    public static ActorAction PlayerAction(
        string? resourceKind,
        bool gathering,
        bool moving,
        bool attacking,
        Inventory backpack,
        string? selectedTool)
    {
        if (moving) return ActorAction.Walk;
        if (attacking)
        {
            string? attackTool = BestTool(backpack, selectedTool, "iron_axe");
            return attackTool == "steel_tools" ? ActorAction.SteelChop : attackTool == "iron_axe" ? ActorAction.Chop : ActorAction.Attack;
        }
        if (!gathering || string.IsNullOrEmpty(resourceKind)) return ActorAction.Idle;

        string? requiredTool = resourceKind == "tree" ? "iron_axe" : resourceKind is "stone" or "iron" or "coal" ? "iron_pick" : null;
        string? tool = requiredTool == null ? null : BestTool(backpack, selectedTool, requiredTool);
        return resourceKind switch
        {
            "tree" => tool switch
            {
                "steel_tools" => ActorAction.SteelChop,
                "iron_axe" => ActorAction.Chop,
                _ => ActorAction.Gather
            },
            "stone" => tool switch
            {
                "steel_tools" => ActorAction.SteelMine,
                "iron_pick" => ActorAction.Mine,
                _ => ActorAction.Gather
            },
            "iron" or "coal" => tool switch
            {
                "steel_tools" => ActorAction.SteelMine,
                "iron_pick" => ActorAction.Mine,
                _ => ActorAction.Gather
            },
            "berries" or "water" or "loose_iron" => ActorAction.Gather,
            _ => ActorAction.Idle
        };
    }

    public static ActorAction WorkerAction(Worker worker, Thing? assignment, bool moving)
    {
        if (worker.Bag.Count > 0) return ActorAction.Carry;
        if (moving) return ActorAction.Walk;

        string status = worker.Status ?? "";
        if (status.StartsWith("Operating bellows", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Pumping bellows", StringComparison.OrdinalIgnoreCase)) return ActorAction.Pump;
        if (status.StartsWith("On strike", StringComparison.OrdinalIgnoreCase)) return ActorAction.Idle;

        if (assignment?.IsResource == true && status.StartsWith("Gathering ", StringComparison.OrdinalIgnoreCase))
        {
            return assignment.Kind switch
            {
                "tree" => ActorAction.Chop,
                "stone" or "iron" or "coal" => ActorAction.Mine,
                _ => ActorAction.Gather
            };
        }

        if (assignment?.Kind == "anvil" && status.StartsWith("Working at station", StringComparison.OrdinalIgnoreCase))
            return ActorAction.Hammer;

        return ActorAction.Idle;
    }

    private static string? BestTool(Inventory backpack, string? selectedTool, string ironTool)
    {
        if (selectedTool is "steel_tools" && backpack[selectedTool] > 0) return selectedTool;
        if (selectedTool == ironTool && backpack[selectedTool] > 0)
            return selectedTool;
        if (backpack["steel_tools"] > 0) return "steel_tools";
        if (backpack[ironTool] > 0) return ironTool;
        return null;
    }
}
