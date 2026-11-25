namespace ChopChopMeow;

/// <summary>Which of a place's boxes a hauler picks a load up from.</summary>
internal enum CarryBox { Inventory, Output, Burner }

/// <summary>One carry a hauler makes for a place: an input or fuel brought in, or an output taken out.</summary>
internal struct Carry {
    /// <summary>The place hauled for, or null for no carry.</summary>
    public Thing? Place;
    /// <summary>Where the load is picked up, and where it goes: an output leaves the place, an input comes to it.</summary>
    public Thing? From, To;
    public CarryBox Box;
    public string Item;
    public int Count;
    /// <summary>The load goes into the fuel slot.</summary>
    public bool Fuel;
    /// <summary>The load is vessels with room, brought to take out liquid the place's recipe no longer uses.</summary>
    public string? DrainLiquid;
    /// <summary>Which of the four haul rules offers the carry, the first before the others.</summary>
    public int Rule;
    /// <summary>Its place among the carries of its rule, lowest first.</summary>
    public float Order;
}
