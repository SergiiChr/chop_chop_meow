using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of hauling: a chest's and a placed crate's Haul, and dragging hauls into haul groups.</summary>
    private async Task RunHaulingUIChecks() {
        await TestClean(CheckChestAndPlacedCrateOfferHaulAndCrateFiltersNameNoCrate);
        await TestClean(CheckAHaulDragsIntoAHaulGroupAndStaysWhenItIsTakenApart);
        await TestClean(CheckAJobBelowAHaulGroupDragsIntoIt);
    }

    /// <summary>A chest and a placed crate offer Haul, and a placed crate's filters name goods but no crate, since a crate never goes into one.</summary>
    private async Task CheckChestAndPlacedCrateOfferHaulAndCrateFiltersNameNoCrate() {
        await CloseWithEsc();
        var chest = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(-2, 0));
        var crate = Hosted.Add(ThingKind.Crate, world.Player + new Vector2(2, 0)); await Frames();
        panel.Select(Shown(chest)); await Frames();
        Require(OnPage("job_haul") != null, "A chest's page must offer Haul.");
        panel.Select(Shown(crate)); await Frames();
        Require(OnPage("job_haul") != null, "A placed crate's page must offer Haul.");
        await Click(Named("filters")!);
        Require(OnPage("filter_in") != null && OnPage("filter_out") != null, "A placed crate's filter must show a row of what workers bring and one of what they take.");
        await Click(Named("filter_add_in")!);
        Require(Named("pick_wood") != null && Named("pick_crate") == null && Named($"pick_{World.CrateOf("berries")}") == null,
            "A placed crate's filter must offer goods but no crate.");
        await Click(Named("pick_wood")!);
        await Until(() => Shown(crate).Inventory.InputFilter.SequenceEqual(new[] { "wood" }), "picking wood adding it to the placed crate's filter", 2);
        await Click(Named("filters_close")!);
        RemoveThings(Hosted, t => t == chest || t == crate);
        await CloseWithEsc();
    }

    private async Task CheckAHaulDragsIntoAHaulGroupAndStaysWhenItIsTakenApart() {
        var (bench, worker) = await OpenBenchWorkerPage();
        await Click(Named("new_group")!);
        await Until(() => OnPage("group_1") != null, "the new haul group's box", 2);
        Require(HasLabel(OnPage("group_1")!, "Drag haul jobs here"), "A new haul group must say what to do with it.");
        var held = await HoldOver(await At(OnPage("grip_1")!), await At(OnPage("group_1")!));
        await LetGo(held);
        await Until(() => OnPage("group_1")?.FindChild($"drop_haul_{bench.Id}", true, false) != null, "the haul showing inside the group", 2);
        Require(HasLabel(OnPage("group_1")!, "1 place"), "A haul group must count its places.");
        await Click(Named("ungroup_1")!);
        await Until(() => OnPage("group_1") == null && Named($"drop_haul_{bench.Id}") != null, "the group gone and its haul back as a job of its own", 2);
        await CloseWorkerPage(bench, worker);
    }

    private async Task CheckAJobBelowAHaulGroupDragsIntoIt() {
        var (bench, worker) = await OpenBenchWorkerPage();
        var anvil = Hosted.Add(ThingKind.Anvil, world.Player + new Vector2(-4, 0));
        Hosted.SetDuty(worker, anvil, JobKind.Craft, true); Hosted.SetDuty(worker, anvil, JobKind.Haul, true);
        await Until(() => OnPage("job_3") != null, "the worker's page listing the anvil's jobs", 2);
        await Click(Named("new_group")!);
        await Until(() => OnPage("group_1") != null, "the new haul group's box", 2);
        await LetGo(await HoldOver(await At(OnPage("grip_1")!), await At(OnPage("group_1")!)));
        await Until(() => OnPage("group_1")?.FindChild($"drop_haul_{bench.Id}", true, false) != null, "the bench's haul showing inside the group", 2);
        // The group goes above the anvil's haul, which is then the row right under its box.
        await LetGo(await HoldOver(await At(OnPage("grip_3")!), await AtFoot(Named($"drop_craft_{anvil.Id}")!)));
        await Until(() => OnPage("group_1") is Control box && Named($"drop_haul_{anvil.Id}") is Button below && box.GlobalPosition.Y < below.GlobalPosition.Y, "the group landing above the anvil's haul", 2);
        // The anvil's haul is the fourth entry, the group, holding the bench's haul, the third.
        var held = await HoldOver(await At(OnPage("grip_3")!), await At(OnPage("group_1")!));
        Require(OnPage("lifted") != null, "A job right under a haul group must lift onto the mouse like any other.");
        await LetGo(held);
        await Until(() => OnPage("group_1")?.FindChild($"drop_haul_{anvil.Id}", true, false) != null, "the anvil's haul showing inside the group", 2);
        RemoveThings(Hosted, t => t == anvil);
        await CloseWorkerPage(bench, worker);
    }
}
