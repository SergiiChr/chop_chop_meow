namespace ChopChopMeow;

/// <summary>A mine site's part of the Haul job: once the entrance is dug, it brings the timber, iron and stone its frame still lacks.</summary>
internal sealed class MineHaul : PlaceHaul {
    public MineHaul(World world) : base(world) { }

    public override void Carries(Worker w, Thing mine, ref Carry best) {
        if (mine.MineStage != MineStage.Dug) return;
        foreach (var (item, wanted) in world.Data.Mine.Materials) {
            int need = world.Mines.Need(mine, item);
            if (need > 0 && world.SourceFor(item, w.Pos, mine.Id) is Thing source) Offer(ref best, Bring(mine, source, item, need, wanted - need, wanted));
        }
    }

    public override string Wait(Thing mine) => mine.MineStage == MineStage.Dug ? world.Mines.Wants(mine) : base.Wait(mine);
}
