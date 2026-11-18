using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

/// <summary>What a wolf that hunts nobody is doing.</summary>
public enum WolfMode { Patrol, Called, Home }

/// <summary>A wolf: where it is and what it is doing, saved with the world.</summary>
public sealed class Wolf : IWalker {
    public float X { get; set; }
    public float Y { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public float Health { get; set; } = 4;
    public float Attack { get; set; }
    /// <summary>Seconds left in a throat leap; zero while the wolf runs on the ground.</summary>
    public float Leap { get; set; }
    /// <summary>True once the wolf has reached the downed player and is feeding.</summary>
    public bool Feeding { get; set; }
    /// <summary>The wolf whose leap landed; it holds the player's throat.</summary>
    public bool Pinning { get; set; }
    /// <summary>The wolf tugging its player out of a cabin's doorway by the waist.</summary>
    public bool Tugging { get; set; }
    /// <summary>
    /// The side of the player another wolf tugs that this wolf holds by the arm: 1 for their left, -1 for their right, 0 for neither.
    /// </summary>
    public int Side { get; set; }
    /// <summary>The wolf holding an arm has bitten it: it bites once as it takes hold, or as soon after as its last bite is a second behind it.</summary>
    public bool SideBitten { get; set; }
    /// <summary>What the wolf is doing at the door of the cabin <see cref="DoorCheckCabin"/>, and for how many seconds.</summary>
    public DoorCheckStage DoorCheck { get; set; }
    public int DoorCheckCabin { get; set; }
    public float DoorCheckTime { get; set; }
    /// <summary>The cabin whose locked door this wolf last left; it leaves that cabin alone until it is out of smell range of it.</summary>
    public int Tried { get; set; }
    /// <summary>The way from a player's throat to where a wolf that tugged them out lunges onto it from, or zero for one that pins them standing over them.</summary>
    public Vector2 LungeFrom { get; set; }
    /// <summary>The wolf has growled at the player while closing in; it growls only once.</summary>
    [JsonIgnore] public bool Growled { get; set; }
    /// <summary>The wolf has broken from a walk into a sprint at the player; it growls once as it does.</summary>
    [JsonIgnore] public bool Sprinted { get; set; }
    /// <summary>Seconds a chasing wolf keeps following the player into firelight before it gives up; above zero the wolf gallops.</summary>
    public float Chase { get; set; }
    /// <summary>A torch swing toward this wolf made it keep its distance; it lasts while the torch burns.</summary>
    public bool Scared { get; set; }
    /// <summary>Seconds left running away after a direct torch hit.</summary>
    public float Flee { get; set; }
    /// <summary>Seconds left reeling from a hit, knocked back and holding still before it runs or attacks again.</summary>
    public float Stagger { get; set; }
    /// <summary>Id of the player this wolf hunts, or zero while it patrols, answers a howl or heads home.</summary>
    public int Target { get; set; }
    public WolfMode Mode { get; set; }
    /// <summary>The way a patrolling wolf walks, in radians; it keeps it until the ground ahead is impassable.</summary>
    public float Heading { get; set; }
    /// <summary>Where the howl that called this wolf came from.</summary>
    public float CallX { get; set; }
    public float CallY { get; set; }
    [JsonIgnore] public Vector2 Call { get => new(CallX, CallY); set { CallX = value.X; CallY = value.Y; } }
    /// <summary>Seconds left turning where it stands to a player it smelled, before it howls at them.</summary>
    public float HowlIn { get; set; }
    /// <summary>Seconds left howling at a player it smelled; it stands still until it is done.</summary>
    public float Howl { get; set; }
    /// <summary>Seconds the current howl lasts in all, the length of its recording.</summary>
    public float HowlLength { get; set; }
    [JsonIgnore] public List<Vector2> Route { get; } = new();
    [JsonIgnore] public Vector2 RouteTarget { get; set; } = new(-100, -100);
    [JsonIgnore] public float RouteRetry { get; set; }
    /// <summary>Tells wolves apart for other machines; zero until <see cref="World.NumberWolvesAndOrders"/>.</summary>
    public int Id { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
}
