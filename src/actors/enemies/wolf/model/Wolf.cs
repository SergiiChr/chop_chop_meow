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
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public float Health { get; set; } = 8;
    /// <summary>Seconds until the wolf may start its next snap at a player.</summary>
    public float Attack { get; set; }
    /// <summary>
    /// Seconds left in a snap at a player (<see cref="WolfSnap"/>): the wolf gathers itself, then leaps and bites as it
    /// lands; zero while it is not snapping.
    /// </summary>
    public float Snap { get; set; }
    /// <summary>Where the player stood as the snap began, which tells the wolf how they move.</summary>
    public Vector2 SnapSeen { get; set; }
    /// <summary>Where a snapping wolf's leap lands, set as it leaves the ground.</summary>
    public Vector2 SnapTo { get; set; }
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
    /// <summary>The cabins whose doors this wolf gave up on, locked or out of its reach; it leaves each alone until it is out of smell range of it.</summary>
    public List<int> Tried { get; set; } = new(TriedRoom);
    /// <summary>How many cabins a wolf has room to remember from the start, so giving up on one mid-game makes no new list.</summary>
    private const int TriedRoom = 4;
    /// <summary>The way from a player's throat to where a wolf that tugged them out lunges onto it from, or zero for one that pins them standing over them.</summary>
    public Vector2 LungeFrom { get; set; }
    /// <summary>The wolf has growled at the player while closing in; it growls only once.</summary>
    [JsonIgnore] public bool Growled { get; set; }
    /// <summary>The wolf has broken from a walk into a sprint at the player; it growls once as it does.</summary>
    [JsonIgnore] public bool Sprinted { get; set; }
    /// <summary>Seconds a chasing wolf keeps following the player into firelight before it gives up; above zero the wolf gallops.</summary>
    public float Chase { get; set; }
    /// <summary>What a torch has made of this wolf while it hunts a player: in follow mode, turned on them, or running from them.</summary>
    public FollowStage Follow { get; set; }
    /// <summary>
    /// A torch swing toward this wolf made it keep its distance, in follow mode (<see cref="WolfFollow"/>): it circles its
    /// player at the edge of the torchlight while the torch burns, or stands braced when they walk up to it.
    /// </summary>
    [JsonIgnore] public bool Following => Follow is FollowStage.Circling or FollowStage.Braced;
    /// <summary>
    /// The way a following wolf circles its player: 1 for clockwise on the map, -1 for the other way, and 0 until it is
    /// given one at random.
    /// </summary>
    public int Round { get; set; }
    /// <summary>The players this wolf ran from; it leaves each alone until it has been out of smell range of them.</summary>
    public List<int> Shunned { get; set; } = new();
    /// <summary>Seconds until a wolf standing its ground growls again.</summary>
    [JsonIgnore] public float GrowlIn { get; set; }
    /// <summary>Seconds left running away, after a torch burnt it or from a player who walked at it.</summary>
    public float Flee { get; set; }
    /// <summary>Seconds left reeling from a hit, knocked back and holding still before it runs or attacks again.</summary>
    public float Stagger { get; set; }
    /// <summary>Id of the player this wolf hunts, or zero while it patrols, answers a howl or heads home.</summary>
    public int Target { get; set; }
    public WolfMode Mode { get; set; }
    /// <summary>
    /// The way the wolf's body faces, in radians: the way a patrolling wolf walks, which it keeps until the ground ahead is
    /// impassable, and for one hunting a player the way it runs, or the way to them while it stands, creeps or backs off.
    /// </summary>
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

    /// <summary>Lets go of the arm it holds.</summary>
    public void LetGoOfArm() { Side = 0; SideBitten = false; }

    /// <summary>Ends its door check, whatever stage it is at.</summary>
    public void EndDoorCheck() { DoorCheck = DoorCheckStage.None; DoorCheckCabin = 0; DoorCheckTime = 0; }
}
