namespace ChopChopMeow;

/// <summary>
/// A place's part of the Haul job: the carries a hauler can make for it, and what a hauler with nothing to carry says.
/// Each kind of place that offers Haul extends it, under the four haul rules: an input or fuel slot short of one batch,
/// an output that is full, the input that covers the fewest batches, the output that holds the most.
/// </summary>
internal abstract class PlaceHaul {
    protected readonly World world;

    protected PlaceHaul(World world) => this.world = world;

    /// <summary>Offers each carry the place wants now, keeping the best of them and of those already offered.</summary>
    public abstract void Carries(Worker w, Thing place, ref Carry best);

    /// <summary>What a hauler with nothing to carry for the place says.</summary>
    public virtual string Wait(Thing place) => "Waiting for the next load";

    /// <summary>Keeps the better of two carries: the earlier rule, then the lower order, then the one offered first.</summary>
    protected static void Offer(ref Carry best, in Carry c) {
        if (c.Count > 0 && (best.Place == null || c.Rule < best.Rule || c.Rule == best.Rule && c.Order < best.Order)) best = c;
    }

    /// <summary>A carry that brings a place an input: short of one batch it comes under the first rule, otherwise under the third, fewest batches covered first.</summary>
    protected static Carry Bring(Thing place, Thing from, string item, int count, int have, int batch) =>
        new() { Place = place, From = from, To = place, Item = item, Count = count, Rule = have < batch ? 1 : 3, Order = have / (float)batch };

    /// <summary>A carry that takes an output out of a place: a full one comes under the second rule, otherwise under the fourth, most held first.</summary>
    protected static Carry TakeOut(Thing place, Thing to, CarryBox box, string item, int count, bool full) =>
        new() { Place = place, From = place, To = to, Box = box, Item = item, Count = count, Rule = full ? 2 : 4, Order = -count };
}
