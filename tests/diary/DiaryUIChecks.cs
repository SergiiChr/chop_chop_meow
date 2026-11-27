using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The diary in the window, in order: each check reads the days the ones before it found.</summary>
    private async Task RunDiaryUIChecks() {
        await TestClean(CheckAFindWaitsBehindTakeAndOpensItsDayWithTheBadgeStampedAfter);
        await TestClean(CheckClickingAFallenPagePicksItUpAndTheBadgeMarksUnreadDays);
        await TestClean(CheckARightClickWadesToTheLakePage);
        await TestClean(CheckAPageInTheShallowsFloatsOnTheWater);
        await TestClean(CheckTheDiaryGoalCountsPagesAndOpensTheDiary);
        await TestClean(CheckAFindInAHeldGatherSaysNothingOfBeingBusy);
        await TestClean(CheckTheLastDaysWritingFallsApartWithNoMarkupShowing);
    }

    private async Task CheckAFindWaitsBehindTakeAndOpensItsDayWithTheBadgeStampedAfter() {
        await CloseWithEsc();
        Require(!hotbar.DiaryBadge.Visible, "The diary's badge must wait for the first find.");
        TurnUpFind(HostedMe, DiaryFind.Berries);
        await Until(() => findPrompt.Visible, "the pop-up of a page found in a bush", 3);
        Require(HasLabel(findPrompt, "You see a piece of paper stuck in the bush.") && findPrompt.FindChildren("*", nameof(Button), true, false).Count == 1,
            "The pop-up must say where the page was found and offer Take alone.");
        Require(!await PlayerWalks(), "A player with a find waiting must stay where they stand.");
        await KeyStroke(Key.Escape); await KeyStroke(Key.Escape);
        Require(!menu.Visible && findPrompt.Visible, "Esc must leave the pop-up up.");
        int since = sounds.Played;
        await TakeFind(40);
        await Until(() => !findPrompt.Visible && Diary.Started(world.Me), "taking the page closing its pop-up and starting the diary", 3);
        Require(sounds.Heard("page_turn", since), "Taking a page must turn a page.");
        await Frames(4);
        Require(!panel.Visible && !hotbar.DiaryBadge.Visible, "Taking the first page must open only its day, with the diary and its badge waiting behind it.");
        await KeyStroke(Key.Escape);
        await Until(() => !sheet.Visible && hotbar.DiaryBadge.Visible && hotbar.DiaryBadge.Scale.X > 1.5f, "closing the first page stamping the diary's badge into the bottom bar from large", 2);
        Require(!panel.Visible, "Closing the first page must leave the diary closed, for its badge to lead the way.");
        await Click(hotbar.DiaryBadge);
        var missing = Named("diary_day_2")!;
        var found = Named("diary_day_40")!;
        Require(missing.Disabled && HasLabel(missing, "?"), "A day not found must show a question mark on its page and not open.");
        Require(!found.Disabled && HasLabel(found, "9"), "A found day's page must show its place in the diary, day 40 being the ninth.");
        await Click(found);
        await Until(() => sheet.Visible && sheet.Day == 40, "a click on a found day's page opening it large", 3);
        since = sounds.Played;
        await KeyStroke(Key.Escape);
        Require(!sheet.Visible && panel.Page == Page.Diary && sounds.Heard("page_turn", since), "Esc must close the open day with a page turn and leave the diary open.");
        await CloseWithEsc();
    }

    private async Task CheckClickingAFallenPagePicksItUpAndTheBadgeMarksUnreadDays() {
        await CloseWithEsc();
        var home = Hosted.Player;
        int since = sounds.Played;
        var spot = SpotsNearby().First(p => world.CanStand(p) && Uncovered(view.Screen(p)));
        var page = DropPage(Hosted, HostedMe, spot);
        await Until(() => view.PropOf(page.Id) is PageProp { Shown: true }, "the page off a tree showing in the window", 3);
        Require(sounds.Heard("paper_fall", since), "A page coming off a tree in sight must rustle as it falls.");
        await ClickOn(page);
        await Until(() => findPrompt.Visible && HasLabel(findPrompt, "A piece of paper fell off a tree."), "a left click on the page walking the player over to pick it up", 8);
        await Until(() => view.PropOf(page.Id) is not { Shown: true }, "the picked up page leaving the ground", 3);
        await TakeFind(36);
        await KeyStroke(Key.Escape); await CloseWithEsc();
        var unread = hotbar.DiaryBadge.GetNode<Control>("unread");
        Require(unread.Visible, "The diary's badge must show a dot while day 37 waits to be read.");
        await Click(hotbar.DiaryBadge);
        await Until(() => panel.Page == Page.Diary, "the badge opening the diary", 3);
        var day37 = Named("diary_day_37")!;
        Require(day37.TooltipText.StartsWith("Not read yet"), "An unread day's cell must say it is not read yet.");
        await Click(day37);
        Require(sheet.Visible && sheet.Day == 37, "A day's cell must open the day large.");
        await KeyStroke(Key.Escape);
        await Until(() => !unread.Visible && !day37.TooltipText.StartsWith("Not read yet"), "reading day 37 clearing the badge's dot and its cell's mark", 3);
        await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }

    /// <summary>The lake's page drifts up to 3 m in from the shore, past where the dry shore reaches, so the player wades in to it.</summary>
    private async Task CheckARightClickWadesToTheLakePage() {
        await CloseWithEsc();
        var home = Hosted.Player;
        var page = Hosted.ThingsOf(ThingKind.FloatingPage).Single();
        var floating = Hosted.Diary.At(page);
        Warp(Hosted, Terrain.WaterPoint(floating) + (floating - Terrain.Lake).Normalized() * 2);
        await Until(() => view.PropOf(page.Id) is PageProp { Shown: true }, "the lake's page showing in the window", 5);
        await RightClick(Hosted.Diary.At(page), 0);
        await Until(() => findPrompt.Visible && HasLabel(findPrompt, "A piece of paper floating in the water."), "a right click on the lake's page wading the player out to pick it up", 12);
        await TakeFind(2);
        Require(view.PropOf(page.Id) is not { Shown: true } && Hosted.Find(page.Id) == page, "The lake's page must be gone for the player who took it and stay in the world.");
        await KeyStroke(Key.Escape); await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckAPageInTheShallowsFloatsOnTheWater() {
        await CloseWithEsc();
        var home = Hosted.Player;
        var shore = Terrain.WaterPoint(Hosted.Diary.At(Hosted.ThingsOf(ThingKind.FloatingPage).Single()));
        var inward = (Terrain.Lake - shore).Normalized();
        var page = DropPage(Hosted, HostedMe, shore + inward * 1.5f);
        Warp(Hosted, shore - inward * 2);
        await Until(() => view.PropOf(page.Id) is PageProp { Shown: true }, "the page in the shallows showing in the window", 5);
        float pageAt = view.PropOf(page.Id)!.Root.Position.Y;
        Require(pageAt == 0, $"A page in the shallows must float on the water, not lie at {pageAt:0.000}.");
        RemoveThings(Hosted, t => t == page);
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckTheDiaryGoalCountsPagesAndOpensTheDiary() {
        await CloseWithEsc();
        await KeyStroke(Key.Escape);
        await Click(FindButton(menu, "A success story")!);
        Require(HasLabel(menu.GoalDetails, "Find all the diary pages.") && HasLabel(menu.GoalDetails, "4 of 11 pages found"),
            "The diary's goal must count the days the player found: 40, 36, 37 and 2.");
        await Click((Button)menu.GoalDetails.FindChild("goal_diary", true, false));
        Require(!menu.Visible && !menu.GoalDetails.Visible && panel.Page == Page.Diary, "The goal's diary must close the menu and open the diary, as its badge does.");
        await CloseWithEsc();
    }

    private async Task CheckTheLastDaysWritingFallsApartWithNoMarkupShowing() {
        await CloseWithEsc();
        TakeEveryFind(HostedMe);
        await Click(hotbar.DiaryBadge);
        await Until(() => panel.Page == Page.Diary, "the badge opening the diary", 3);
        await Click(Named("diary_day_45")!);
        Require(sheet.Visible && sheet.Day == 45 && sheet.Reading.Contains("in the dark. I miss my") && !sheet.Reading.Contains('['),
            $"Day 45 must read on to where its writing falls apart, with no markup showing: \"{sheet.Reading}\".");
        await KeyStroke(Key.Escape); await CloseWithEsc();
    }

    private async Task CheckAFindInAHeldGatherSaysNothingOfBeingBusy() {
        await CloseWithEsc();
        var home = Hosted.Player;
        var leaves = Hosted.Add(ThingKind.LeafSpot, home + Vector2.Down * 1.25f, World.LeavesPerSpot);
        await Frames(2);
        var press = RightPress(leaves.Pos);
        await Until(() => view.PlayerAnimation == ActorAction.Gather, "holding RMB on the leaves gathering them by hand", 3);
        TurnUpFind(HostedMe, DiaryFind.Leaves);
        await Until(() => findPrompt.Visible, "a page turning up in the leaves being gathered", 3);
        await Frames(6);
        Require(!messages.Shown.Contains("You are busy."), $"A page turning up while the player holds on to a gather must not tell them they are busy: {string.Join(" | ", messages.Shown)}.");
        RightRelease(press);
        await TakeFind(10);
        await KeyStroke(Key.Escape); await CloseWithEsc();
        RemoveThings(Hosted, t => t == leaves);
        Warp(Hosted, home); await Frames(4);
    }
}
