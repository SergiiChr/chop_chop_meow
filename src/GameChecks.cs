using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The intro film covers the start menu and takes every click and key until Esc skips it and nothing else.</summary>
    private async Task CheckIntroFilmCoversTheStartMenuUntilEsc() {
        ShowStart(); await Frames();
        // The film plays when the game opens, which a check run does without: no input starts it.
        PlayIntro(); await Frames();
        await Click(FindButton(startMenu.Screen, "Options")!);
        Require(intro!.Visible && FindButton(startMenu.Screen, "New game") != null, "A click must not reach the start menu under the intro film.");
        await KeyStroke(Key.Escape);
        Require(!IsInstanceValid(intro), "Esc must skip the intro film and take it off the screen.");
        Require(startMenu.Visible && FindButton(startMenu.Screen, "New game") != null, "Skipping the intro film must leave the start menu as it was.");
        await Click(FindButton(startMenu.Screen, "Options")!);
        Require(FindButton(startMenu.Screen, "New game") == null, "The start menu must take clicks again once the intro film is gone.");
        await KeyStroke(Key.Escape);
    }
    /// <summary>
    /// A new game reuses the ids of the last one, so the models the view keeps for them must be built anew:
    /// an old one would carry the last game's state, such as a felled tree faded out.
    /// </summary>
    private async Task CheckRestartBuildsFreshModelsForReusedIds() {
        var tree = world.Things.Where(t => t.Kind == ThingKind.Tree).MinBy(t => t.Pos.DistanceTo(world.Player))!;
        await Frames();
        var old = view.PropOf(tree.Id) as TreeProp;
        Require(old != null, "Every tree must have its model once the world has loaded.");
        CheckTreeCanopyCastsShadowsOnlyThroughItsProxies(old!);
        CheckFadingCanopyKeepsItsCutoutShadows(old!);
        CheckEveryTreeTrunkReachesTheGround();
        CheckStumpIsAsThickAsItsTreesTrunk();
        old!.Fade = .68f; old.Lying = 9;
        await Click(FindButton(startMenu.Screen, "New game")!);
        var fresh = view.PropOf(tree.Id) as TreeProp;
        Require(world.Find(tree.Id)?.Kind == ThingKind.Tree, "A new game must keep a tree where the last game's tree stood.");
        Require(fresh != null, "A new game must build a model for that tree.");
        Require(fresh != old, "A new game must build a fresh tree model rather than keep the last game's.");
        Require(fresh.Fade == 0, "A new game's tree model must not stay faded.");
        Require(fresh.Falling == null && fresh.Lying < 0, "A new game's tree model must not stay fallen.");
        await CloseWithEsc();
    }
    /// <summary>Every tree species and variant has a trunk that stands on the ground, also where its lowest branches hang below the start of its baked trunk.</summary>
    private void CheckEveryTreeTrunkReachesTheGround() {
        for (int id = 0; id < WorldModels.TreeLooks; id++) {
            var prop = (TreeProp)WorldModels.Build(new Thing { Id = id, Kind = ThingKind.Tree });
            float bottom = TrunkBottom(prop);
            prop.Root.Free();
            Require(bottom < .03f, $"Tree look {id} must have a trunk down to the ground, not one that starts {bottom:0.00} m above it.");
        }
    }
    /// <summary>A stump is shaped by the tree that left it: a birch's is thinner than an oak's, and one with no tree on record is no thicker than a trunk either.</summary>
    private void CheckStumpIsAsThickAsItsTreesTrunk() {
        // A tree's species goes by its id: 3 is an oak and 2 a birch.
        var (oak, birch, bare) = (StumpWidth(3), StumpWidth(2), StumpWidth(0));
        Require(oak > birch, $"An oak's stump must be thicker than a birch's: {oak:0.00} against {birch:0.00}.");
        Require(bare < 1, $"A stump with no tree on record must be as thin as a trunk, not the stump model's own size: {bare:0.00}.");
    }
    /// <summary>Canopy fades use separate shadow-only meshes so the original cutout shadows survive alpha blending.</summary>
    private void CheckTreeCanopyCastsShadowsOnlyThroughItsProxies(TreeProp prop) {
        Require(prop.SwayParts.Length > 0, "A tree canopy must expose its visible meshes for fading.");
        Require(AllMeshes(prop.Sway).Count(mesh => CanopyCore(mesh) && mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.ShadowsOnly) == 1,
            "A tree canopy must have one shadow-only solid core.");
        var shadows = CanopyShadows(prop);
        Require(shadows.Length == prop.SwayParts.Length, "Each visible tree canopy mesh must have exactly one shadow-only proxy.");
        foreach (var (mesh, material) in prop.SwayParts) {
            Require(mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off,
                "Visible tree canopy meshes must leave shadow casting to their proxies.");
            Require(ReferenceEquals(mesh.MaterialOverride, material),
                "A visible tree canopy mesh must start with its original material.");
            var proxy = ShadowProxy(shadows, mesh, material);
            Require(proxy != null, "Each tree canopy shadow proxy must share its mesh, material and transform with the visible mesh.");
            Require(Mathf.IsEqualApprox(FoliageOpacity(proxy!.MaterialOverride!), 1), "A tree shadow proxy must keep fully opaque foliage alpha.");
        }
        Require(prop.SwayParts.All(part => part.Mesh.CastShadow != GeometryInstance3D.ShadowCastingSetting.ShadowsOnly),
            "Shadow-only proxies must be excluded from the tree canopy fade parts.");
    }

    private void CheckFadingCanopyKeepsItsCutoutShadows(TreeProp prop) {
        var shadows = CanopyShadows(prop);
        var originals = prop.SwayParts.Select(part => part.Material).ToArray();
        var proxies = prop.SwayParts.Select(part => ShadowProxy(shadows, part.Mesh, part.Material)!).ToArray();
        var proxyMaterials = proxies.Select(proxy => proxy.MaterialOverride!).ToArray();
        var proxyAlphas = proxyMaterials.Select(FoliageOpacity).ToArray();
        var proxyBlended = proxyMaterials.Select(FoliageBlended).ToArray();
        ModelKit.SetFade(prop.SwayParts, .68f);
        for (int i = 0; i < prop.SwayParts.Length; i++) {
            var mesh = prop.SwayParts[i].Mesh;
            var faded = mesh.MaterialOverride as Material;
            Require(faded != null, "A faded tree canopy mesh must still have a material.");
            Require(FoliageBlended(faded), "Fading a tree canopy must blend the visible mesh material.");
            Require(FoliageOpacity(faded) < FoliageOpacity(originals[i]), "Fading a tree canopy must make the visible mesh material more transparent.");
            Require(mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off, "Fading a tree canopy must leave the visible mesh without shadow casting, to its proxy.");
            var proxy = proxies[i];
            Require(ReferenceEquals(proxy.MaterialOverride, originals[i]),
                "A tree shadow proxy must keep its original material while the visible mesh fades.");
            Require(Mathf.IsEqualApprox(FoliageOpacity(proxyMaterials[i]), proxyAlphas[i]),
                "A tree shadow proxy must keep its original opacity while the visible mesh fades.");
            Require(FoliageBlended(proxyMaterials[i]) == proxyBlended[i],
                "A tree shadow proxy must keep its original blending while the visible mesh fades.");
        }
        ModelKit.SetFade(prop.SwayParts, 0);
        for (int i = 0; i < prop.SwayParts.Length; i++) {
            var mesh = prop.SwayParts[i].Mesh;
            Require(ReferenceEquals(mesh.MaterialOverride, originals[i]) && mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off,
                "Restoring a tree canopy must restore the original visible material while proxies keep casting shadows.");
        }
    }
    /// <summary>Messages keep clear of the HUD, stack, restart on a repeat, make room by dropping the stalest and fade out.</summary>
    private async Task CheckMessagesStackFadeAndKeepClearOfHud() {
        await CloseWithEsc();
        Require(messages.GetGlobalRect().Position.X > needsPlate.GetGlobalRect().End.X && messages.GetGlobalRect().End.X < ui.Size.X - needsPlate.GetGlobalRect().End.X + 1,
            "Messages must keep clear of the needs plate and stay centred.");
        await KeyStroke(Key.C);
        Require(messages.GetGlobalRect().End.X < panel.GetGlobalRect().Position.X, "Messages must keep to the left of an open side panel.");
        await CloseWithEsc();
        messages._Process(60);
        Require(messages.Shown.Length == 0, "Old messages must be gone before the message check.");
        world.Notify!("First"); world.Notify("Second"); messages._Process(3); world.Notify("First");
        Require(messages.Shown.SequenceEqual(new[] { "First", "Second" }), "A repeated message must restart its line instead of stacking again.");
        messages._Process(4.5);
        Require(messages.Shown.SequenceEqual(new[] { "First" }), "Each message must disappear after its own time.");
        world.Notify("Third"); messages._Process(1); world.Notify("First"); world.Notify("Fourth"); world.Notify("Fifth"); world.Notify("Sixth");
        Require(messages.Shown.SequenceEqual(new[] { "First", "Fourth", "Fifth", "Sixth" }), "A full stack must drop the message closest to fading, even when a repeat kept an older line.");
        var lines = messages.GetChildren().OfType<Label>().ToArray();
        Require(lines.Count(l => l.Visible) == 4, "All four stacked messages must show.");
        Require(lines[3].Text == "Sixth", "The newest stacked message must be on the last line.");
        Require(lines.All(l => l.Modulate.A == 1), "Every stacked message must show at full strength.");
        messages._Process(6.5);
        Require(lines[0].Modulate.A is > 0 and < 1, "A message must fade out over its last second.");
        messages._Process(1);
        Require(messages.Shown.Length == 0 && lines.All(l => !l.Visible), "Every message must disappear once its time is up.");
    }
    private async Task CheckTradingPostSendsOnlyCratesOffOnACart() {
        await CloseWithEsc();
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        Give(Hosted, "wood", 5); Give(Hosted, Inventory.Coins, 100);
        var berries = Hosted.Add(ThingKind.Crate, world.Player + new Vector2(0, 1)); berries.Inventory.Add("berries", 12); Hosted.PickUpCrate(berries);
        string packed = Hosted.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        await Click(view.Screen(sign.Pos, .8f));
        Require(Shows(sign), "Clicking a trading post must select it.");
        Require(Named("sell") != null, "The market must offer a sell button.");
        Require(Named("coins") != null, "The market must offer a coins button.");
        Require(Named("hardhat") != null, "The market must offer a hire button.");
        Require(Named("prices") != null, "The market must offer a prices button.");
        await Holding(Key.Shift, () => Click(Cell(2, c => c.TooltipText.StartsWith("Wood ·"))));
        Require(sign.Inventory.Count == 0 && world.Backpack["wood"] == 5, "Loose goods must stay in the backpack: the market only takes crates.");
        await Holding(Key.Shift, () => Click(Cell(2, c => c.TooltipText.StartsWith("Wood crate of 12 Berries"))));
        Require(sign.Inventory[packed] == 1 && world.Backpack[packed] == 0, "Shift-clicking a packed crate must load it into the market input.");
        int played = sounds.Played;
        await Click(Named("sell")!);
        Require(sounds.Played > played, "Sell must play the sound of the goods going.");
        Require(world.Orders.Count == 1 && sign.Inventory.Count == 0, "Sell must send the market input off on a cart.");
        await Click(Named("auto_sell")!);
        Require(sign.AutoSell, "Ticking Send when full must set the post to send the cart on its own.");
        await Click(Named("filters")!); await Click(Named("filter_add_in")!);
        await Click(Named($"pick_{World.CrateOf("berries")}")!);
        Require(sign.Inventory.InputFilter.SequenceEqual(new[] { World.CrateOf("berries") }),
            "The post's filter must offer the crate of berries.");
        Require(Named("filter_add_out") == null,
            "The post's filter must not offer a row of goods that workers do not bring.");
        Require(Named("pick_berries") == null, "The post's filter must not offer loose berries.");
        await Click(Named("filters_close")!);
        int before = world.DayRevenue;
        Require(world.DailyWages == 0 && BankRevenue() == ($"+{before}", Style.Warn), "With no sale today the bank must show today's coins in red, under the wages.");
        Hosted.Tick(world.Data.TradeSeconds);
        // A crate of 12 berries at 0.82 and the crate at 3.15 pay 12 coins.
        await Until(() => BankRevenue() == ($"+{before + 12}", Style.Ready), "the bank showing the sale's 12 coins in green, above no wages", 2);
        RemoveThings(Hosted, t => t == sign); Drop(Hosted, Inventory.Coins, 999);
        await CloseWithEsc();
    }

    /// <summary>An empty pile's filter window has one row naming one resource: water there is one entry for every fill, shows in the heading, and comes off with a click.</summary>
    private async Task CheckPileFilterNamesOneResource() {
        var pile = await OpenFilters(ThingKind.Pile);
        Require(OnPage("filter_in") != null && OnPage("filter_out") == null, "A pile's filter must show only the row of what workers bring.");
        await Click(Named("filter_add_in")!);
        Require(Named($"pick_{Water.OneLiter}") == null && Named($"pick_{Water.Full}") == null && Named($"pick_{Water.Empty}") != null,
            "A pile's filter must offer water at any fill as one entry, apart from empty bottles.");
        Require(Named($"pick_{World.MixedCrate}") != null, "A pile's filter must offer crates of mixed goods.");
        await Click(Named($"pick_{Water.Liquid}")!);
        await Until(() => pile.Inventory.InputFilter.SequenceEqual(new[] { Water.OneLiter, Water.TwoLiters, Water.Full }) && ShownPage() == "Pile · water",
            "picking water making the empty pile a water pile, named in its heading", 2);
        Require(Named("filter_add_in") == null, "A pile whose filter names a resource must offer no second one.");
        await Click(Named($"filter_in_{Water.Liquid}")!);
        await Until(() => pile.Inventory.InputFilter.Count == 0 && ShownPage() == "Pile" && Named("filter_add_in") != null, "clicking water taking it off the empty pile", 2);
        Require(FindButton(panel, "Clear") != null, "The filter window's clear button must read Clear.");
        await Click(Named("filters_close")!);
        RemoveThings(Hosted, t => t == pile);
        await CloseWithEsc();
    }

    /// <summary>A chest's item list runs by group, each simplest first: what is gathered, grown or bought, materials, tools, containers, food, then crates of goods.</summary>
    private async Task CheckItemListRunsByGroupSimplestFirst() {
        var chest = await OpenFilters(ThingKind.Chest);
        await Click(Named("filter_add_in")!);
        var listed = FilterEntries("item_list", "pick_").ToList();
        string[] expected = ["wood", "planks", "iron_bar", "steel_bar", "steel_parts", "stone_chisel", "steel_chisel", Water.Empty, Bucket.AtFill(5), "flour", "meat_stew", "dough", "canned_meat_stew", "bread", World.CrateOf("berries"), World.CrateOf("bread")];
        Require(listed.Where(expected.Contains).SequenceEqual(expected), $"The item list must run by group, simplest first, not {string.Join(" ", listed)}.");
        Require(listed.SkipWhile(id => !id.StartsWith(World.PackedPrefix)).All(id => id.StartsWith(World.PackedPrefix)), "The item list must show crates of goods after every item.");
        await Click(Named("filters_close")!);
        RemoveThings(Hosted, t => t == chest);
        await CloseWithEsc();
    }

    /// <summary>A filter row shows its items in the order they were added, also after one of them comes off, or comes off and goes back on between two looks of the page.</summary>
    private async Task CheckFilterRowKeepsTheOrderAdded() {
        var chest = await OpenFilters(ThingKind.Chest);
        foreach (string item in new[] { "stone", "planks" }) {
            await Click(Named("filter_add_in")!); await Click(Named($"pick_{item}")!);
            await Until(() => Named($"filter_in_{item}") != null, $"picking {item} adding it to the row", 2);
        }
        await Click(Named("filter_in_stone")!);
        await Until(() => Named("filter_in_stone") == null, "clicking stone taking it off the row", 2);
        await Click(Named("filter_add_in")!); await Click(Named("pick_coal")!);
        await Until(() => FilterEntries("filter_in", "filter_in_").SequenceEqual(["planks", "coal"]), "coal added after stone came off showing after planks", 2);
        // As another player would, between two looks of the page: the row's items stay the same, only their order changes.
        Hosted.SetFilter(chest, "planks", false, false); Hosted.SetFilter(chest, "planks", false, true);
        await Until(() => FilterEntries("filter_in", "filter_in_").SequenceEqual(["coal", "planks"]), "planks taken off and put back at once moving after coal", 2);
        await Click(Named("filters_close")!);
        RemoveThings(Hosted, t => t == chest);
        await CloseWithEsc();
    }

    /// <summary>The autosave comes every five minutes, more than this stage has, so the check asks for it.</summary>
    private async Task CheckAutosaveTellsTheOwner() {
        await CloseWithEsc();
        hosted!.Autosave();
        await Until(() => messages.Shown.Contains("Settlement saved · Autosave."), "the owner hearing about the autosave", 5);
        Require(Saves.SlotUsed(0), "The autosave must write its slot.");
    }

    /// <summary>A save that cannot be written says so and leaves the slot's earlier save as it was.</summary>
    private void CheckFailedSaveKeepsTheSaveBefore() {
        Require(Saves.Write(Hosted, 0).StartsWith("Settlement saved"), "A save must say it was written.");
        // A position that is no number cannot be written as save text.
        // No frame runs before it is put back.
        var stood = Hosted.Player;
        Hosted.Player = new Vector2(float.NaN, 0);
        string said = Saves.Write(Hosted, 0);
        Hosted.Player = stood;
        Require(said.StartsWith("Save failed"), $"A save that cannot be written must say so, not: {said}");
        Require(Saves.Read(world.Data, 0).Error == null, "A failed save must leave the slot's earlier save loadable.");
    }

    /// <summary>A window glides what the server moves, and an update that changes one number must not leave the other where the glide had got to.</summary>
    private async Task CheckOthersGlideAndStopWhereTheServerPutThem() {
        var worker = AddWorker(Hosted, world.Player + new Vector2(3, 0));
        // With every menu open the world stands still, so nothing but this check moves the worker.
        await KeyStroke(Key.Escape);
        var copy = world.Workers.Single(w => w.Id == worker.Id);
        worker.Pos += new Vector2(2, 0); await Frames(2);
        Require(copy.Pos.DistanceTo(worker.Pos) > .1f, "What the server moves must glide there, not jump.");
        worker.Pos += new Vector2(0, 1);
        await Until(() => copy.Pos.DistanceTo(worker.Pos) < .01f, "the worker stopping where the server put it", 5);
        await KeyStroke(Key.Escape);
        Hosted.Fire(worker); RemoveThings(Hosted, t => t.Kind == ThingKind.Grave);
        await Frames();
    }

    private async Task CheckWorkerPageShowsWhatEachPlaceIsSetToMake() {
        var (bench, worker) = await OpenBenchWorkerPage();
        Texture2D? Made() => OnPage($"made_{bench.Id}") is { Visible: true } cup ? cup.GetChild<TextureRect>(0).Texture : null;
        await Until(() => Made() == Icons.Of("planks"), "the bench's jobs showing planks", 2);
        Hosted.SetRecipe(bench, "crate");
        await Until(() => Made() == Icons.Of("crate"), "the bench's jobs following it to crates", 2);
        await CloseWorkerPage(bench, worker);
    }

    private async Task CheckDraggingAJobBelowAnotherMakesItComeAfter() {
        var (bench, worker) = await OpenBenchWorkerPage();
        float Row(string job) => Named($"drop_{job}_{bench.Id}")!.GlobalPosition.Y;
        Require(Row("craft") < Row("haul"), "A worker put on a bench must list Craft before Haul.");
        var held = await HoldOver(await At(OnPage("grip_0")!), await AtFoot(Named($"drop_haul_{bench.Id}")!));
        Require(OnPage("drop_mark") is { Visible: true } mark && mark.GlobalPosition.Y > Row("haul") && OnPage("lifted") != null,
            "A job dragged by its grip must follow the mouse and mark where it would land.");
        await LetGo(held);
        await Until(() => OnPage("lifted") == null && Named($"drop_haul_{bench.Id}") != null && Row("haul") < Row("craft"), "the craft job landing below the haul job", 2);
        await CloseWorkerPage(bench, worker);
    }

    private async Task CheckAWorkersLoadAndToolAreTakenAndGivenOnTheirPage() {
        var (bench, worker) = await OpenBenchWorkerPage();
        worker.Bag.Add("planks", 5); worker.Tool = "steel_axe";
        int planks = world.Backpack["planks"], axes = world.Backpack["steel_axe"];
        // The page's first grid is the tool slot and its second the worker's hands.
        await PickUp(await Showing(1, c => c.TooltipText.StartsWith("Wood planks ·", StringComparison.Ordinal)));
        await Click(BagCell(c => c.Visible && c.Icon == null));
        await Until(() => cursor.Empty && world.Backpack["planks"] == planks + 5 && Cells(1).All(c => c.Icon == null), "the worker's planks landing in the backpack", 2);
        await PickUp(await Showing(0, c => c.TooltipText.StartsWith("Steel axe ·", StringComparison.Ordinal)));
        await Click(BagCell(c => c.Visible && c.Icon == null));
        await Until(() => cursor.Empty && world.Backpack["steel_axe"] == axes + 1 && Cells(0).All(c => c.Icon == null), "the worker's axe landing in the backpack", 2);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Wood planks ·", StringComparison.Ordinal)));
        await Click(Cells(0).First());
        Require(!cursor.Empty && Cells(0).All(c => c.Icon == null), "A worker's tool slot must not take planks.");
        await Click(Cells(1).First(c => c.Visible));
        await Until(() => cursor.Empty && Cells(1).Any(c => c.TooltipText.StartsWith("Wood planks ·", StringComparison.Ordinal)), "the planks going into the worker's hands", 2);
        await CloseWorkerPage(bench, worker);
    }

    /// <summary>A packed crate put into a worker's hands fills them alone, and the page says so above them.</summary>
    private async Task CheckAWorkerHoldingAPackedCrateSaysSoAboveTheirHands() {
        string packed = PackCrate(Hosted, ("berries", 1));
        var (bench, worker) = await OpenBenchWorkerPage();
        var carrying = (Label)OnPage("hands")!.GetChild(0);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Wood crate of", StringComparison.Ordinal)));
        await Click(Cells(1).First(c => c.Visible));
        await Until(() => cursor.Empty && worker.Bag[packed] == 1 && carrying.Text == "Carrying a packed crate", "the worker's page saying their hands carry a packed crate", 2);
        await PickUp(await Showing(1, c => c.TooltipText.StartsWith("Wood crate of", StringComparison.Ordinal)));
        await Click(BagCell(c => c.Visible && c.Icon == null));
        await Until(() => cursor.Empty && worker.Bag.Count == 0, "the packed crate going back into the backpack", 2);
        Drop(Hosted, packed);
        await CloseWorkerPage(bench, worker);
    }

    private async Task CheckWorkerPageFollowsItsJobs() {
        await CloseWithEsc();
        var forge = Hosted.Add(ThingKind.Reinforced, world.Player + new Vector2(-2, 0));
        var worker = AddWorker(Hosted, world.Player + Vector2.One); Hosted.Assign(worker, forge);
        await KeyStroke(Key.H); await Click(Named($"crew_{worker.Id}")!);
        string drop = $"drop_{((Duty)worker.Jobs[0]).Job.Id()}_{forge.Id}";
        await Click(Named(drop)!);
        await Until(() => Named(drop) == null, "the worker's page dropping the job taken away", 2);
        Hosted.Assign(worker, forge);
        await Until(() => Named(drop) != null, "the worker's page showing a job given elsewhere", 2);
        await Click(FindButton(panel, "Clear jobs")!);
        await Until(() => Named(drop) == null, "the worker's page clearing its jobs", 2);
        await Click(FindButton(panel, "Dismiss")!);
        await Until(() => Named($"crew_{worker.Id}") == null, "the crew page dropping the dismissed worker", 2);
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == forge || t.Kind == ThingKind.Grave);
        await Frames();
    }

    /// <summary>A tick box asks the server, so it must not read the old answer back at once, and must follow what the server decides.</summary>
    private async Task CheckTickBoxesFollowTheServer() {
        await CloseWithEsc();
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        await Click(view.Screen(sign.Pos, .8f)); await Click(Named("coins")!);
        var auto = (CheckBox)Named("auto_sell")!;
        await Click(auto);
        await Until(() => sign.AutoSell, "the tick turning the post's sending on", 2);
        await Click(auto);
        await Until(() => !sign.AutoSell, "a second click turning the post's sending off", 2);
        await Click(auto);
        await Until(() => sign.AutoSell, "the tick turning the post's sending on again", 2);
        // What a refusal leaves: the box said yes and the server says no.
        sign.AutoSell = false;
        await Until(() => !auto.ButtonPressed, "the box going back to what the server says", 2);
        var forge = Hosted.Add(ThingKind.Reinforced, world.Player + new Vector2(-2, 0));
        var worker = AddWorker(Hosted, world.Player + Vector2.One); await Frames();
        await ClickOn(forge);
        var job = world.Jobs(Shown(forge))[0];
        await Click(Named($"job_{job.Id()}")!); await Click(Named($"duty_{worker.Id}")!);
        await Until(() => World.HasDuty(worker, forge.Id, job), "the tick giving the worker the job", 2);
        await Click(Named($"duty_{worker.Id}")!);
        await Until(() => !World.HasDuty(worker, forge.Id, job), "a second click taking the job away", 2);
        await CloseWithEsc();
        Hosted.Fire(worker); RemoveThings(Hosted, t => t == sign || t == forge || t.Kind == ThingKind.Grave);
        await Frames();
    }

    /// <summary>A window joins before it has the world, which must not read as everything it remembers being gone.</summary>
    private async Task CheckLoadRemembersWhatIsOutOfSight() {
        await CloseWithEsc();
        var home = world.Player;
        var tree = world.Things.Where(t => t.Kind == ThingKind.Tree).MinBy(t => t.Pos.DistanceTo(Terrain.Spawn))!;
        var cliff = Cliff.Of(ThingKind.Iron);
        Warp(Hosted, Terrain.Spawn); await Frames(4);
        Warp(Hosted, cliff.MineSpot + cliff.Face * 3); await Frames(4);
        Require(world.Remembered(tree.Id) != null && !world.Sees(tree.Pos), "The player must remember a tree they have walked away from.");
        await KeyStroke(Key.Escape); await Click(menu.SaveButton); await Click(FindButton(startMenu.Screen, Saves.SlotLabel(2))!);
        await Until(() => Saves.SlotUsed(2), "the server writing the save");
        await KeyStroke(Key.Escape); await KeyStroke(Key.F9); await StartedGame(); await Frames(4);
        Require(world.Remembered(tree.Id) != null, "A load must keep what the player remembers around the clearing, where a window stands until the world arrives.");
        Warp(Hosted, home); await CloseWithEsc(); await Frames(4);
    }

    private async Task CheckPriceSheetBuysGoodsAndFetchNeedsAHiredWorker() {
        await CloseWithEsc();
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        Give(Hosted, Inventory.Coins, 100);
        await Click(view.Screen(sign.Pos, .8f));
        var fetch = Named("fetch");
        Require(ShowsChild(panel, "side_board"), "The price sheet must open.");
        Require(panel.OffsetLeft == SidePanel.Left - MarketPanel.SheetWidth, "The price sheet must open beside the panel.");
        Require(fetch != null, "The price sheet must offer a fetch button.");
        Require(fetch.Disabled, "The fetch button must be disabled while no worker is assigned.");
        Require(fetch.TooltipText == "No worker assigned.", "The fetch button must explain that no worker is assigned.");
        Require(Named("buy_wood") == null, "The price sheet must not offer wood, which the village does not sell.");
        Require(Named("buy_berries") == null,
            "The price sheet must not offer berries, which the village does not sell.");
        Require(Named("buy_water") != null, "The price sheet must offer water, which the village sells.");
        await Click(Named("buy_water")!);
        Require(world.Orders.Count == 1 && world.Orders[0].Buying, "Buy on the price sheet must order goods.");
        await Click(Named("hardhat")!);
        var dear = world.Data.HireOffers.OrderBy(o => o.Upfront).Last();
        await Click(Named($"hire_{dear.Upfront:0}")!);
        var hired = Hosted.Workers.LastOrDefault();
        Require(hired != null && hired.Wage == dear.Wage, "A hiring offer must set the new worker's wage.");
        Hosted.Assign(hired!, sign); await ClickOn(sign);
        Require(!Named("fetch")!.Disabled, "With a worker assigned, fresh prices can be fetched.");
        await Click(Named("fetch")!);
        Require(hired!.Away > 0, "Fetching must send the assigned worker to the village.");
        await Click(Named("prices")!);
        Require(!ShowsChild(panel, "side_board") && panel.OffsetLeft == SidePanel.Left, "The price sheet icon must close the sheet again.");
        await Click(Named("prices")!); await Click(Named("sheet_close")!);
        Require(!ShowsChild(panel, "side_board") && panel.Visible, "The price sheet's close button must close it and leave the panel open.");
        Hosted.Fire(hired); Hosted.Tick(world.Data.TradeSeconds); RemoveThings(Hosted, t => t == sign || t.Kind == ThingKind.Grave); Drop(Hosted, Inventory.Coins, 999);
        await CloseWithEsc();
    }

    /// <summary>A held stack shows its count by the cursor: the whole stack by left click, half of one by right click.</summary>
    private async Task CheckCarriedStackShowsItsCount() {
        Drop(Hosted, "wood", world.Backpack["wood"]);
        Require(Give(Hosted, "wood", 10) == 10, "The backpack must have room for ten wood.");
        var crate = Hosted.Add(ThingKind.Crate, world.Player + new Vector2(-1, 0));
        await ClickOn(crate);
        await PickUp(await Showing(1, c => c.TooltipText.Contains("Wood · 10 total")));
        await Until(() => CarriedCount() == "10", "the held stack of ten showing 10 by the cursor", 2);
        await Click(Cell(0, c => c.TooltipText == ""));
        await Until(() => crate.Inventory["wood"] == 10 && cursor.Empty, "a click on the crate putting the ten wood down there", 2);
        await PickUpHalf(await Showing(0, c => c.TooltipText.StartsWith("Wood")));
        await Until(() => CarriedCount() == "5", "a right click on ten wood picking up half and showing 5 by the cursor", 2);
        await Click(Cell(1, c => c.TooltipText == ""));
        await Until(() => world.Backpack["wood"] == 5 && crate.Inventory["wood"] == 5, "the picked up half going into the backpack", 2);
        RemoveThings(Hosted, t => t == crate);
        await CloseWithEsc();
    }

    /// <summary>The side panel and the backpack drawer move where they are dragged and stay on screen, and open in their usual spots next time.</summary>
    private async Task CheckWindowsMoveByDragAndOpenInTheirUsualSpots() {
        var crates = new[] { Hosted.Add(ThingKind.Crate, world.Player + new Vector2(-1, 0)), Hosted.Add(ThingKind.Crate, world.Player + new Vector2(1, 1)) };
        await ClickOn(crates[0]);
        await Until(() => bag.Visible, "the backpack drawer opening beside the crate", 2);
        var (panelAt, bagAt) = (panel.GlobalPosition, bag.GlobalPosition);
        var (panelBy, bagBy) = (new Vector2(-300, 80), new Vector2(250, -120));
        await MoveWindow(panel, panelBy);
        Require(panel.GlobalPosition.IsEqualApprox(panelAt + panelBy), $"Dragging the side panel must move it with the mouse: from {panelAt} to {panel.GlobalPosition}.");
        await MoveWindow(bag, bagBy);
        Require(bag.GlobalPosition.IsEqualApprox(bagAt + bagBy), $"Dragging the backpack drawer must move it with the mouse: from {bagAt} to {bag.GlobalPosition}.");
        await MoveWindow(bag, new Vector2(-2000, 0));
        Require(bag.GlobalPosition.X == 0, $"The backpack drawer must stop at the left edge of the screen, not at {bag.GlobalPosition.X}.");
        Give(Hosted, "wood", 1);
        await Holding(Key.Shift, async () => await Click(await Showing(1, c => c.TooltipText.StartsWith("Wood"))));
        await Until(() => crates[0].Inventory["wood"] > 0, "the wood reaching the crate", 2);
        Require(panel.GlobalPosition.IsEqualApprox(panelAt + panelBy) && bag.GlobalPosition.X == 0, "Moving items must leave both windows where they were dragged.");
        await ClickOn(crates[1]);
        await Until(() => bag.Visible, "the backpack drawer opening beside the second crate", 2);
        Require(panel.GlobalPosition.IsEqualApprox(panelAt) && bag.GlobalPosition.IsEqualApprox(bagAt), "Another crate's page must open both windows in their usual spots.");
        RemoveThings(Hosted, crates.Contains);
        await CloseWithEsc();
    }

    /// <summary>A panel dragged to the left edge makes room for the filter window, and a press on a well's own surface moves the panel too.</summary>
    private async Task CheckFilterWindowOpensOnScreenBesideAMovedPanel() {
        var chest = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(2, -2));
        await ClickOn(chest);
        await MoveWindow(panel, new Vector2(-4000, 0));
        Require(panel.GlobalPosition.X == 0, $"The side panel must stop at the left edge of the screen, not at {panel.GlobalPosition.X}.");
        await Click(Named("filters")!);
        await Until(() => ShowsChild(panel, "filter_in"), "the filter window opening", 2);
        Require(panel.GlobalPosition.X >= 0, $"The filter window must open on screen beside a panel at the left edge, not at {panel.GlobalPosition.X}.");
        var at = panel.GlobalPosition;
        await MoveWindow((Control)panel.FindChild("filter_in", true, false)!.GetParent(), new Vector2(0, 60));
        Require(panel.GlobalPosition.IsEqualApprox(at + new Vector2(0, 60)), $"Dragging a well in the filter window must move the panel: from {at} to {panel.GlobalPosition}.");
        RemoveThings(Hosted, t => t == chest);
        await CloseWithEsc();
    }

    private async Task CheckCratePutIntoCrateStopsAtWarning() {
        var crate = Hosted.Add(ThingKind.Crate, world.Player + new Vector2(1, 0));
        var box = Hosted.Add(ThingKind.Crate, world.Player); box.Inventory.Add("berries", 3); Hosted.PickUpCrate(box);
        string packed = Hosted.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        // A crate selected on the hotbar only opens a clicked crate; the alert waits for a deliberate move.
        string lastSlot = world.HotbarItem(8); int active = world.ActiveHotbar;
        Give(Hosted, "crate", 1); SetHotbarSlot(Hosted, 8, "crate"); await KeyStroke(Key.Key9); await CloseWithEsc();
        await Click(view.Screen(crate.Pos, .4f));
        Require(!blackHole.Showing, "Clicking a crate with a crate selected on the hotbar must not show the nesting alert.");
        Require(Shows(crate), "Clicking a crate with a crate selected on the hotbar must select the clicked crate.");
        Require(crate.Inventory.Count == 0, "Clicking a crate with a crate selected on the hotbar must not put that crate into it.");
        Require(world.Backpack["crate"] == 1, "Clicking a crate with a crate selected on the hotbar must keep that crate in the backpack.");
        SetHotbarSlot(Hosted, 8, lastSlot); await KeyStroke(Key.Key1 + active); await CloseWithEsc();
        // Two empty crates go down one after the other without opening, and placement ends with the last.
        Give(Hosted, "crate", 1);
        await CloseWithEsc(); await KeyStroke(Key.Tab);
        await PickUp(Cell(0, c => c.TooltipText.StartsWith("Wood crate ·")));
        for (int placed = 1; placed <= 2; placed++) {
            var spot = SpotsNearby()
                .First(p => world.Construction.PlacementBlock(ThingKind.Crate, p, "crate") == null && Uncovered(view.Screen(p)));
            await Click(view.Screen(spot));
            await Until(() => world.Backpack["crate"] == 2 - placed && (world.Held?.Count ?? 0) == 2 - placed, "a click on open ground placing one held crate", 2);
            Require(panel.Page == Page.Backpack, "Placing a crate must not open it.");
            Require(player.Mode.Placing == (placed == 1 ? ThingKind.Crate : null),
                "Placing a held crate must stay in placement while crates remain held.");
        }
        RemoveThings(Hosted, t => t.Kind == ThingKind.Crate && t != crate && t.Inventory.Count == 0);
        await ClickOn(crate);
        await Carry(Cell(1, c => c.TooltipText.StartsWith("Wood crate")), Cell(0, c => c.TooltipText == ""));
        Require(blackHole.Showing, "A crate put into a crate must show the nesting alert.");
        Require(blackHole.Asking, "A crate put into a crate must show the nesting warning.");
        await Until(() => world.Backpack[packed] == 1, "the crate going back to the backpack", 2);
        Require(crate.Inventory.Count == 0, "A crate put into a crate must leave the target crate empty.");
        await Click((Control)blackHole.FindChild("anomaly_no", true, false));
        Require(!blackHole.Showing, "No must close the black hole question.");
        Require(world.Backpack[packed] == 1, "No must keep the packed crate in the backpack.");
        Require(world.Find(crate.Id) != null, "No must keep the placed crate on the ground.");
        await Carry(await Showing(1, c => c.TooltipText.StartsWith("Wood crate")), Cell(0, c => c.TooltipText == ""));
        await Click((Control)blackHole.FindChild("anomaly_yes", true, false));
        Require(ShowsChild(blackHole, "collapse") && !blackHole.Asking, "Yes must open the black hole.");
        await Until(() => blackHole.FindChild("game_over", true, false) is Control { Visible: true, Modulate.A: > .99f }, "the collapse ending on the game over screen", 10);
        await Click((Control)blackHole.FindChild("menu_after_collapse", true, false));
        Require(!blackHole.Showing, "Main menu must leave the black hole.");
        Require(startMenu.Visible, "Main menu must open the start menu.");
        Require(FindButton(startMenu.Screen, "New game") != null,
            "The start menu must offer New game after the black hole.");
    }

    /// <summary>What changed while the player was away shows as it now is when they come back, without playing the change in front of them.</summary>
    private async Task CheckChangesOutOfSightDoNotReplayWhenSeenAgain() {
        await CloseWithEsc();
        var (start, home) = (Hosted.Player, Terrain.Spawn);
        Warp(Hosted, home); await Frames(4);
        var tree = Hosted.Add(ThingKind.Tree, home + new Vector2(3, 3), 42);
        var chest = Hosted.Add(ThingKind.Chest, home + new Vector2(-3, 3)); chest.Inventory.Add("wood", 3);
        var rock = Hosted.Add(ThingKind.Stone, home + new Vector2(0, 6), World.StonePile);
        await Frames(4);
        await StandAtIronCliff();
        Require(!world.Sees(tree.Pos) && world.Remembered(tree.Id)?.Remaining == 42, "The player must remember the tree standing once they have walked away.");
        var stump = Fell(Hosted, tree); chest.Inventory.Add("wood", 5);
        var chip = Hosted.Add(ThingKind.LooseStone, rock.Pos + new Vector2(1.2f, 0), 1);
        await Frames(4);
        Warp(Hosted, home); await Frames(3);
        Require(view.PropOf(stump.Id) is DugProp { Shown: true } && view.PropOf(tree.Id) is not { Shown: true } && view.Departing == 0, "A tree felled out of sight must be a stump when seen again, not topple then.");
        Require(view.PropOf(chest.Id) is BoxProp { Puff: 0, Open: 0 }, "A chest filled out of sight must keep its lid shut when seen again.");
        Require(view.PropOf(chip.Id) is LooseProp { Flight: >= WorldModels.ChipFlight }, "A chip knocked off a rock out of sight must lie where it fell when seen, not fly from the rock then.");
        RemoveThings(Hosted, t => t == stump || t == chest || t == rock || t == chip); Warp(Hosted, start); await Frames(4);
    }
    /// <summary>Launch opens the start menu with no save to continue, and Esc does not leave it.</summary>
    private async Task CheckLaunchOpensTheStartMenuWithNoSaveToContinue() {
        await Frames(4);
        Require(!view.Warming, "The models drawn while loading must be gone by the time the start menu is up.");
        Require(startMenu.Visible, "Launch must show the start menu.");
        Require(view.Paused, "Launch must hold the world.");
        Require(FindButton(startMenu.Screen, "New game") != null, "Launch must offer a new game.");
        Require(FindButton(startMenu.Screen, "Continue")!.Disabled, "Launch must offer no save to continue.");
        float launchTime = world.Time; await KeyStroke(Key.Escape);
        Require(startMenu.Visible && world.Time == launchTime, "Esc must not leave the start menu, and the world must not advance behind it.");
    }

    /// <summary>Each option takes effect at once and shows its choice when the options open again.</summary>
    private async Task CheckOptionsApplyAndKeepTheirChoices() {
        await Click(FindButton(startMenu.Screen, "Options")!);
        var picker = (OptionButton)startMenu.Screen.FindChild("resolution", true, false);
        Require(picker.Selected == Array.IndexOf(StartMenu.Resolutions, new Vector2I(1440, 900)), "Options must show the default resolution before one is chosen.");
        await Choose(picker, 0);
        Require(GetWindow().ContentScaleSize == StartMenu.Resolutions[0], "Choosing a resolution must render at it.");
        // 720 rows against the 900 the interface is laid out for, at 0.8 of its size.
        Require(layer.Scale.IsEqualApprox(Vector2.One * .64f) && ui.Size.IsEqualApprox(new Vector2(2000, 1125)), "Choosing a resolution must keep the interface's share of the screen.");
        var limits = (OptionButton)startMenu.Screen.FindChild("frame_limit", true, false);
        Require(limits.Selected == 0, "The frame rate limit must start on its first choice.");
        Require(limits.GetItemText(0) == "Off", "The frame rate limit's first choice must be Off.");
        Require(Engine.MaxFps == 0, "The engine must start with no frame rate limit.");
        await Choose(limits, 2);
        Require(Engine.MaxFps == 90, "Choosing a frame rate limit must cap the frame rate.");
        var fullscreen = (CheckButton)startMenu.Screen.FindChild("fullscreen", true, false);
        // The headless display these checks run on has no window modes, so the screen filling is checked by playing the real window.
        Require(!fullscreen.ButtonPressed, "The game must start in a window.");
        await Click(fullscreen);
        Require(GetWindow().ContentScaleSize == StartMenu.Resolutions[0], "Switching fullscreen on must keep rendering at the chosen resolution.");
        var counter = (CheckButton)startMenu.Screen.FindChild("fps", true, false);
        var frameRate = (Label)ui.FindChild("frame_rate", true, false);
        Require(counter.ButtonPressed && frameRate.Visible && frameRate.Text.EndsWith(" fps"), "The frame rate counter must start on and show a rate.");
        await Click(counter);
        Require(!frameRate.Visible, "Switching the frame rate counter off must hide it at once.");
        var musicSlider = (HSlider)startMenu.Screen.FindChild("music", true, false);
        Require(musicSlider.Value == 100, "Music must start at full volume.");
        await Click(musicSlider);
        Require(Mathf.IsEqualApprox(sounds.MusicVolume, .5f), "The music slider must set the music volume.");
        async Task ReopenOptions() {
            await KeyStroke(Key.Escape);
            await Click(FindButton(startMenu.Screen, "Options")!);
            picker = (OptionButton)startMenu.Screen.FindChild("resolution", true, false);
            limits = (OptionButton)startMenu.Screen.FindChild("frame_limit", true, false);
            counter = (CheckButton)startMenu.Screen.FindChild("fps", true, false);
            fullscreen = (CheckButton)startMenu.Screen.FindChild("fullscreen", true, false);
            musicSlider = (HSlider)startMenu.Screen.FindChild("music", true, false);
        }
        await ReopenOptions();
        Require(picker.Selected == 0, "Reopened options must show the chosen resolution.");
        Require(limits.Selected == 2, "Reopened options must show the chosen frame rate limit.");
        Require(fullscreen.ButtonPressed, "Reopened options must show fullscreen on.");
        Require(!counter.ButtonPressed, "Reopened options must show the frame rate counter off.");
        Require(musicSlider.Value == 50, "Reopened options must show the chosen music volume.");
        // The later checks measure the screen at the design size.
        await Choose(picker, Array.IndexOf(StartMenu.Resolutions, Settings.DesignSize));
        await Choose(limits, 0);
        Require(Engine.MaxFps == 0, "Turning the frame rate limit off must lift the cap.");
        await Click(fullscreen);
        await ReopenOptions();
        Require(limits.Selected == 0 && !fullscreen.ButtonPressed, "Reopened options must show the choices changed back.");
        await KeyStroke(Key.Escape);
        Require(FindButton(startMenu.Screen, "New game") != null, "Esc in options must return to the start menu.");
    }

    /// <summary>Anti-aliasing offers off, FSR upscaling, FSR at full size and TAA, names the size each draws the world at for the chosen resolution, and keeps its choice.</summary>
    private async Task CheckAntiAliasingChoicesNameTheSizeTheWorldIsDrawnAt() {
        await Click(FindButton(startMenu.Screen, "Options")!);
        var smoothing = (OptionButton)startMenu.Screen.FindChild("anti_aliasing", true, false);
        var picker = (OptionButton)startMenu.Screen.FindChild("resolution", true, false);
        string[] Names() => Enumerable.Range(0, smoothing.ItemCount).Select(smoothing.GetItemText).ToArray();
        Require(Names().SequenceEqual(["Off", "FSR Upscale (964 × 603)", "FSR Native (1440 × 900)", "TAA"]), "Anti-aliasing must offer Off, FSR Upscale and FSR Native with the size they draw at, and TAA.");
        Require(smoothing.Selected == 2 && !GetViewport().UseTaa && GetViewport().Scaling3DMode == Viewport.Scaling3DModeEnum.Fsr2 && GetViewport().Scaling3DScale == 1, "Anti-aliasing must start on FSR Native.");
        await Choose(smoothing, 1);
        Require(!GetViewport().UseTaa && GetViewport().Scaling3DMode == Viewport.Scaling3DModeEnum.Fsr2 && Mathf.IsEqualApprox(GetViewport().Scaling3DScale, .67f), "FSR Upscale must upscale with FSR 2 from two thirds of the render size, without TAA.");
        await Choose(smoothing, 2);
        Require(!GetViewport().UseTaa && GetViewport().Scaling3DMode == Viewport.Scaling3DModeEnum.Fsr2 && GetViewport().Scaling3DScale == 1, "FSR Native must smooth with FSR 2 at full render size, without TAA.");
        await Choose(picker, Array.IndexOf(StartMenu.Resolutions, new Vector2I(1280, 720)));
        Require(Names()[1] == "FSR Upscale (857 × 482)" && Names()[2] == "FSR Native (1280 × 720)", "The FSR choices must name the sizes for a newly chosen resolution.");
        await KeyStroke(Key.Escape);
        await Click(FindButton(startMenu.Screen, "Options")!);
        smoothing = (OptionButton)startMenu.Screen.FindChild("anti_aliasing", true, false);
        Require(smoothing.Selected == 2 && Names()[2] == "FSR Native (1280 × 720)", "Reopened options must show FSR Native chosen, at the chosen resolution.");
        await Choose(smoothing, 0);
        Require(!GetViewport().UseTaa && GetViewport().Scaling3DMode == Viewport.Scaling3DModeEnum.Bilinear && GetViewport().Scaling3DScale == 1, "Turning anti-aliasing off must render at full size without TAA or FSR.");
        await Choose(smoothing, 3);
        Require(GetViewport().UseTaa && GetViewport().Scaling3DMode == Viewport.Scaling3DModeEnum.Bilinear && GetViewport().Scaling3DScale == 1, "Choosing TAA must turn it on at full render size.");
        await Choose(smoothing, 2);
        // The later checks measure the screen at the design size.
        await Choose((OptionButton)startMenu.Screen.FindChild("resolution", true, false), Array.IndexOf(StartMenu.Resolutions, Settings.DesignSize));
        await KeyStroke(Key.Escape);
    }

    /// <summary>The paint filter switch starts on, hides the filter at once when switched off and keeps its choice.</summary>
    private async Task CheckPaintFilterSwitchDrawsTheFilter() {
        await Click(FindButton(startMenu.Screen, "Options")!);
        var paint = (CheckButton)startMenu.Screen.FindChild("paint_filter", true, false);
        Require(paint.ButtonPressed && paintFilter.Visible, "The paint filter must start on.");
        await Click(paint);
        Require(!paintFilter.Visible, "Switching the paint filter off must hide it at once.");
        await KeyStroke(Key.Escape);
        await Click(FindButton(startMenu.Screen, "Options")!);
        paint = (CheckButton)startMenu.Screen.FindChild("paint_filter", true, false);
        Require(!paint.ButtonPressed, "Reopened options must show the paint filter off.");
        await Click(paint);
        Require(paintFilter.Visible, "Switching the paint filter on must draw it again.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>At 4K the interface keeps its share of the screen, and its text is drawn at the size it shows rather than drawn small and stretched.</summary>
    private async Task CheckInterfaceTextIsDrawnAtTheSizeItShows() {
        await Click(FindButton(startMenu.Screen, "Options")!);
        var picker = (OptionButton)startMenu.Screen.FindChild("resolution", true, false);
        await Choose(picker, Array.IndexOf(StartMenu.Resolutions, new Vector2I(3840, 2160)));
        Require(layer.Scale.IsEqualApprox(Vector2.One * 1.92f) && ui.Size.IsEqualApprox(new Vector2(2000, 1125)) && Mathf.IsEqualApprox(GetViewport().GetOversampling(), 1.92f), "At 4K the interface must keep its share of the screen, with text drawn at the size it shows.");
        // The later checks measure the screen at the design size.
        await Choose(picker, Array.IndexOf(StartMenu.Resolutions, Settings.DesignSize));
        Require(layer.Scale.IsEqualApprox(Vector2.One * .8f) && Mathf.IsEqualApprox(GetViewport().GetOversampling(), .8f), "At the design size the interface and its text must be drawn at 0.8.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>Empty save slots cannot load, and New game closes the menu on the welcome page.</summary>
    private async Task CheckNewGameStartsTheWelcomePage() {
        await Click(FindButton(startMenu.Screen, "Load game")!);
        Require(Enumerable.Range(0, Saves.SlotCount + 1).All(n => FindButton(startMenu.Screen, $"{Saves.SlotName(n)} · empty")!.Disabled), "Empty save slots must not be loadable.");
        await Click(FindButton(startMenu.Screen, "Back · Esc")!);
        await Click(FindButton(startMenu.Screen, "New game")!);
        Require(!startMenu.Visible, "New game must close the start menu.");
        Require(!view.Paused, "New game must not leave the game paused.");
        Require(ShownPage() == "A new beginning", "New game must show the welcome page.");
    }
    /// <summary>WASD jogs and Shift while moving sprints, unless the player is too thirsty.</summary>
    private async Task CheckWasdJogsAndShiftSprintsUnlessTooThirsty() {
        var home = Hosted.Player;
        var start = world.Player;
        int soundsBefore = sounds.Played;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Until(() => view.PlayerAnimation == ActorAction.Jog && world.Player.DistanceTo(start) > .25f && sounds.Heard("footsteps_default", soundsBefore),
            "WASD moving the player through Godot input with the full jogging cycle and grass footsteps", 2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Shift, Keycode = Key.Shift, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.A, Keycode = Key.A, Pressed = true });
        await Until(() => world.Me.Sprinting && view.PlayerAnimation == ActorAction.Run, "Shift while moving sprinting with the running cycle", 2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.A, Keycode = Key.A, Pressed = false });
        await Frames();
        Require(!world.Me.Sprinting, "Shift alone must not sprint once the player stops.");
        float drink = world.Thirst; SetNeeds(Hosted, thirst: 10);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.A, Keycode = Key.A, Pressed = true });
        await Frames();
        Require(!world.Me.Sprinting && view.PlayerAnimation == ActorAction.Jog, "Under 15% drink the player must be too weak to sprint.");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.A, Keycode = Key.A, Pressed = false });
        SetNeeds(Hosted, thirst: drink);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Shift, Keycode = Key.Shift, Pressed = false });
        Warp(Hosted, home); await Frames(4);
    }
    /// <summary>Food under 15% keeps the screen edges glowing.</summary>
    private async Task CheckLowFoodGlowsScreenEdges() {
        float fed = world.Hunger; SetNeeds(Hosted, hunger: 10); await Frames();
        Require(damageEdges.Strength >= .15f, "Food under 15% must keep the screen edges glowing.");
        SetNeeds(Hosted, hunger: fed);
    }
    /// <summary>Sounds rotate through variants, a howl plays as long as a wolf holds it, and music hands over and crossfades by time of day.</summary>
    private async Task CheckSoundsRotateAndMusicCrossfades() {
        Require(Sounds.Names.All(name => sounds.VariantCount(name) > 0), "Every sound must load.");
        Require(sounds.VariantCount("footsteps_default") == 6, "Numbered footstep files must group as six variants.");
        Require(sounds.VariantCount("chop_tree") == 1, "A single chop file must count as one variant.");
        Require(Math.Abs(sounds.Length("wolf_howl") - world.Data.LoneHowlSeconds) < .1 && Math.Abs(sounds.Length("wolf_howl_group") - world.Data.PackHowlSeconds) < .1,
            "A wolf must hold its howl as long as the howl recording plays.");
        // Random picks can keep to two of the three variants, about once in a thousand runs: a seed makes them the same every run.
        sounds.Reseed(1);
        var picks = Enumerable.Range(0, 12).Select(_ => { sounds.Play("anvil_work"); return sounds.LastVariant("anvil_work"); }).ToArray();
        Require(picks.Zip(picks.Skip(1)).All(p => p.First != p.Second) && picks.Distinct().Count() == 3, "Repeated sounds must rotate through their variants without repeating the previous one.");
        sounds.UpdateMusic(Sounds.MusicFadeSeconds);
        Require(sounds.DayMusicPlaying && !sounds.NightMusicPlaying, "Daytime must play the day music only.");
        sounds.SkipToDayTrackEnd();
        await Until(() => sounds.DayMusicPlaying && sounds.DayMusicIndex == 1, "a day track that plays to its end handing over to the next one", 5);
        sounds.SkipToDayTrackEnd();
        await Until(() => sounds.DayMusicPlaying && sounds.DayMusicIndex == 0, "the last day track wrapping to the first", 5);
        RunUntilNight(Hosted); await Frames(1); sounds.UpdateMusic(Sounds.MusicFadeSeconds / 2);
        Require(sounds.DayMusicPlaying && sounds.NightMusicPlaying, "Nightfall must crossfade the day music into the night music.");
        sounds.UpdateMusic(Sounds.MusicFadeSeconds); await Seconds(.5f);
        Require(!sounds.DayMusicPlaying && sounds.NightMusicPlaying, "Day music must stop once faded out.");
        RunUntilMorning(Hosted); await Frames(1); sounds.UpdateMusic(Sounds.MusicFadeSeconds * 2); float stoppedAt = sounds.NightMusicPosition;
        Require(!sounds.NightMusicPlaying, "Dawn must stop the night music.");
        Require(sounds.DayMusicPlaying, "Dawn must bring the day music back.");
        Require(stoppedAt > .2f, "Dawn must keep the night music's position.");
        RunUntilNight(Hosted); await Frames(1); sounds.UpdateMusic(.1f);
        Require(sounds.NightMusicPlaying && sounds.NightMusicPosition >= stoppedAt, "The next night must resume the music where it stopped.");
        RunUntilMorning(Hosted); await Frames(1); sounds.UpdateMusic(Sounds.MusicFadeSeconds * 2);
    }
    /// <summary>A sound is loudest at the player and fades to silence over its reach, whatever the zoom.</summary>
    private async Task CheckSoundsFadeWithDistanceFromThePlayerAtAnyZoom() {
        float zoom = view.Zoom;
        // Zoomed out, every spot played here is on screen; zoomed in, only the nearest is.
        foreach (float level in new[] { .55f, 1.6f }) {
            view.Zoom = level; await Frames(1);
            sounds.Play("anvil_work", world.Player);
            Require(sounds.LastFade == 1, $"A sound at the player must play at full volume at zoom {level}.");
            Require(!sounds.LastPlaced, "A sound at the player's own spot must stay centred as they walk on, not play from where it started.");
            sounds.Play("anvil_work", world.Player + new Vector2(4, 0)); float near = sounds.LastFade;
            Require(sounds.LastPlaced, "A sound away from the player must play from its place.");
            sounds.Play("anvil_work", world.Player + new Vector2(12, 0)); float far = sounds.LastFade;
            Require(Math.Abs(near - .5625f) < .001f && Math.Abs(far - .0625f) < .001f,
                $"A sound 4 metres from the player must play at 9/16 of its volume and one 12 metres away at 1/16, at zoom {level}: {near}, {far}.");
            int heard = sounds.Played; sounds.Play("anvil_work", world.Player + new Vector2(17, 0));
            Require(sounds.Played == heard, $"A sound 17 metres from the player must stay silent at zoom {level}.");
        }
        view.Zoom = zoom;
        int played = sounds.Played; sounds.Play("wolf_growl_distance", world.Player + new Vector2(world.Data.WolfSightRange, 0));
        Require(sounds.Played == played + 1, "A wolf's growl as it breaks into its sprint must carry to the player from where it starts.");
        sounds.Play("wolf_howl", world.Player + new Vector2(5, 0)); float close = sounds.LastFade;
        sounds.Play("wolf_howl", world.Player + new Vector2(40, 0)); float distant = sounds.LastFade;
        Require(close < 1 && distant > 0 && distant < close, "A howl must carry further than other sounds and fade with distance from the player.");
        played = sounds.Played; sounds.Play("wolf_howl", world.Player + new Vector2(world.Data.HowlRadius + 1, 0));
        Require(sounds.Played == played, "A howl beyond the howl radius must stay silent.");
    }
    /// <summary>A tree felled in sight is heard breaking as it starts to go and crashing as its wood reaches the ground, and a tall one comes down slower.</summary>
    private async Task CheckFelledTreeBreaksThenCrashesAsItLands() {
        await CloseWithEsc();
        var (start, home) = (Hosted.Player, Terrain.Spawn);
        Warp(Hosted, home); await Frames(4);
        var quick = await FellInSight(home + new Vector2(3, 3), tall: false);
        var slow = await FellInSight(home + new Vector2(3, 3), tall: true);
        foreach (var fall in new[] { quick, slow }) {
            Require(fall.LeanAtBreak < 10, "A tree must be heard breaking as it starts to go over.");
            Require(fall.LeanAtCrash == fall.Lands, $"A felled tree must crash as its wood reaches the ground at {fall.Lands:0}°, not at {fall.LeanAtCrash:0}°.");
        }
        Require(slow.Seconds > quick.Seconds, $"A tall tree must take longer to come down than a short one: {slow.Seconds:0.00} s against {quick.Seconds:0.00}.");
        Warp(Hosted, start); await Frames(4);
    }
    /// <summary>A double click on a packed crate in the backpack opens it, and its contents move to the backpack and back by Shift-click.</summary>
    private async Task CheckPackedCrateOpensFromTheBackpack() {
        // Berries already carried would join the five taken out in one stack, which Shift would move back whole.
        int carried = Drop(Hosted, "berries"); Give(Hosted, "crate", 1); Give(Hosted, "berries", 5); Give(Hosted, "stone", 1);
        var placed = Hosted.PlaceCrate("crate", Hosted.Player + new Vector2(0, 2))!;
        // The stone keeps the crate from emptying, which would close its page, when the berries come out.
        Hosted.Transfer(Hosted.Backpack, placed.Inventory, "berries", 5); Hosted.Transfer(Hosted.Backpack, placed.Inventory, "stone", 1); Hosted.PickUpCrate(placed);
        int berries = Hosted.Backpack["berries"];
        await KeyStroke(Key.Tab);
        await DoubleClick(await Showing(0, c => c.TooltipText.StartsWith("Wood crate of")));
        await Until(() => panel.Visible && panel.HeadingTitle.Text == "Wood crate", "a double click on a packed crate in the backpack opening it", 2);
        await Holding(Key.Shift, async () => await Click(await Showing(0, c => c.TooltipText.StartsWith("Berries"))));
        await Until(() => world.Backpack["berries"] == berries + 5, "Shift on berries in the open crate taking the stack out into the backpack", 2);
        await Holding(Key.Shift, async () => await Click(await Showing(1, c => c.TooltipText.StartsWith("Berries"))));
        await Until(() => world.Backpack["berries"] == berries, "Shift on the berries in the backpack packing that stack back into the open crate", 2);
        await CloseWithEsc();
        Drop(Hosted, world.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix))); Give(Hosted, "berries", carried); await Frames();
    }
    /// <summary>The driver behind `--drive` plays its command file as real input and reports each line once: a key it sends opens the backpack, and a command it does not know fails by name.</summary>
    private async Task CheckDriverPlaysItsCommandFileAsRealInput() {
        await CloseWithEsc();
        string folder = System.IO.Directory.CreateTempSubdirectory("chop-chop-meow-drive-").FullName, log = System.IO.Path.Combine(folder, "log.txt");
        // The driver starts from the `--drive` launch flag, which a check run does without: no input starts it.
        var driver = new ReviewDrive { Folder = folder }; AddChild(driver);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "cmd.txt"), "key Tab\nwiggle\n");
        await Until(() => System.IO.File.Exists(log) && System.IO.File.ReadAllText(log).Contains("wiggle"), "the driver running its command file", 5);
        await Frames();
        Require(bag.Visible, "A key the driver sends must open the backpack as a pressed key does.");
        Require(System.IO.File.ReadAllLines(log) is ["ok key Tab", var failed] && failed.StartsWith("failed wiggle"), "The driver must report each command once, as ok or as failed.");
        driver.QueueFree(); System.IO.Directory.Delete(folder, true);
        await CloseWithEsc();
    }
    /// <summary>A packed crate's page gives way to the backpack's once the last item is taken out of the crate.</summary>
    private async Task CheckEmptiedCrateGivesWayToTheBackpack() {
        PackCrate(Hosted, ("berries", 1));
        await KeyStroke(Key.Tab);
        await DoubleClick(await Showing(0, c => c.TooltipText.StartsWith("Wood crate of")));
        await Until(() => panel.HeadingTitle.Text == "Wood crate", "the packed crate opening", 2);
        await Holding(Key.Shift, async () => await Click(await Showing(0, c => c.TooltipText.StartsWith("Berries"))));
        await Until(() => panel.Page == Page.Backpack && bag.Visible, "the emptied crate's page giving way to the backpack's", 3);
        await CloseWithEsc();
        Drop(Hosted, "crate", 1); Drop(Hosted, "berries", 1); await Frames();
    }
    /// <summary>With a building's page open, Shift on an item on the hotbar moves it in as Shift on it in the backpack does.</summary>
    private async Task CheckHotbarClickMovesItemsIntoAnOpenBuilding() {
        var chest = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(2, -2));
        string before = world.HotbarItem(4);
        Give(Hosted, "stone", 3); SetHotbarSlot(Hosted, 4, "stone");
        int stone = Hosted.Backpack["stone"];
        await ClickOn(chest);
        await Holding(Key.Shift, () => Click(hotbar.Buttons[4]));
        await Until(() => chest.Inventory["stone"] == stone && world.Backpack["stone"] == 0, "Shift on stone on the hotbar storing the stack in the open chest", 2);
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == chest); Drop(Hosted, "stone", 2); SetHotbarSlot(Hosted, 4, before); await Frames();
    }
    /// <summary>A stack put on the hotbar gets the selected slot's rim in the backpack, and stays behind when Ctrl moves the rest of its item into a chest.</summary>
    private async Task CheckABoundStackIsRimmedAndStaysBehindOnCtrl() {
        var chest = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(2, -2));
        string before = world.HotbarItem(4);
        Give(Hosted, "steel_shovel", 3);
        await ClickOn(chest);
        await Carry(BagCell(c => c.TooltipText.StartsWith("Steel shovel")), hotbar.Buttons[4]);
        await Until(() => world.HotbarItem(4) == "steel_shovel" && world.HotbarStack(4) >= 0 && cursor.Empty, "the shovel put on the hotbar binding there", 2);
        var bound = BagCell(c => c.Name == $"slot_{world.HotbarStack(4)}");
        await Until(() => bound.GetThemeStylebox("normal") == Style.SlotWanted, "the bound shovel's backpack slot getting the selected hotbar slot's rim", 2);
        await Holding(Key.Ctrl, () => Click(BagCell(c => c.TooltipText.StartsWith("Steel shovel") && c != bound)));
        await Until(() => chest.Inventory["steel_shovel"] == 2 && world.Backpack["steel_shovel"] == 1 && world.HotbarItem(4) == "steel_shovel",
            "Ctrl storing every shovel but the one bound to the hotbar", 2);
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == chest); Drop(Hosted, "steel_shovel"); SetHotbarSlot(Hosted, 4, before); await Frames();
    }
    /// <summary>A press on one backpack slot let go of over another does nothing.</summary>
    private async Task CheckAPressLetGoOverAnotherSlotDoesNothing() {
        await CloseWithEsc();
        Give(Hosted, "stone", 2);
        await KeyStroke(Key.Tab);
        await PressAndLetGoOver(BagCell(c => c.TooltipText.StartsWith("Stone")), BagCell(c => c.TooltipText == ""));
        await Seconds(.4f);
        Require(cursor.Empty && world.Backpack["stone"] == 2, "A press on the stone let go of over an empty slot must pick nothing up.");
        await CloseWithEsc();
        Drop(Hosted, "stone"); await Frames();
    }
    /// <summary>
    /// A held stack goes into a chest clicked in the world, and back to its cell on Esc or a click on open ground;
    /// with nothing held, an item only selected on the hotbar stays out of a clicked chest.
    /// </summary>
    private async Task CheckHeldStackGoesIntoAClickedChestAndBackOnEscOrOpenGround() {
        await CloseWithEsc();
        var chest = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(2, -2));
        string before = world.HotbarItem(4);
        Give(Hosted, "stone", 3); SetHotbarSlot(Hosted, 4, "stone"); await KeyStroke(Key.Key5);
        await ClickOn(chest);
        await Frames(4);
        Require(chest.Inventory["stone"] == 0, "Stone only selected on the hotbar must not go into a clicked chest.");
        await CloseWithEsc(); await KeyStroke(Key.Tab);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Stone")));
        await KeyStroke(Key.Escape);
        await Until(() => cursor.Empty, "Esc letting go of the held stone", 2);
        Require(bag.Visible && world.Backpack["stone"] == 3, "Esc must only let go of the held stack, which stays in the backpack.");
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Stone")));
        await ClickGround();
        await Until(() => cursor.Empty, "a click on open ground letting go of the held stone", 2);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Stone")));
        await RightClick(world.Player + new Vector2(0, 2));
        await Until(() => cursor.Empty, "a right click in the world letting go of the held stone", 2);
        Require(world.Backpack["stone"] == 3 && world.Find(chest.Id)!.Inventory["stone"] == 0, "Letting go must leave the stone in the backpack.");
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Stone")));
        await ClickOn(chest);
        await Until(() => chest.Inventory["stone"] == 3 && cursor.Empty, "a click on a chest in the world storing the held stone", 2);
        RemoveThings(Hosted, t => t == chest); SetHotbarSlot(Hosted, 4, before); await KeyStroke(Key.Key1); await CloseWithEsc();
    }
    /// <summary>An empty crate opens as storage by double click, goods put in pack it, and picking up the open crate closes its page.</summary>
    private async Task CheckEmptyCrateOpensAsStorageAndClosesWhenPickedUp() {
        await CloseWithEsc();
        Give(Hosted, "crate", 1); Give(Hosted, "stone", 3);
        await KeyStroke(Key.Tab);
        await DoubleClick(await Showing(0, c => c.TooltipText.StartsWith("Wood crate ·")));
        await Until(() => panel.Page == Page.Crate && Cells(0).Any(), "a double click on an empty crate opening it as storage", 2);
        await Carry(BagCell(c => c.TooltipText.StartsWith("Stone")), Cell(0, c => c.TooltipText == ""));
        await Until(() => world.Backpack.Items.Keys.Any(id => id.StartsWith(World.PackedPrefix)) && Cells(0).Any(c => c.TooltipText.StartsWith("Stone")),
            "the stone packing the empty crate, whose page shows it", 2);
        Require(world.Backpack["crate"] == 0 && world.Backpack["stone"] == 0, "Packing the empty crate must use it and all three stone.");
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Wood crate of")));
        await Until(() => panel.Page == Page.Backpack, "picking up the open crate closing its page for the backpack's", 2);
        await KeyStroke(Key.Escape); await CloseWithEsc();
        Drop(Hosted, world.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix))); await Frames();
    }
    /// <summary>At a trading post a click on the bank picks up all its coins, and coins put on it pay in.</summary>
    private async Task CheckBankCoinsArePickedUpWholeAndPaidInByPuttingDown() {
        await CloseWithEsc();
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        int coins = Drop(Hosted, Inventory.Coins);
        Give(Hosted, Inventory.Coins, 40);
        await KeyStroke(Key.Tab);
        var bank = (Control)ui.FindChild("bank", true, false);
        await Carry(BagCell(c => c.TooltipText.StartsWith("Coins")), bank);
        await Until(() => sign.Wages![Inventory.Coins] == 40 && cursor.Empty, "coins put on the bank paying in", 2);
        await PickUp(bank);
        Require(world.Held is { Item: Inventory.Coins, Count: 40 }, "A click on the bank must pick up all its coins.");
        await Click(BagCell(c => c.TooltipText == ""));
        await Until(() => sign.Wages![Inventory.Coins] == 0 && world.Backpack[Inventory.Coins] == 40, "the bank's coins going into the backpack", 2);
        RemoveThings(Hosted, t => t == sign); Drop(Hosted, Inventory.Coins, 40); Give(Hosted, Inventory.Coins, coins); await CloseWithEsc();
    }
    /// <summary>A page about a thing closes when the thing goes, as a recovery box does once it is emptied.</summary>
    private async Task CheckPageClosesWhenWhatItShowsIsGone() {
        await CloseWithEsc();
        var box = Hosted.Add(ThingKind.Grave, world.Player + new Vector2(2, 0)); box.Inventory.Add("wood", 3);
        await ClickOn(box);
        await Until(() => ShownPage() == "Recovery box", "the recovery box's page opening", 3);
        await Click(FindButton(panel, "Take all")!);
        await Until(() => !panel.Visible && view.Selection.Thing == 0, "the page closing once its box is emptied and gone", 3);
        Drop(Hosted, "wood", 3);
    }
    /// <summary>Chest stacks move by deposit, carrying and halving, filter rows take and release items, and the backpack drawer closes on its own.</summary>
    private async Task CheckChestMovesSplitsAndFiltersStacks(Thing chest) {
        await CloseWithEsc(); await ClickOn(chest!);
        var deposit = Named("deposit"); Require(deposit != null, "Chest context must expose deposit action.");
        await Click(deposit!);
        Require(chest!.Inventory["wood"] > 0 && world.Backpack["steel_axe"] == 1, "Manual deposit must move materials and retain equipped tools.");
        int storedWood = chest.Inventory["wood"];
        await Carry(Cell(0, c => c.TooltipText.StartsWith("Wood")), Cell(1, c => c.TooltipText == ""));
        await Until(() => chest.Inventory["wood"] == 0 && world.Backpack["wood"] == storedWood && cursor.Empty, "a held chest stack put on the backpack moving there", 2);
        await Carry(await Showing(1, c => c.TooltipText.StartsWith("Wood")), hotbar.Buttons[3]);
        await Until(() => world.HotbarItem(3) == "wood" && cursor.Empty, "a held stack put on the hotbar assigning it", 2);
        Require(world.Backpack["wood"] == storedWood, "A held stack put on the hotbar must assign a reference without moving items.");
        await Carry(Cell(1, c => c.TooltipText.StartsWith("Wood")), Cell(0, c => c.TooltipText == ""));
        await Until(() => chest.Inventory["wood"] == storedWood, "a held backpack stack put on the chest storing it", 2);
        await SplitTo(await Showing(0, c => c.TooltipText.StartsWith("Wood")), Cell(1, c => c.TooltipText == ""));
        int half = (Math.Min(storedWood, world.Backpack.StackOf("wood")) + 1) / 2;
        await Until(() => world.Backpack["wood"] == half && chest.Inventory["wood"] == storedWood - half, $"a right click picking up half a stack and a left click putting it down ({world.Backpack["wood"]} of {half}, chest {chest.Inventory["wood"]} of {storedWood})", 2);
        await Carry(await Showing(1, c => c.TooltipText.StartsWith("Wood")), Cell(0, c => c.TooltipText == ""));
        await Until(() => chest.Inventory["wood"] == storedWood, "the half going back into the chest", 2);
        float plainHeight = panel.Size.Y;
        await Click(Named("filters")!);
        Require(Named("filters_close") != null, "Chest filters must open the filter window.");
        Require(ShowsChild(panel, "side_board"), "Chest filters must show the sheet beside the panel.");
        Require(panel.FindChild("filter_in", true, false) != null, "The filter window must show an input filter.");
        Require(panel.FindChild("filter_out", true, false) != null, "The filter window must show an output filter.");
        await Carry(Cell(0, c => c.TooltipText.StartsWith("Wood")), (Control)panel.FindChild("filter_add_in", true, false));
        await Until(() => chest.Inventory.InputFilter.Contains("wood") && cursor.Empty, "a held stack put on a filter row adding it to that filter", 2);
        Require(chest.Inventory["wood"] == storedWood, "A held stack put on a filter row must not move.");
        await Click(Named("filter_in_wood")!);
        Require(!chest.Inventory.InputFilter.Contains("wood"), "Clicking an item in a filter row must take it off.");
        await Click((Control)bag.FindChild("bag_close", true, false));
        await Click(Named("filters")!);
        Require(!bag.Visible && panel.Visible, "The backpack drawer's close button must hide it while the building's page stays open and refreshes.");
        await Click(Named("filters")!);
        await Click(Named("filters_close")!);
        Require(Named("filters_close") == null && panel.Visible, "The filter window's close button must close it and leave the panel open.");
        await Until(() => panel.Size.Y == plainHeight, "the panel shrinking back once the taller filter window closed", 2);
        await KeyStroke(Key.Tab);
        Require(!panel.Visible, "Tab must not open the side panel.");
        Require(bag.Visible, "Tab must open the backpack drawer.");
        Require(FindButton(panel, "Assign hotbar slots") == null,
            "The side panel must have no separate hotbar assignment.");
        await Click((Control)bag.FindChild("bag_close", true, false));
        Require(!bag.Visible && !panel.Visible, "On the backpack page the drawer's close button must close the page.");
        await KeyStroke(Key.Tab);
        Require(!panel.Visible && bag.Visible, "Tab must open the backpack again after it was closed.");
        await KeyStroke(Key.B);
        int carriedBeforeAssignment = world.Backpack.Count;
        await Carry(Named("build_hut")!, hotbar.Buttons[0]);
        await Until(() => world.HotbarItem(0) == "hut" && cursor.Empty, "a plan put on the hotbar assigning it", 2);
        Require(world.Backpack.Count == carriedBeforeAssignment, "A plan put on the hotbar must store a plan reference without creating items.");
    }
    private async Task CheckLoadEndsClickModeAndSelection() {
        var chest = world.Things.First(t => t.Kind == ThingKind.Chest);
        await CloseWithEsc(); await ClickOn(chest);
        Require(Shows(chest), "A click must select the chest.");
        await KeyStroke(Key.Key3);
        Require(player.Mode.Placing == ThingKind.Chest && view.Selection.Thing == chest.Id, "A plan's hotkey must enter placement and keep the selection.");
        await KeyStroke(Key.F9); await Frames();
        Require(player.Mode is ClickMode.Normal, "A load must end placement.");
        Require(view.Selection == default, "A load must clear the selection.");
        await CloseWithEsc();
    }
    /// <summary>A load ends a crate lift under way, since finishing it on the crate from before the load would copy the crate into the backpack.</summary>
    private async Task CheckLoadEndsCrateLiftUnderWay() {
        await CloseWithEsc();
        var crate = Hosted.Add(ThingKind.Crate, world.Player + new Vector2(1, 0));
        int carried = world.Backpack["crate"];
        await KeyStroke(Key.F5);
        await Frames();
        var point = view.Screen(crate.Pos, .3f);
        Require(view.Pick(point)?.Id == crate.Id, "The crate fixture must be selectable.");
        var windowPoint = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
        await Frames();
        await KeyStroke(Key.F9);
        await Seconds(1);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
        await Frames();
        Require(world.Backpack["crate"] == carried && world.Find(crate.Id) != null, "Loading while a crate is held under the right button must leave it on the ground, and not in the backpack too.");
        RemoveThings(Hosted, t => t.Id == crate.Id);
        await CloseWithEsc();
    }
    /// <summary>Carried items show as backpack cells, and the HUD heart, stomach and droplet track health, hunger and thirst.</summary>
    private async Task CheckBackpackCellsAndHudIconsTrackStats() {
        await KeyStroke(Key.Tab);
        Give(Hosted, Inventory.Coins, 1200);
        await Until(() => Slots().Count(c => c.TooltipText.StartsWith("Coins")) == 2, "carried coins showing as backpack cells stacking to 1000", 2);
        Drop(Hosted, Inventory.Coins, 1200);
        // A wolf at the player's side, at night, bites on the next tick: the flash needs a hit from the world, not a changed number.
        float healthBefore = world.Health;
        RunUntilNight(Hosted); PlaceWolf(Hosted, new Wolf { Pos = world.Player + Vector2.Right * .5f, Target = world.Me.Id });
        await Until(() => world.Health < healthBefore - 3 && damageEdges.Strength > 0, "a wolf bite flashing the screen edges", 2);
        RemoveWolves(Hosted, _ => true); float bitten = world.Health;
        await Until(() => needsPlate.Health.TooltipText == $"Health {bitten:0}%", "the HUD heart tracking health", 2);
        RunUntilMorning(Hosted); SetNeeds(Hosted, health: healthBefore);
        Require(!HasLabel(panel, "Hunger") && !HasLabel(panel, "Thirst"), "Hunger and thirst belong to the HUD icons, not the inventory.");
        SetNeeds(Hosted, 40, 70);
        await Until(() => needsPlate.Hunger.TooltipText == "Hunger 40%", "the HUD stomach tracking hunger", 2);
        await Until(() => needsPlate.Thirst.TooltipText == "Thirst 70%", "the HUD droplet tracking thirst", 2);
        Require(needsPlate.Hunger.Visible, "The HUD stomach must show.");
        Require(needsPlate.Thirst.Visible, "The HUD droplet must show.");
        Require(Slots().Count() == world.Backpack.Slots, "The backpack must show one grid cell per slot.");
        Give(Hosted, "berries", 2);
        // Berries may already be carried, so the window shows a berry cell before the two given here reach it.
        int berriesBeforeClick = Hosted.Backpack["berries"];
        await Until(() => world.Backpack["berries"] == berriesBeforeClick && Slots().Any(c => c.TooltipText.StartsWith("Berries")), "carried berries appearing as a grid cell", 2);
        await DoubleClick(Slots().First(c => c.TooltipText.StartsWith("Berries")));
        await Until(() => world.Backpack["berries"] == berriesBeforeClick - 1, "a double click on a berry cell eating one berry", 2);
        Require(cursor.Empty, "A double click on a berry cell must not pick the berries up.");
        await CloseWithEsc();
    }
    /// <summary>A mauled player cannot move; death shows the respawn countdown and respawning clears it.</summary>
    private async Task CheckPinnedPlayerStaysDownAndDeathCountsDown() {
        var home = Hosted.Player;
        RunUntilNight(Hosted); SetMauled(Hosted, true);
        // The pin lasts only while a wolf holds it.
        PlaceWolf(Hosted, new Wolf { Pos = world.Player + Vector2.Right * .8f, Feeding = true, Pinning = true }); await Frames();
        var pinnedAt = world.Player;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Seconds(.25);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        await Frames();
        Require(world.Player == pinnedAt, "A pinned player must not move.");
        await Seconds(ActorAnimation.FallBackSeconds);
        Require(view.PlayerAnimation == ActorAction.Mauled, "A pinned player must show the mauled struggle once down.");
        Require(deathLabel.Text == "", "A pinned player must leave the message to the pinned notice.");
        Hosted.Die(); await Until(() => world.Dead && deathLabel.Text.StartsWith("Respawning in"), "a wolf kill showing only the respawn countdown", 2);
        Hosted.Tick(world.RespawnIn); await Until(() => !world.Dead && deathLabel.Text == "", "the countdown clearing once the player respawns", 2);
        RunUntilMorning(Hosted); SetNeeds(Hosted, health: 100);
        Hosted.Die(); await Until(() => world.Dead && deathLabel.Text.StartsWith("You collapsed\nRespawning in"), "a collapse showing its cause over the respawn countdown", 2);
        Hosted.Tick(world.RespawnIn); await Until(() => !world.Dead && deathLabel.Text == "", "the countdown clearing once the player respawns again", 2);
        // Dying left what the player carried in a box where they fell.
        RemoveThings(Hosted, t => t.Kind == ThingKind.Grave); await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }
    /// <summary>The steel furnace shows one slot per input and output, loads held stacks put on its input slots, and Space pumps its bellows. Returns the furnace.</summary>
    private async Task<Thing> CheckSteelFurnaceSlotsLoadHeldStacksThenPump() {
        var furnace = Hosted.Add(ThingKind.Reinforced, world.Player + new Vector2(1, 0));
        Give(Hosted, "iron_bar", 5); Give(Hosted, "coal", 100);
        await ClickOn(furnace);
        Require(Named("input_iron_bar") != null, "The steel furnace must show a slot for iron bars.");
        Require(Named("input_coal") != null, "The steel furnace must show a slot for coal.");
        Require(Named("output_steel_bar") != null, "The steel furnace must show a slot for steel bars.");
        await Carry(BagCell(c => c.TooltipText.StartsWith("Iron bar")), Named("input_iron_bar")!);
        await Until(() => furnace.Inventory["iron_bar"] == 5 && world.Backpack["iron_bar"] == 0 && cursor.Empty, "held bars put on the matching input slot loading", 2);
        await Carry(BagCell(c => c.TooltipText.StartsWith("Coal")), Named("input_iron_bar")!);
        await Frames(4);
        Require(furnace.Inventory["coal"] == 0 && !cursor.Empty, "An input slot must refuse a different item and leave it held.");
        Require(((ItemSlot)Named("input_iron_bar")!).Shaking, "An input slot that refuses a held stack must shake.");
        await RightClick(Named("input_coal")!);
        await Until(() => furnace.Inventory["coal"] == 1, "a right click with coal held putting one in its input slot", 2);
        await KeyStroke(Key.Escape);
        await PickUpHalf(Named("input_iron_bar")!);
        await Click(BagCell(c => c.TooltipText == ""));
        await Until(() => furnace.Inventory["iron_bar"] == 2 && world.Backpack["iron_bar"] == 3, "half the input slot's bars going back to the backpack", 2);
        await KeyStroke(Key.Space);
        Require(view.PlayerAnimation == ActorAction.Pump, "Manual bellows input must trigger its dedicated cycle.");
        await Until(() => view.PlayerAnimation != ActorAction.Pump, "the bellows action pulse finishing before pause checks", 3);
        return furnace;
    }
    /// <summary>While paused, Space and the pump button do nothing and animation clocks hold still; nothing plays late after resuming.</summary>
    private async Task CheckPauseFreezesPumpingAttackAndAnimations(Thing furnace) {
        await CloseWithEsc();
        await KeyStroke(Key.Escape);
        Require(view.Paused, "Escape must pause when no panel is open.");
        float heatBeforePausedActions = furnace.Heat;
        var actionBeforePausedActions = view.PlayerAnimation;
        float pausedActionTime = view.PlayerAnimationTime;
        await KeyStroke(Key.Space);
        Require(Math.Abs(furnace.Heat - heatBeforePausedActions) < .001f, "Space must not pump or attack while paused.");
        // The pause menu holds world clicks, so a player cannot get here; this proves the button stays dead if a panel ever is open.
        panel.Select(Shown(furnace));
        var pumpButton = Named("pump");
        Require(pumpButton != null, "The selected furnace should still expose its pump control while paused.");
        await Click(pumpButton!);
        Require(Math.Abs(furnace.Heat - heatBeforePausedActions) < .001f, "The pump button must not change heat while paused.");
        Require(view.PlayerAnimation == actionBeforePausedActions && Math.Abs(view.PlayerAnimationTime - pausedActionTime) < .001f, "Paused Space and pump-button input must not start an animation pulse.");
        await Click(Named("close")!);
        float animationTime = view.PlayerAnimationTime;
        await Seconds(.2);
        Require(Math.Abs(view.PlayerAnimationTime - animationTime) < .001f, "Pausing must freeze actor animation clocks.");
        await KeyStroke(Key.Escape);
        Require(!view.Paused, "Escape must resume after animation pause check.");
        await Frames();
        Require(view.PlayerAnimation != ActorAction.Pump, "Actions rejected during pause must not play delayed pulses after resume.");
        RemoveThings(Hosted, t => t == furnace);
    }
    /// <summary>A canopy in front of a building fades; one behind it or far away does not.</summary>
    private void CheckForegroundCanopyFadesOnlyOverBuildings() {
        var occluded = new Thing { Kind = ThingKind.Reinforced, Pos = world.Player };
        var foregroundTree = new Thing { Kind = ThingKind.Tree, Pos = world.Player + Vector2.One };
        Require(view.TreeCanopyCoversThing(foregroundTree, occluded), "A foreground canopy overlapping a furnace must fade.");
        foregroundTree.Pos = world.Player - Vector2.One;
        Require(!view.TreeCanopyCoversThing(foregroundTree, occluded), "Trees behind buildings must retain their opacity.");
        foregroundTree.Pos = world.Player + new Vector2(8, -8);
        Require(!view.TreeCanopyCoversThing(foregroundTree, occluded), "A distant tree must not fade for a building.");
    }
    private async void RunUIChecks() {
        try {
            await TestClean(CheckLaunchOpensTheStartMenuWithNoSaveToContinue);
            await RunControlsMenuUIChecks(inGame: false);
            await RunFirstPersonUIChecks(inGame: false);
            await TestClean(CheckOptionsApplyAndKeepTheirChoices);
            await TestClean(CheckAntiAliasingChoicesNameTheSizeTheWorldIsDrawnAt);
            await TestClean(CheckPaintFilterSwitchDrawsTheFilter);
            await TestClean(CheckInterfaceTextIsDrawnAtTheSizeItShows);
            await TestClean(CheckNewGameStartsTheWelcomePage);
            await RunControlsMenuUIChecks(inGame: true);
            await RunPauseMenuUIChecks();
            await RunGoalUIChecks(freshGame: true);
            await RunFirstPersonUIChecks(inGame: true);
            await TestClean(CheckWasdJogsAndShiftSprintsUnlessTooThirsty);
            await TestClean(CheckMessagesStackFadeAndKeepClearOfHud);
            await RunPanelUIChecks();
            await TestClean(CheckLowFoodGlowsScreenEdges);
            await TestClean(CheckSoundsRotateAndMusicCrossfades);
            await TestClean(CheckSoundsFadeWithDistanceFromThePlayerAtAnyZoom);
            await RunGatheringUIChecks();
            await RunReplantingUIChecks();
            await RunFarmUIChecks();
            await RunNettleUIChecks();
            await RunFirebugUIChecks();
            await RunLakeUIChecks();
            await RunDiaryUIChecks();
            await RunLanternUIChecks();
            await RunDebugChestUIChecks();
            await RunBuildingRemovalUIChecks();
            await RunGravelPathUIChecks();
            await TestClean(CheckPageClosesWhenWhatItShowsIsGone);
            await TestClean(CheckPackedCrateOpensFromTheBackpack);
            await TestClean(CheckDriverPlaysItsCommandFileAsRealInput);
            await TestClean(CheckEmptiedCrateGivesWayToTheBackpack);
            await TestClean(CheckHotbarClickMovesItemsIntoAnOpenBuilding);
            await TestClean(CheckABoundStackIsRimmedAndStaysBehindOnCtrl);
            await TestClean(CheckAPressLetGoOverAnotherSlotDoesNothing);
            await TestClean(CheckHeldStackGoesIntoAClickedChestAndBackOnEscOrOpenGround);
            await TestClean(CheckEmptyCrateOpensAsStorageAndClosesWhenPickedUp);
            await RunHeldPlanUIChecks();
            await TestClean(CheckBankCoinsArePickedUpWholeAndPaidInByPuttingDown);
            await RunMineUIChecks();
            await RunHudUIChecks();
            await RunHotbarUIChecks();
            var chest = await RunBuildingPlacementUIChecks();
            await Test(() => CheckChestMovesSplitsAndFiltersStacks(chest));
            await RunSaveUIChecks(chest);
            await TestClean(CheckLoadEndsClickModeAndSelection);
            await TestClean(CheckLoadEndsCrateLiftUnderWay);
            await TestClean(CheckBackpackCellsAndHudIconsTrackStats);
            await TestClean(CheckPinnedPlayerStaysDownAndDeathCountsDown);
            await RunCabinUIChecks();
            await RunStrawBedUIChecks();
            await TestClean(CheckChangesOutOfSightDoNotReplayWhenSeenAgain);
            await TestClean(CheckFelledTreeBreaksThenCrashesAsItLands);
            await TestClean(CheckTradingPostSendsOnlyCratesOffOnACart);
            await TestClean(CheckPileFilterNamesOneResource);
            await TestClean(CheckItemListRunsByGroupSimplestFirst);
            await TestClean(CheckFilterRowKeepsTheOrderAdded);
            await TestClean(CheckPriceSheetBuysGoodsAndFetchNeedsAHiredWorker);
            await RunGoalUIChecks(freshGame: false);
            await RunCraftingUIChecks();
            await RunGlassJarUIChecks();
            var furnace = await Test(CheckSteelFurnaceSlotsLoadHeldStacksThenPump);
            await Test(() => CheckPauseFreezesPumpingAttackAndAnimations(furnace));
            Test(CheckForegroundCanopyFadesOnlyOverBuildings);
            await RunWolfUIChecks();
            await RunSwingsUIChecks();
            await RunCrateUIChecks();
            await TestClean(CheckCarriedStackShowsItsCount);
            await TestClean(CheckWindowsMoveByDragAndOpenInTheirUsualSpots);
            await TestClean(CheckFilterWindowOpensOnScreenBesideAMovedPanel);
            await RunSurvivalUIChecks();
            await TestClean(CheckAutosaveTellsTheOwner);
            Test(CheckFailedSaveKeepsTheSaveBefore);
            await TestClean(CheckOthersGlideAndStopWhereTheServerPutThem);
            await TestClean(CheckTickBoxesFollowTheServer);
            await TestClean(CheckLoadRemembersWhatIsOutOfSight);
            await TestClean(CheckWorkerPageFollowsItsJobs);
            await TestClean(CheckWorkerPageShowsWhatEachPlaceIsSetToMake);
            await TestClean(CheckDraggingAJobBelowAnotherMakesItComeAfter);
            await RunStorageUIChecks();
            await RunHaulingUIChecks();
            await TestClean(CheckAWorkersLoadAndToolAreTakenAndGivenOnTheirPage);
            await TestClean(CheckAWorkerHoldingAPackedCrateSaysSoAboveTheirHands);
            await RunBarrelUIChecks();
            await TestClean(CheckCratePutIntoCrateStopsAtWarning);
            RemoveCheckFiles();
            await TestClean(CheckRestartBuildsFreshModelsForReusedIds);
            await RunWaterPumpUIChecks();
            await RunSteelPipeUIChecks();
            await RunBuildingEffectsUIChecks();
            await TestClean(CheckIntroFilmCoversTheStartMenuUntilEsc);
            GD.Print("UI-TEST PASS"); Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
