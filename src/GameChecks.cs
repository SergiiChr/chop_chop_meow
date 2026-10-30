using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    private async Task Frames(int count = 2) {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Seconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    /// <summary>Waits frame by frame until <paramref name="done"/> holds, and fails naming <paramref name="what"/> if it still does not after <paramref name="seconds"/> real seconds.</summary>
    private async Task Until(Func<bool> done, string what, double seconds = 60, [CallerFilePath] string file = "") {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (!done() && Time.GetTicksMsec() < end) await Frames(1);
        Require(done(), $"{what} never happened.", file);
    }
    private async Task KeyStroke(Key key) {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        await Frames();
    }
    /// <summary>
    /// Walking for a few hundred real frames, through Godot input, allocates no more than its budget on the main thread:
    /// every byte is garbage the runtime collects later, in a pause that stalls a frame.
    /// </summary>
    private async Task CheckWalkingFrames(long maxBytesPerFrame) {
        var key = new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = true };
        Input.ParseInputEvent(key);
        await Frames(120);
        long before = GC.GetAllocatedBytesForCurrentThread(); ulong first = Engine.GetProcessFrames();
        await Frames(300);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before; ulong played = Math.Max(1, Engine.GetProcessFrames() - first);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = false });
        await Frames();
        string report = $"frames: walking: {bytes} B over {played} frames, {bytes / (double)played:0.#} B per frame (budget {maxBytesPerFrame} B)";
        GD.Print(report);
        Require(bytes <= maxBytesPerFrame * (long)played, $"Allocation budget exceeded: {report}");
    }
    /// <summary>
    /// Every panel page, refreshed again and again while what it shows stays the same, allocates nothing:
    /// the panel refreshes several times a second while it is open.
    /// </summary>
    private async Task CheckPanelRefresh() {
        var at = world.Player;
        var chest = world.Add(ThingKind.Chest, at + new Vector2(2, 0)); chest.Inventory.Add("wood", 40); chest.Inventory.Add("coal", 12);
        var pile = world.Add(ThingKind.Pile, at + new Vector2(-4, 2)); pile.Inventory.Add("stone", 9);
        var forge = world.Add(ThingKind.Reinforced, at + new Vector2(-2, 0)); forge.Inventory.Add("iron_bar", 4); forge.Inventory.Add("coal", 6); forge.Heat = 85; forge.Processing = true; forge.Progress = 2;
        var furnace = world.Add(ThingKind.Furnace, at + new Vector2(0, 2)); furnace.Inventory.Add("iron_ore", 4);
        var fire = world.Add(ThingKind.Campfire, at + new Vector2(0, -2)); fire.Burn = 120;
        var hut = world.Add(ThingKind.Hut, at + new Vector2(4, -3));
        var sign = world.Add(ThingKind.Signpost, at + new Vector2(-4, -3));
        var farm = world.Add(ThingKind.Farm, at + new Vector2(5, 3));
        var packer = world.Add(ThingKind.Packer, at + new Vector2(-5, 4)); world.SetPacking(packer, "berries"); packer.Inventory.Add("berries", 6); packer.Inventory.Add("crate", 2);
        var tree = world.S.Things.Where(t => t.Kind == ThingKind.Tree).MinBy(t => t.Pos.DistanceTo(at))!;
        var worker = new Worker { Id = world.S.NextId++, Name = "Panel Check", Pos = at + Vector2.One, Wage = 5 };
        world.S.Workers.Add(worker); world.Assign(worker, forge); worker.Bag.Add("coal", 2);
        bool wasPaused = paused; paused = true;
        var pages = new (string Name, Action Open)[] {
            ("inventory", () => OpenPage("inventory")), ("craft", () => OpenPage("craft")), ("workers", () => OpenPage("workers")),
            ("chest", () => Select(chest)), ("pile", () => Select(pile)), ("bellows furnace", () => Select(forge)), ("furnace", () => Select(furnace)),
            ("campfire", () => Select(fire)), ("cabin", () => Select(hut)), ("signpost", () => Select(sign)), ("farm", () => Select(farm)), ("packing station", () => Select(packer)),
            ("tree", () => Select(tree)), ("worker", () => Select(worker)),
        };
        foreach (var (name, open) in pages) {
            open(); await Frames();
            // .NET first runs new code unoptimized, which boxes numbers written into text, and recompiles it optimized once it has been
            // called often, as it does a few seconds after a panel opens. So the page is refreshed in rounds until one allocates nothing;
            // garbage the code itself makes never gets there.
            long bytes = 0; int rounds = 0;
            while (rounds++ < 10) {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 50; i++) RefreshLive();
                bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                if (bytes == 0) break;
                await Seconds(.2);
            }
            string report = $"panel: {name}: {bytes} B over 50 refreshes after {rounds} rounds (budget 0 B)";
            GD.Print(report);
            Require(bytes == 0, $"Allocation budget exceeded: {report}");
        }
        ClosePanel(); paused = wasPaused;
        world.S.Workers.Remove(worker);
        foreach (var t in new[] { chest, pile, forge, furnace, fire, hut, sign, farm, packer }) world.S.Things.Remove(t);
        await Frames();
    }
    /// <summary>
    /// A new game reuses the ids of the last one, so the models the view keeps for them must be built anew:
    /// an old one would carry the last game's state, such as a felled tree faded out.
    /// </summary>
    private async Task CheckFreshModelsAfterRestart() {
        var tree = world.S.Things.Where(t => t.Kind == ThingKind.Tree).MinBy(t => t.Pos.DistanceTo(world.Player))!;
        await Frames();
        var old = view.PropOf(tree.Id) as TreeProp;
        Require(old != null, "Every tree must have its model once the world has loaded.");
        CheckTreeCanopyShadows(old!);
        old!.Fade = .68f; old.Fall = 9;
        Restart(); await Frames(2);
        var fresh = view.PropOf(tree.Id) as TreeProp;
        Require(world.Find(tree.Id)?.Kind == ThingKind.Tree && fresh != null && fresh != old && fresh.Fade == 0 && fresh.Fall < 0,
            "A new game must build fresh models rather than keep the last game's, faded or fallen.");
        ClosePanel();
    }
    /// <summary>Canopy fades use separate shadow-only meshes so the original cutout shadows survive alpha blending.</summary>
    private void CheckTreeCanopyShadows(TreeProp prop) {
        Require(prop.SwayParts.Length > 0, "A tree canopy must expose its visible meshes for fading.");
        var allMeshes = new List<MeshInstance3D>();
        void Gather(Node at) {
            if (at is MeshInstance3D mesh) allMeshes.Add(mesh);
            foreach (var child in at.GetChildren()) Gather(child);
        }
        Gather(prop.Sway);
        // The solid core that fills a canopy's shadow is separate from the per-mesh proxies.
        static bool Core(MeshInstance3D mesh) => mesh.MaterialOverride is ShaderMaterial { Shader: var shader } && shader == Tree3DShape.SolidFoliage;
        Require(allMeshes.Count(mesh => Core(mesh) && mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.ShadowsOnly) == 1,
            "A tree canopy must have one shadow-only solid core.");
        var shadows = allMeshes.Where(mesh => mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.ShadowsOnly && !Core(mesh)).ToArray();
        Require(shadows.Length == prop.SwayParts.Length, "Each visible tree canopy mesh must have exactly one shadow-only proxy.");
        // Code-built canopies fade a standard material's albedo alpha; Tree3D foliage swaps to a blended shader with an opacity.
        static float Opacity(Material material) => material is ShaderMaterial shader
            ? shader.Shader == Tree3DShape.FadingFoliage ? shader.GetShaderParameter(Tree3DShape.Opacity).AsSingle() : 1
            : ((StandardMaterial3D)material).AlbedoColor.A;
        static bool Blended(Material material) => material is ShaderMaterial shader
            ? shader.Shader == Tree3DShape.FadingFoliage
            : ((StandardMaterial3D)material).Transparency == BaseMaterial3D.TransparencyEnum.Alpha;
        var originals = new Material[prop.SwayParts.Length];
        var proxies = new MeshInstance3D[prop.SwayParts.Length];
        var proxyMaterials = new Material[prop.SwayParts.Length];
        var proxyAlphas = new float[prop.SwayParts.Length];
        var proxyBlended = new bool[prop.SwayParts.Length];
        for (int i = 0; i < prop.SwayParts.Length; i++) {
            var (mesh, material) = prop.SwayParts[i];
            originals[i] = material;
            Require(mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off,
                "Visible tree canopy meshes must leave shadow casting to their proxies.");
            Require(ReferenceEquals(mesh.MaterialOverride, material),
                "A visible tree canopy mesh must start with its original material.");
            MeshInstance3D? proxy = null;
            foreach (var candidate in shadows) {
                if (ReferenceEquals(candidate.GetParent(), mesh.GetParent()) && ReferenceEquals(candidate.Mesh, mesh.Mesh) &&
                    ReferenceEquals(candidate.MaterialOverride, material) && candidate.Transform.Equals(mesh.Transform)) {
                    proxy = candidate; break;
                }
            }
            Require(proxy != null, "Each tree canopy shadow proxy must share its mesh, material and transform with the visible mesh.");
            proxies[i] = proxy!;
            proxyMaterials[i] = proxy!.MaterialOverride!;
            proxyAlphas[i] = Opacity(proxyMaterials[i]);
            proxyBlended[i] = Blended(proxyMaterials[i]);
            Require(Mathf.IsEqualApprox(proxyAlphas[i], 1), "A tree shadow proxy must keep fully opaque foliage alpha.");
        }
        Require(prop.SwayParts.All(part => part.Mesh.CastShadow != GeometryInstance3D.ShadowCastingSetting.ShadowsOnly),
            "Shadow-only proxies must be excluded from the tree canopy fade parts.");
        ModelKit.SetFade(prop.SwayParts, .68f);
        for (int i = 0; i < prop.SwayParts.Length; i++) {
            var mesh = prop.SwayParts[i].Mesh;
            Require(mesh.MaterialOverride is Material faded &&
                Blended(faded) && Opacity(faded) < Opacity(originals[i]) &&
                mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off,
                "Fading a tree canopy must change only the visible mesh material and retain proxy shadow casting.");
            var proxy = proxies[i];
            Require(ReferenceEquals(proxy.MaterialOverride, originals[i]) &&
                Mathf.IsEqualApprox(Opacity(proxyMaterials[i]), proxyAlphas[i]) && Blended(proxyMaterials[i]) == proxyBlended[i],
                "A tree shadow proxy must retain the original material opacity while the visible mesh fades.");
        }
        ModelKit.SetFade(prop.SwayParts, 0);
        for (int i = 0; i < prop.SwayParts.Length; i++) {
            var mesh = prop.SwayParts[i].Mesh;
            Require(ReferenceEquals(mesh.MaterialOverride, originals[i]) && mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off,
                "Restoring a tree canopy must restore the original visible material while proxies keep casting shadows.");
        }
    }
    private Button? FindButton(Node parent, string text) {
        foreach (var child in parent.GetChildren()) {
            if (child is Button b && b.Text == text) return b;
            var found = FindButton(child, text); if (found != null) return found;
        }
        return null;
    }
    private Button? Named(string name) => panel.FindChild(name, true, false) as Button;
    private bool HasLabel(Node parent, string text) {
        foreach (var child in parent.GetChildren()) {
            if (child is Label label && label.Text.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (HasLabel(child, text)) return true;
        }
        return false;
    }
    /// <summary>Window position of a control's centre, scrolled into view first.</summary>
    private async Task<Vector2> At(Control control) {
        for (Node? n = control.GetParent(); n != null; n = n.GetParent()) if (n is ScrollContainer scroll) scroll.EnsureControlVisible(control);
        await Frames();
        return GetViewport().GetFinalTransform() * control.GetGlobalRect().GetCenter();
    }
    /// <summary>Moves the mouse onto a control.</summary>
    private async Task Hover(Control control) {
        var at = await At(control);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await Frames();
    }
    /// <summary>Opens an option button with a click and picks an item with the arrow keys and Enter.</summary>
    private async Task Choose(OptionButton picker, int index) {
        await Click(picker);
        // The popup opens with nothing focused, so the first press lands on the first item.
        for (int i = 0; i <= index; i++) await KeyStroke(Key.Down);
        await KeyStroke(Key.Enter);
    }
    private async Task Click(Control control) {
        var at = await At(control);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
        await Frames();
    }
    /// <summary>Two presses, the second flagged as a double click the way Godot reports one.</summary>
    private async Task DoubleClick(Control control) {
        var at = await At(control);
        foreach (bool second in new[] { false, true }) {
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true, DoubleClick = second });
            await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
            await Frames();
        }
    }
    /// <summary>A real mouse drag: press, move in steps past the drag threshold, release over the target.</summary>
    private async Task DragTo(Control from, Control to) {
        var start = await At(from); var end = await At(to);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = start, GlobalPosition = start, Pressed = true });
        await Frames();
        for (int i = 1; i <= 8; i++) {
            var at = start.Lerp(end, i / 8f);
            Input.ParseInputEvent(new InputEventMouseMotion { ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Relative = (end - start) / 8 });
            await Frames(1);
        }
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = end, GlobalPosition = end, Pressed = false });
        await Frames();
    }
    /// <summary>A right click on a stack, a move to the target and a left click there.</summary>
    private async Task SplitTo(Control from, Control to) {
        var start = await At(from); var end = await At(to);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = start, GlobalPosition = start, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = start, GlobalPosition = start, Pressed = false });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = end, GlobalPosition = end, Relative = end - start });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = end, GlobalPosition = end, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = end, GlobalPosition = end, Pressed = false });
        await Frames();
    }
    /// <summary>The panel's grids in order, then the backpack drawer's.</summary>
    private ItemSlot Cell(int grid, Func<ItemSlot, bool> match) => panel.FindChildren("*", "GridContainer", true, false).Concat(bag.FindChildren("*", "GridContainer", true, false))
        .ElementAt(grid).GetChildren().OfType<ItemSlot>().First(match);
    private System.Collections.Generic.IEnumerable<Button> Slots() => panel.FindChildren("slot_*", "Button", true, false).Concat(bag.FindChildren("slot_*", "Button", true, false)).OfType<Button>();
    private async Task Click(Vector2 point) {
        var at = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
        await Frames();
    }
    /// <summary>Messages keep clear of the HUD, stack, restart on a repeat, make room by dropping the stalest and fade out.</summary>
    private async Task CheckMessages() {
        ClosePanel(); await Frames();
        Require(messages.GetGlobalRect().Position.X > needsPlate.GetGlobalRect().End.X && messages.GetGlobalRect().End.X < ui.Size.X - needsPlate.GetGlobalRect().End.X + 1,
            "Messages must keep clear of the needs plate and stay centred.");
        OpenPage("craft"); await Frames();
        Require(messages.GetGlobalRect().End.X < panel.GetGlobalRect().Position.X, "Messages must keep to the left of an open side panel.");
        ClosePanel();
        messages._Process(60);
        Require(messages.Shown.Length == 0, "Old messages must be gone before the message check.");
        Notify("First"); Notify("Second"); messages._Process(3); Notify("First");
        Require(messages.Shown.SequenceEqual(new[] { "First", "Second" }), "A repeated message must restart its line instead of stacking again.");
        messages._Process(4.5);
        Require(messages.Shown.SequenceEqual(new[] { "First" }), "Each message must disappear after its own time.");
        Notify("Third"); messages._Process(1); Notify("First"); Notify("Fourth"); Notify("Fifth"); Notify("Sixth");
        Require(messages.Shown.SequenceEqual(new[] { "First", "Fourth", "Fifth", "Sixth" }), "A full stack must drop the message closest to fading, even when a repeat kept an older line.");
        var lines = messages.GetChildren().OfType<Label>().ToArray();
        Require(lines.Count(l => l.Visible) == 4 && lines[3].Text == "Sixth" && lines.All(l => l.Modulate.A == 1), "Every stacked message must show at full strength on its own line, newest last.");
        messages._Process(6.5);
        Require(lines[0].Modulate.A is > 0 and < 1, "A message must fade out over its last second.");
        messages._Process(1);
        Require(messages.Shown.Length == 0 && lines.All(l => !l.Visible), "Every message must disappear once its time is up.");
    }
    private async Task CheckSignpost() {
        ClosePanel(); await Frames();
        var sign = world.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        world.S.Backpack.Add("wood", 5); world.S.Backpack.Add(Inventory.Coins, 100);
        var berries = world.Add(ThingKind.Crate, world.Player + new Vector2(0, 1)); berries.Inventory.Add("berries", 12); world.PickUpCrate(berries);
        string packed = world.S.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        await Click(view.Screen(sign.Pos, .8f));
        Require(selected == sign.Id && Named("sell") != null && Named("coins") != null && Named("hardhat") != null && Named("prices") != null, "A trading post must open the market with its icon buttons.");
        await Click(Cell(2, c => c.TooltipText.StartsWith("Wood ·")));
        Require(sign.Inventory.Count == 0 && world.S.Backpack["wood"] == 5, "Loose goods must stay in the backpack: the market only takes crates.");
        await Click(Cell(2, c => c.TooltipText.StartsWith("Wood crate of 12 Berries")));
        Require(sign.Inventory[packed] == 1 && world.S.Backpack[packed] == 0, "Clicking a packed crate must load it into the market input.");
        await Click(Named("sell")!);
        Require(world.S.Orders.Count == 1 && sign.Inventory.Count == 0, "Sell must send the market input off on a cart.");
        await Click(Named("auto_sell")!);
        Require(sign.AutoSell, "Ticking Send when full must set the post to send the cart on its own.");
        await Click(Named("filters")!); await Click(Named("filter_add_in")!); await Click(Named($"pick_{World.CrateOf("berries")}")!);
        Require(sign.Inventory.InputFilter.SetEquals(new[] { World.CrateOf("berries") }) && Named("filter_add_out") == null && Named("pick_berries") == null,
            "The post's filter must offer crates of goods, not loose goods, and only a row of what workers bring.");
        await Click(Named("filters_close")!);
        var fetch = Named("fetch");
        Require(sheet.Visible && panel.OffsetLeft == PanelLeft - SheetWidth && fetch != null && fetch.Disabled && fetch.TooltipText == "No worker assigned.", "The price sheet must open beside the panel and explain why no one can fetch prices.");
        Require(Named("buy_wood") == null && Named("buy_berries") == null && Named("buy_water") != null, "The price sheet must only offer what the village sells.");
        await Click(Named("buy_water")!);
        Require(world.S.Orders.Count == 2 && world.S.Orders[1].Buying, "Buy on the price sheet must order goods.");
        await Click(Named("hardhat")!);
        var dear = world.Data.HireOffers.OrderBy(o => o.Upfront).Last();
        await Click(Named($"hire_{dear.Upfront:0}")!);
        var hired = world.S.Workers.LastOrDefault();
        Require(hired != null && hired.Wage == dear.Wage, "A hiring offer must set the new worker's wage.");
        world.Assign(hired!, sign); Select(sign); await Frames();
        Require(!Named("fetch")!.Disabled, "With a worker assigned, fresh prices can be fetched.");
        await Click(Named("fetch")!);
        Require(hired!.Away > 0, "Fetching must send the assigned worker to the village.");
        await Click(Named("prices")!);
        Require(!sheet.Visible && panel.OffsetLeft == PanelLeft, "The price sheet icon must close the sheet again.");
        await Click(Named("prices")!); await Click(Named("sheet_close")!);
        Require(!sheet.Visible && panel.Visible, "The price sheet's close button must close it and leave the panel open.");
        world.S.Workers.Remove(hired); world.S.Orders.Clear(); world.S.Things.Remove(sign); world.S.Backpack.Remove(Inventory.Coins, 999);
        signpostTab = "market"; sheetOpen = true; ClosePanel(); await Frames();
    }
    /// <summary>A new packing station opens its list of goods; picking one sets what it packs, and a backpack click loads it.</summary>
    private async Task CheckPackingStation() {
        ClosePanel(); await Frames();
        var packer = world.Add(ThingKind.Packer, world.Player + new Vector2(2, 0));
        world.S.Backpack.Add("berries", 5);
        await Click(view.Screen(packer.Pos, .8f));
        Require(selected == packer.Id && Named("pack_berries") != null, "A packing station with nothing chosen must open its list of goods.");
        await Click(Named("pack_berries")!);
        Require(packer.Packs == "berries" && Named("pack_berries") == null && Named("packed_crate") != null, "Picking berries must set what the station packs and show the crate being packed.");
        await Click(Slots().First(c => c.TooltipText.StartsWith("Berries ·")));
        Require(packer.Inventory["berries"] == 1, "Clicking berries in the backpack must load one into the packing station.");
        world.S.Things.Remove(packer); world.S.Backpack.Remove("berries", 99); ClosePanel(); await Frames();
    }
    /// <summary>Marks a mine on every ore through the build plan and a ground click, then checks its stages and the end goals in the pause menu.</summary>
    private async Task CheckMine() {
        var home = world.Player;
        foreach (var ore in World.MineOres) {
            var cliff = World.CliffOf(ore);
            var spot = World.MineSpot(cliff);
            world.Player = spot + cliff.Face * 3; await Frames(4);
            await KeyStroke(Key.B);
            await Click(Named("build_mine")!);
            Require(view.BuildMode == ThingKind.Mine, "The mine plan must enter placement mode.");
            // Off the exact spot, so the click shows the plan snapping to the cliff.
            await Click(view.Screen(spot + cliff.Face * .8f + cliff.Along * .8f));
            var mine = world.S.Things.LastOrDefault(t => t.Kind == ThingKind.Mine);
            Require(mine != null && mine.Pos == spot && selected == mine.Id && view.BuildMode == null, $"A click near the {ore} cliff must mark a mine on its spot and select it.");
            Require(new[] { "Stage 1 · Dig the entrance", "Stage 2 · Shore it up", "Stage 3 · Fit the frame and rails" }.Select((title, i) => panel.FindChild($"stage_{i + 1}", true, false) is Control node && node.TooltipText.StartsWith(title)).All(found => found),
                "The mine panel must show its three stages.");
        }
        world.Player = home; ClosePanel();
        await KeyStroke(Key.Escape);
        Require(HasLabel(menu, "END GOALS") && HasLabel(menu, "A working iron mine") && HasLabel(menu, $"{world.Data.Goals.DailyProfit} coins profit a day"), "The pause menu must list the end goals.");
        await KeyStroke(Key.Escape);
    }
    /// <summary>Clicks open ground beside the player, so the click uses the item in hand instead of selecting something.</summary>
    private async Task ClickGround() {
        await Frames();
        var open = Enumerable.Range(1, 6).SelectMany(r => Enumerable.Range(0, 8).Select(a => world.Player + Vector2.Right.Rotated(a * Mathf.Tau / 8) * r))
            .Select(p => view.Screen(p)).First(p => view.Pick(p) == null && view.PickWorker(p) == null && GetViewport().GetVisibleRect().HasPoint(p) && p.Y < GetViewport().GetVisibleRect().Size.Y - 120);
        await Click(open);
    }

    private async Task CheckCraftingAndTorch() {
        ClosePanel(); world.S.Backpack.Remove("stone_chisel", 99); world.S.Backpack.Add("stone", 2); world.S.Backpack.Add("wood", 3);
        await KeyStroke(Key.C);
        Require(page == "craft", "C must open the crafting menu.");
        // The page rebuilds when the queue moves on, so the badge is looked up afresh.
        TextureRect ChiselBadge() => (TextureRect)panel.FindChild("section_chisel", true, false).FindChild("badge", true, false);
        var badge = ChiselBadge();
        Require(badge.Modulate != Colors.White && badge.TooltipText.Contains("Needs a chisel"), "A missing chisel must show a red badge with a hint.");
        await Click(Named("craft_stone_chisel")!);
        Require(world.S.Crafting.SequenceEqual(new[] { "stone_chisel" }) && world.S.Backpack["stone"] == 0, "Clicking a recipe must queue it and take its materials.");
        world.S.CraftTime = world.Data.Recipes["stone_chisel"].Seconds - .05f; await Frames(10); RefreshLive(); await Frames();
        Require(world.S.Backpack["stone_chisel"] == 1 && ChiselBadge().Modulate == Colors.White, "The chisel must arrive and turn its badge back to normal.");
        int wood = world.S.Backpack["wood"];
        await Click(Named("section_chisel")!);
        await Click(Named("craft_torch")!);
        world.S.CraftTime = world.Data.Recipes["torch"].Seconds - .05f; await Frames(10);
        Require(world.S.Backpack["torch"] == 1 && world.S.Backpack["wood"] == wood - 3, "A torch must be made by hand from 3 wood.");
        ClosePanel(); world.S.HotbarSlots[8] = "torch";
        await KeyStroke(Key.Key9);
        Require(world.S.Torch == 0, "Selecting a torch must not light it.");
        await ClickGround();
        Require(world.S.TorchKindling > 0 && world.S.Backpack["torch"] == 0 && view.PlayerAnimation == ActorAction.Kindle, "The first click must strike flint and steel with the torch in hand.");
        await Until(() => world.S.TorchLit, "the torch catching after the lighting animation", World.KindleSeconds + 2);
        await ClickGround();
        Require(world.TorchFlare > 0 && view.PlayerAnimation == ActorAction.TorchSwing, "Clicking with a lit torch must swing it.");
        // A swing made on the move plays out in full instead of giving way to the walk.
        await Until(() => view.PlayerAnimation != ActorAction.TorchSwing, "the end of a torch swing made standing still", 2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Seconds(.15);
        var from = world.Player;
        await ClickGround(); await Seconds(.25);
        bool swungWalking = view.PlayerAnimation == ActorAction.TorchSwing && world.Player.DistanceTo(from) > .3f;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        Require(swungWalking, "A torch swing made while walking must keep playing as the player walks on.");
    }

    /// <summary>A stack bigger than a crate's room fills it and the rest stays on the cursor until it is dropped elsewhere.</summary>
    private async Task CheckCrateOverflow() {
        world.S.Backpack.Remove("wood", world.S.Backpack["wood"]);
        Require(world.S.Backpack.Add("wood", 64) == 64, "The backpack must have room for a stack of wood.");
        var crates = new[] { world.Add(ThingKind.Crate, world.Player + new Vector2(-1, 0)), world.Add(ThingKind.Crate, world.Player + new Vector2(0, -1)) };
        int cap = world.Data.CrateCapacity;
        Select(crates[0]); await Frames();
        await DragTo(Cell(1, c => c.TooltipText.Contains("Wood · 64 total")), Cell(0, c => c.TooltipText == ""));
        Require(crates[0].Inventory["wood"] == cap && world.S.Backpack["wood"] == 64 - cap, "Dragging a full stack into a crate must fill it and leave the rest in the backpack.");
        Require(GetViewport().GuiIsDragging(), "What did not fit into the crate must stay held.");
        Select(crates[1]); await Frames();
        await Click(Cell(0, c => c.TooltipText == ""));
        Require(crates[1].Inventory["wood"] == cap && world.S.Backpack["wood"] == 64 - 2 * cap && GetViewport().GuiIsDragging(), "The held rest must drop into the next crate and keep what still does not fit.");
        await Click(Cell(1, c => c.TooltipText.Contains("Wood · ")));
        Require(!GetViewport().GuiIsDragging() && world.S.Backpack["wood"] == 64 - 2 * cap, "Clicking the backpack must let go of the held rest.");
        foreach (var crate in crates) world.S.Things.Remove(crate);
        ClosePanel(); await Frames();
    }

    private async Task CheckCrateInCrate() {
        var crate = world.Add(ThingKind.Crate, world.Player + new Vector2(1, 0));
        var box = world.Add(ThingKind.Crate, world.Player); box.Inventory.Add("berries", 3); world.PickUpCrate(box);
        string packed = world.S.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        // A crate in hand on the hotbar only opens a clicked crate; the alert waits for a deliberate move.
        string lastSlot = world.S.HotbarSlots[8]; int active = world.S.ActiveHotbar;
        world.S.Backpack.Add("crate", 1); world.S.HotbarSlots[8] = "crate"; world.S.ActiveHotbar = 8; ClosePanel(); await Frames();
        await Click(view.Screen(crate.Pos, .4f));
        Require(!anomaly.Visible && selected == crate.Id && crate.Inventory.Count == 0 && world.S.Backpack["crate"] == 1, "Clicking a crate with a crate in hand must open it without the nesting alert.");
        world.S.HotbarSlots[8] = lastSlot; world.S.ActiveHotbar = active;
        // Two empty crates go down one after the other without opening, and placement ends with the last.
        world.S.Backpack.Add("crate", 1); PlaceCrate("crate");
        for (int placed = 1; placed <= 2; placed++) {
            var spot = Enumerable.Range(2, 3).SelectMany(r => Enumerable.Range(0, 8).Select(a => (world.Player + Vector2.Right.Rotated(a * Mathf.Tau / 8) * r).Round()))
                .First(p => world.PlacementBlock(ThingKind.Crate, p, "crate") == null);
            await Click(view.Screen(spot));
            Require(world.S.Backpack["crate"] == 2 - placed && !panel.Visible && view.BuildMode == (placed == 1 ? ThingKind.Crate : null), "Placing a crate must not open it and must stay in placement while crates remain.");
        }
        world.S.Things.RemoveAll(t => t.Kind == ThingKind.Crate && t != crate && t.Inventory.Count == 0);
        Select(crate); await Frames();
        await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood crate")), Cell(0, c => c.TooltipText == ""));
        Require(anomaly.Visible && anomalyWarning.Visible && world.S.Backpack[packed] == 1 && crate.Inventory.Count == 0, "A crate dragged into a crate must stop at the warning.");
        await Click((Control)anomaly.FindChild("anomaly_no", true, false));
        Require(!anomaly.Visible && world.S.Backpack[packed] == 1 && world.Find(crate.Id) != null, "No must cancel and keep both crates.");
        RefreshLive();
        await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood crate")), Cell(0, c => c.TooltipText == ""));
        await Click((Control)anomaly.FindChild("anomaly_yes", true, false));
        Require(collapse.Visible && !anomalyWarning.Visible, "Yes must open the black hole.");
        await Until(() => singularity.Visible && singularity.Modulate.A > .99f, "the collapse ending on the game over screen", 10);
        await Click((Control)singularity.FindChild("menu_after_collapse", true, false));
        Require(!anomaly.Visible && backdrop.Visible && FindButton(screen, "New game") != null, "Main menu must leave the black hole for the start menu.");
    }

    private async Task Wheel(Vector2 point) {
        var at = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Position = at, GlobalPosition = at, Pressed = true, Factor = 1 });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Position = at, GlobalPosition = at, Pressed = false, Factor = 1 });
        await Frames();
    }

    private async Task CheckWheelOverPanel() {
        float zoom = view.Zoom;
        foreach (var page in new[] { "inventory", "build", "craft", "help" }) {
            OpenPage(page); await Frames(3);
            // The backpack shows only its drawer, so the wheel goes over that.
            var box = page == "inventory" ? bag.GetGlobalRect() : panel.GetGlobalRect();
            foreach (var spot in new[] { box.GetCenter(), box.Position + new Vector2(30, 30), box.End - new Vector2(30, 30) }) await Wheel(spot);
            Require(view.Zoom == zoom, $"The mouse wheel over the {page} panel must not zoom the world.");
        }
        ClosePanel(); await Frames();
        await Wheel(GetViewport().GetVisibleRect().GetCenter());
        Require(view.Zoom < zoom, "The mouse wheel over the world must still zoom.");
        view.Zoom = zoom;
    }

    private async Task CheckCabinAndCanopy() {
        ClosePanel(); await Frames();
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, 4));
        // The click lands while the player is still walking, as when running from a wolf.
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Seconds(.1);
        await Click(view.Screen(hut.Pos, 1.5f));
        await Until(() => world.S.Sheltered == hut.Id && page == "inspect" && selected == hut.Id, "a cabin clicked while walking taking the player inside and opening", 4);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        await Frames();
        float dayTime = world.S.Time;
        world.S.Time = world.Data.DaySeconds * (world.Data.NightStarts + .02f); world.S.Hunger = 5; RefreshLive();
        var sleep = Named("sleep");
        Require(sleep != null && sleep.TooltipText.Contains("too hungry"), "The sleep button must explain that the player is too hungry to sleep.");
        world.S.Time = dayTime; world.S.Hunger = 100;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = true });
        await Seconds(.2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = false });
        await Frames();
        Require(world.S.Sheltered == hut.Id, "Moving must not step the player out of the cabin.");
        await Click(FindButton(panel, "Step outside")!);
        Require(world.S.Sheltered == 0, "Step outside must leave the cabin.");
        world.S.Things.Remove(hut); ClosePanel(); await Frames();

        // A loose piece far enough behind a tree sits under its crown on screen; it must still be the one picked.
        var tree = world.S.Things.First(t => t.Kind == ThingKind.Tree && t.Pos == Terrain.Spawn + new Vector2(5, 0));
        var hidden = world.Add(ThingKind.LooseCoal, tree.Pos - new Vector2(3.3f, 3.3f), 2);
        var point = view.Screen(hidden.Pos, .15f);
        Require(view.Pick(point)?.Id == hidden.Id, "A tree crown must not steal the click from a loose piece behind it.");
        world.S.Things.Remove(hidden);
        Require(view.Pick(point) == null, "A crown alone must not select its tree, so a click in a forest swings instead.");
        Require(view.Pick(view.Screen(tree.Pos, .5f))?.Id == tree.Id, "Clicking the foot of a trunk must select its tree.");
    }
    private static void RemoveCheckFiles() {
        foreach (var path in Enumerable.Range(0, SlotCount + 1).Select(SlotPath).Append(SettingsPath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }
    private async void RunUIChecks() {
        try {
            // A failed earlier run may have left saves behind, which would enable Continue.
            RemoveCheckFiles(); ShowStart(); await Frames(4);
            Require(backdrop.Visible && paused && FindButton(screen, "New game") != null && FindButton(screen, "Continue")!.Disabled, "Launch must open the start menu, hold the world and offer no save to continue.");
            float launchTime = world.S.Time; await KeyStroke(Key.Escape);
            Require(backdrop.Visible && world.S.Time == launchTime, "Esc must not leave the start menu, and the world must not advance behind it.");
            await Click(FindButton(screen, "Options")!);
            var picker = (OptionButton)screen.FindChild("resolution", true, false);
            Require(picker.Selected == Array.IndexOf(Resolutions, new Vector2I(1440, 900)), "Options must show the default resolution before one is chosen.");
            await Choose(picker, 0);
            var limits = (OptionButton)screen.FindChild("frame_limit", true, false);
            Require(limits.Selected == 0 && limits.GetItemText(0) == "Off" && Engine.MaxFps == 0, "The frame rate limit must start off.");
            await Choose(limits, 2);
            Require(Engine.MaxFps == 90, "Choosing a frame rate limit must cap the frame rate.");
            var taa = (CheckButton)screen.FindChild("taa", true, false);
            Require(taa.ButtonPressed && GetViewport().UseTaa, "Temporal anti-aliasing must start on.");
            await Click(taa);
            Require(!GetViewport().UseTaa, "Switching temporal anti-aliasing off must apply it at once.");
            var musicSlider = (HSlider)screen.FindChild("music", true, false);
            Require(musicSlider.Value == 100, "Music must start at full volume.");
            await Click(musicSlider);
            Require(Mathf.IsEqualApprox(sounds.MusicVolume, .5f), "The music slider must set the music volume.");
            await KeyStroke(Key.Escape);
            await Click(FindButton(screen, "Options")!);
            picker = (OptionButton)screen.FindChild("resolution", true, false);
            limits = (OptionButton)screen.FindChild("frame_limit", true, false);
            taa = (CheckButton)screen.FindChild("taa", true, false);
            musicSlider = (HSlider)screen.FindChild("music", true, false);
            Require(picker.Selected == 0 && limits.Selected == 2 && !taa.ButtonPressed && musicSlider.Value == 50, "Reopened options must show the stored choices.");
            await Choose(limits, 0);
            Require(Engine.MaxFps == 0, "Turning the frame rate limit off must lift the cap.");
            await Click(taa);
            Require(GetViewport().UseTaa, "Switching temporal anti-aliasing back on must apply it.");
            await KeyStroke(Key.Escape);
            await Click(FindButton(screen, "Options")!);
            limits = (OptionButton)screen.FindChild("frame_limit", true, false);
            taa = (CheckButton)screen.FindChild("taa", true, false);
            Require(limits.Selected == 0 && taa.ButtonPressed, "Reopened options must show the choices changed back.");
            await KeyStroke(Key.Escape);
            Require(FindButton(screen, "New game") != null, "Esc in options must return to the start menu.");
            await Click(FindButton(screen, "Load game")!);
            Require(Enumerable.Range(0, SlotCount + 1).All(n => FindButton(screen, $"{SlotName(n)} · empty")!.Disabled), "Empty save slots must not be loadable.");
            await Click(FindButton(screen, "Back · Esc")!);
            await Click(FindButton(screen, "New game")!);
            Require(!backdrop.Visible && !paused && page == "welcome", "New game must close the start menu and show the welcome page.");
            ClosePanel();
            await KeyStroke(Key.Escape);
            Require(menu.Visible && paused && HasLabel(menu, World.ObjectiveFor(world.S.Goal).Title), "Esc must open the pause menu with the current goal.");
            // New game and Quit ask first, and Esc or Back returns to the pause menu.
            world.Player = Terrain.Spawn + new Vector2(6, 0);
            await Click(FindButton(menu, "New game")!);
            Require(backdrop.Visible && FindButton(screen, "Start a new game") != null && world.Player != Terrain.Spawn, "New game must ask before leaving the settlement.");
            await KeyStroke(Key.Escape);
            Require(!backdrop.Visible && menu.Visible && paused, "Esc on the new game question must return to the pause menu.");
            await Click(FindButton(menu, "Quit")!);
            Require(backdrop.Visible && FindButton(screen, "Quit to desktop") != null, "Quit must ask before closing the game.");
            await Click(FindButton(screen, "Back · Esc")!);
            Require(menu.Visible && paused, "Back on the quit question must return to the pause menu.");
            await Click(FindButton(menu, "New game")!);
            await Click(FindButton(screen, "Start a new game")!);
            Require(!backdrop.Visible && !menu.Visible && !paused && page == "welcome" && world.Player == Terrain.Spawn, "Confirming a new game must start a fresh settlement.");
            ClosePanel();
            await KeyStroke(Key.Escape);
            await KeyStroke(Key.Escape);
            Require(!menu.Visible && !paused, "Esc must close the pause menu and resume.");
            var start = world.Player;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await Until(() => view.PlayerAnimation == ActorAction.Walk && world.Player.DistanceTo(start) > .25f && sounds.Last == "footsteps_default",
                "WASD moving the player through Godot input with the full walking cycle and grass footsteps", 2);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            await Frames();
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Shift, Keycode = Key.Shift, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.A, Keycode = Key.A, Pressed = true });
            await Until(() => world.Me.Sprinting && view.PlayerAnimation == ActorAction.Run, "Shift while moving sprinting with the running cycle", 2);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.A, Keycode = Key.A, Pressed = false });
            await Frames();
            Require(!world.Me.Sprinting, "Shift alone must not sprint once the player stops.");
            float drink = world.S.Thirst; world.S.Thirst = 10;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.A, Keycode = Key.A, Pressed = true });
            await Frames();
            Require(!world.Me.Sprinting && view.PlayerAnimation == ActorAction.Walk, "Under 15% drink the player must be too weak to sprint.");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.A, Keycode = Key.A, Pressed = false });
            world.S.Thirst = drink;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Shift, Keycode = Key.Shift, Pressed = false });
            await CheckMessages();
            // Measured about 115 B a frame: this check's own await of each frame and the memory of things seen for the first time on a straight walk.
            // The budget leaves a third more, so a change that adds garbage to every frame fails it.
            await CheckWalkingFrames(150);
            await CheckPanelRefresh();
            float fed = world.S.Hunger; world.S.Hunger = 10; await Frames();
            Require(damageEdges.Strength >= .15f, "Food under 15% must keep the screen edges glowing.");
            world.S.Hunger = fed;
            Require(Sounds.Names.All(name => sounds.VariantCount(name) > 0) && sounds.VariantCount("footsteps_default") == 3 && sounds.VariantCount("chop_tree") == 1, "Every sound must load, with numbered files grouped as variants.");
            var picks = Enumerable.Range(0, 12).Select(_ => { sounds.Play("anvil_work"); return sounds.LastVariant("anvil_work"); }).ToArray();
            Require(picks.Zip(picks.Skip(1)).All(p => p.First != p.Second) && picks.Distinct().Count() == 3, "Repeated sounds must rotate through their variants without repeating the previous one.");
            float musicDay = world.S.Time; sounds.UpdateMusic(Sounds.MusicFadeSeconds);
            Require(sounds.DayMusicPlaying && !sounds.NightMusicPlaying, "Daytime must play the day music only.");
            sounds.FinishDayTrack();
            Require(sounds.DayMusicPlaying && sounds.DayMusicIndex == 1, "A finished day track must hand over to the next one.");
            sounds.FinishDayTrack();
            Require(sounds.DayMusicIndex == 0, "The last day track must wrap to the first.");
            world.S.Time = world.Data.DaySeconds * (world.Data.NightStarts + .02f); sounds.UpdateMusic(Sounds.MusicFadeSeconds / 2);
            Require(sounds.DayMusicPlaying && sounds.NightMusicPlaying, "Nightfall must crossfade the day music into the night music.");
            sounds.UpdateMusic(Sounds.MusicFadeSeconds); await Seconds(.5f);
            Require(!sounds.DayMusicPlaying && sounds.NightMusicPlaying, "Day music must stop once faded out.");
            world.S.Time = musicDay; sounds.UpdateMusic(Sounds.MusicFadeSeconds * 2); float stoppedAt = sounds.NightMusicPosition;
            Require(!sounds.NightMusicPlaying && sounds.DayMusicPlaying && stoppedAt > .2f, "Dawn must stop the night music, keep its position and bring the day music back.");
            world.S.Time = world.Data.DaySeconds * (1 + world.Data.NightStarts + .02f); sounds.UpdateMusic(.1f);
            Require(sounds.NightMusicPlaying && sounds.NightMusicPosition >= stoppedAt, "The next night must resume the music where it stopped.");
            world.S.Time = musicDay; sounds.UpdateMusic(Sounds.MusicFadeSeconds * 2);
            int heard = sounds.Played; sounds.Play("anvil_work", world.Player + Vector2.One * 40);
            Require(view.OffScreen(world.Player) == 0 && view.OffScreen(world.Player + Vector2.One * 40) > Sounds.Reach && sounds.Played == heard, "A sound well beyond the screen edge must stay silent.");
            var tree = world.S.Things.First(t => t.Kind == ThingKind.Tree && t.Pos == Terrain.Spawn + new Vector2(5, 0));
            int wood = world.S.Backpack["wood"];
            var point = view.Screen(tree.Pos) - new Vector2(0, 70);
            Require(view.Pick(point)?.Id == tree.Id, "Illustrated tree hit area must select the visible resource.");
            var windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Until(() => view.PlayerAnimation == ActorAction.SteelChop && sounds.Last == "chop_tree" && world.S.Backpack["wood"] > wood,
                "holding RMB approaching and gathering a tree with the starting axe's steel stroke and chop sound", 8);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Position = windowPoint, Pressed = false });
            await Frames();
            var farm = world.Add(ThingKind.Farm, world.Player + new Vector2(-3, 3)); farm.FarmStage = FarmStage.Ripe;
            await Frames(2);
            var farmPoint = GetViewport().GetFinalTransform() * view.Screen(farm.Pos);
            Require(view.Pick(view.Screen(farm.Pos))?.Id == farm.Id, "A farm plot must be picked where it is drawn.");
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = farmPoint, GlobalPosition = farmPoint, Pressed = true });
            await Until(() => farm.Progress > 0, "holding RMB on a ripe farm plot walking over and harvesting by hand", 6);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Position = farmPoint, Pressed = false });
            await Frames(); Require(farm.Progress > 0 && farm.FarmStage == FarmStage.Ripe, "holding RMB on a ripe farm plot must walk over and harvest by hand.");
            world.S.Things.Remove(farm);
            Require(!stats.Text.Contains("Health") && !stats.Text.Contains("Food") && !stats.Text.Contains("Water") && !stats.Text.Contains("coins") && !stats.Text.Contains("Pack"), "The persistent HUD must show only the day and clock beside the brand.");
            Require(hotbarButtons.Count == 9 && world.S.HotbarSlots.Count == 9, "The bottom toolbar must show nine saved hotbar references.");
            int berries = world.S.Backpack["berries"];
            await KeyStroke(Key.Key3);
            Require(view.BuildMode == ThingKind.Chest, "A building-plan hotkey must enter placement mode.");
            await KeyStroke(Key.Key8);
            Require(world.S.ActiveHotbar == 7 && world.S.Backpack["berries"] == berries && hotbarButtons[7].GetThemeStylebox("normal") == Style.CupWanted && hotbarButtons[2].GetThemeStylebox("normal") != Style.CupWanted,
                "Key 8 must put the berries in hand with an outline, without eating them.");
            Require(view.BuildMode == null, "Selecting an item must clear any previously selected building plan.");
            int played = sounds.Played;
            await ClickGround();
            Require(world.S.Backpack["berries"] == berries - 1, "A left click on open ground must eat the berries in hand.");
            Require(sounds.Played == played + 1 && sounds.Last == "eat_berry", "Eating must play only the eating sound, not an inventory sound as well.");
            world.S.Backpack.Add("water", 1); world.S.Thirst = 20; RefreshLive();
            await Click(hotbarButtons[8]);
            Require(world.S.ActiveHotbar == 8 && world.S.Backpack["water"] == 1, "Clicking a hotbar slot must only select it.");
            await ClickGround();
            Require(world.S.Backpack["water"] == 0 && world.S.Thirst > 20, "A left click on open ground must drink the water in hand.");
            await KeyStroke(Key.B); Require(page == "build" && panel.Visible, "B must open the building plans.");
            var plan = Named("build_chest"); Require(plan != null, "Building plans must expose storage construction.");
            await Hover(plan!);
            Require(HasLabel(panel, "Needs: Wood") && HasLabel(panel, "5 slots"), "Hovering a plan must show its description and required resources.");
            await Click(plan!);
            Require(view.BuildMode == ThingKind.Chest && !panel.Visible, "Choosing a plan must enter placement mode.");
            Vector2 place = Vector2.Zero;
            for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++) {
                var candidate = world.Player.Round() + new Vector2(x, y);
                if (world.Player.DistanceTo(candidate) <= 3 && world.PlacementBlock(ThingKind.Chest, candidate) == null) place = candidate;
            }
            Require(place != Vector2.Zero, "A fresh clearing must offer space for construction.");
            point = view.Screen(place);
            windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = windowPoint, Pressed = false });
            await Frames();
            var chest = world.S.Things.LastOrDefault(t => t.Kind == ThingKind.Chest);
            Require(chest != null && view.BuildMode == (world.S.Backpack.Has(world.Data.Buildings[ThingKind.Chest].Cost) ? ThingKind.Chest : null) && selected != chest.Id && !panel.Visible,
                "A ground click must construct the chest without opening it, staying in placement while another can be afforded.");
            view.BuildMode = null; Select(chest!);
            var deposit = Named("deposit"); Require(deposit != null, "Chest context must expose deposit action.");
            await Click(deposit!);
            Require(chest!.Inventory["wood"] > 0 && world.S.Backpack["steel_axe"] == 1, "Manual deposit must move materials and retain equipped tools.");
            int storedWood = chest.Inventory["wood"]; RefreshLive();
            await DragTo(Cell(0, c => c.TooltipText.StartsWith("Wood")), Cell(1, c => c.TooltipText == ""));
            Require(chest.Inventory["wood"] == 0 && world.S.Backpack["wood"] == storedWood, "Dragging a chest stack onto the backpack must move it.");
            RefreshLive();
            await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood")), hotbarButtons[3]);
            Require(world.S.HotbarSlots[3] == "wood" && world.S.Backpack["wood"] == storedWood, "Dragging an item onto the hotbar must assign a reference without moving items.");
            await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood")), Cell(0, c => c.TooltipText == ""));
            Require(chest.Inventory["wood"] == storedWood, "Dragging a backpack stack onto the chest must store it.");
            RefreshLive();
            await SplitTo(Cell(0, c => c.TooltipText.StartsWith("Wood")), Cell(1, c => c.TooltipText == ""));
            int half = (Math.Min(storedWood, world.S.Backpack.StackOf("wood")) + 1) / 2;
            Require(world.S.Backpack["wood"] == half && chest.Inventory["wood"] == storedWood - half, $"A right click must pick up half a stack and a left click must drop it ({world.S.Backpack["wood"]} of {half}, chest {chest.Inventory["wood"]} of {storedWood}).");
            RefreshLive();
            await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood")), Cell(0, c => c.TooltipText == ""));
            Require(chest.Inventory["wood"] == storedWood, "The split half must go back into the chest.");
            await Click(Named("filters")!);
            Require(filterOpen && sheet.Visible && panel.FindChild("filter_in", true, false) != null && panel.FindChild("filter_out", true, false) != null, "Chest filters must open in the filter window beside the panel.");
            await DragTo(Cell(0, c => c.TooltipText.StartsWith("Wood")), (Control)panel.FindChild("filter_add_in", true, false));
            Require(chest.Inventory.InputFilter.Contains("wood") && chest.Inventory["wood"] == storedWood, "Dropping an item on a filter row must add it to that filter without moving it.");
            await Click((Button)panel.FindChild("filter_in_wood", true, false));
            Require(!chest.Inventory.InputFilter.Contains("wood"), "Clicking an item in a filter row must take it off.");
            await Click((Control)bag.FindChild("bag_close", true, false));
            await Click(Named("filters")!);
            Require(!bag.Visible && panel.Visible, "The backpack drawer's close button must hide it while the building's page stays open and refreshes.");
            await Click(Named("filters")!);
            await Click(Named("filters_close")!);
            Require(!filterOpen && panel.Visible, "The filter window's close button must close it and leave the panel open.");
            await KeyStroke(Key.Tab);
            Require(page == "inventory" && bag.Visible && FindButton(panel, "Assign hotbar slots") == null, "Tab must open the backpack drawer, with no separate hotbar assignment.");
            await Click((Control)bag.FindChild("bag_close", true, false));
            Require(!bag.Visible && page == "", "On the backpack page the drawer's close button must close the page.");
            await KeyStroke(Key.Tab);
            Require(page == "inventory" && bag.Visible, "Tab must open the backpack again after it was closed.");
            await KeyStroke(Key.B);
            int carriedBeforeAssignment = world.S.Backpack.Count;
            await DragTo(Named("build_hut")!, hotbarButtons[0]);
            Require(world.S.HotbarSlots[0] == "hut" && world.S.Backpack.Count == carriedBeforeAssignment, "Dragging a plan onto the hotbar must store a plan reference without creating items.");
            await KeyStroke(Key.I); Require(page == "help", "I must open the field guide.");
            var savedPosition = world.Player;
            var savedSlots = world.S.HotbarSlots.ToArray(); int savedActiveSlot = world.S.ActiveHotbar;
            await KeyStroke(Key.F5);
            Require(backdrop.Visible && FindButton(screen, "Slot 1 · empty") != null && FindButton(screen, "Autosave · empty") == null, "F5 in a new game must ask for a save slot and leave out the autosave.");
            await Click(FindButton(screen, "Slot 1 · empty")!);
            Require(SlotUsed(1) && !backdrop.Visible && !paused, "Choosing a slot must write the save and resume.");
            int savedWood = chest.Inventory["wood"];
            Require(savedWood > 0 && chest.Inventory.Remove("wood", 1) == 1, "The chest must hold wood to take out after saving.");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await Seconds(.25);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            await Frames(); Require(world.Player.DistanceTo(savedPosition) > .1f, "The player should move after saving.");
            await KeyStroke(Key.F9);
            Require(world.Player.DistanceTo(savedPosition) < .01f && world.S.Things.Any(t => t.Id == chest.Id && t.Inventory["wood"] == savedWood), "F9 must restore position and stored items from disk.");
            Require(world.S.HotbarSlots.SequenceEqual(savedSlots) && world.S.ActiveHotbar == savedActiveSlot, "F5 and F9 must preserve hotbar references and selection.");
            world.Player += Vector2.One; ClosePanel();
            await KeyStroke(Key.Escape);
            await Click(FindButton(menu, "Load")!);
            Require(FindButton(screen, "Slot 2 · empty")!.Disabled, "The load screen must disable empty slots.");
            await Click(screen.GetChildren().OfType<Button>().First(b => b.Text.StartsWith("Slot 1 · ")));
            Require(!backdrop.Visible && !menu.Visible && !paused && world.Player.DistanceTo(savedPosition) < .01f, "Loading from the pause menu must restore the save and resume.");
            world.S.Backpack.Add(Inventory.Coins, 1200); RefreshLive();
            Require(Slots().Count(c => c.TooltipText.StartsWith("Coins")) == 2, "Carried coins must show as backpack cells stacking to 1000.");
            world.S.Backpack.Remove(Inventory.Coins, 1200);
            float healthBefore = world.S.Health; world.S.Health -= 12; RefreshLive(); DamageFlash(healthBefore - world.S.Health);
            Require(health.TooltipText == $"Health {world.S.Health:0}%" && damageEdges.Strength > 0, "The HUD heart must track health and a hit must flash the screen edges.");
            world.S.Health = healthBefore;
            Require(!HasLabel(panel, "Hunger") && !HasLabel(panel, "Thirst"), "Hunger and thirst belong to the HUD icons, not the inventory.");
            world.S.Hunger = 40; world.S.Thirst = 70; RefreshLive();
            Require(hunger.Visible && thirst.Visible && hunger.TooltipText == "Hunger 40%" && thirst.TooltipText == "Thirst 70%", "The HUD stomach and droplet must track hunger and thirst.");
            Require(Slots().Count() == world.S.Backpack.Slots, "The backpack must show one grid cell per slot.");
            world.S.Backpack.Add("berries", 2); RefreshLive();
            var berryCell = Slots().FirstOrDefault(c => c.TooltipText.StartsWith("Berries"));
            int berriesBeforeClick = world.S.Backpack["berries"];
            Require(berryCell != null, "Carried berries must appear as a grid cell.");
            await Click(berryCell!);
            Require(world.S.Backpack["berries"] == berriesBeforeClick - 1, "Clicking a berry cell must eat one berry.");

            float dayTime = world.S.Time;
            world.S.Time = world.Data.DaySeconds * (world.Data.NightStarts + .02f); world.S.Mauled = true;
            var pinnedAt = world.Player;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await Seconds(.25);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            await Frames();
            Require(world.Player == pinnedAt && view.PlayerAnimation == ActorAction.Mauled && deathLabel.Text == "", "A pinned player must not move, must show the mauled struggle and leaves the message to the pinned notice.");
            world.S.Mauled = false; world.S.Wolves.Clear(); world.S.Time = dayTime; world.S.Health = 100;
            world.S.Health = -1; await Until(() => world.Dead && deathLabel.Text.Contains("Respawning in"), "death showing the respawn countdown", 2);
            world.S.RespawnIn = .01f; await Until(() => !world.Dead && deathLabel.Text == "", "the countdown clearing once the player respawns", 2);

            await CheckWheelOverPanel();
            await CheckCabinAndCanopy();
            await CheckSignpost();
            await CheckPackingStation();

            world.S.Milestones.UnionWith(new[] { "gather", "bench", "crate", "sold", "pick", "furnace", "iron_bar", "reinforced", "steel_bar", "anvil", "steel_tool", "sold_steel", "hired", "assigned" });
            await Frames(3);
            Require(!ending.Visible && !paused, "The starter goal chain alone must not end the game.");
            await CheckMine();
            foreach (var mine in world.S.Things.Where(t => t.Kind == ThingKind.Mine)) mine.MineStage = MineStage.Open;
            world.S.Milestones.Add("profit_week");
            await Frames(3);
            Require(ending.Visible && paused, "Reaching the end goals must show the ending and pause the world.");
            await Click((Button)ending.FindChild("keep_playing", true, false)); await Frames(3);
            Require(!ending.Visible && !paused && world.S.EndingShown, "Keep playing must close the ending and resume.");
            await Frames(3);
            Require(!ending.Visible, "The ending must not reappear after the player chose to keep playing.");
            ClosePanel();
            var workbench = world.Add(ThingKind.Bench, world.Player + new Vector2(-1, 0)); world.SetRecipe(workbench, "crate");
            Select(workbench); world.S.Backpack.Add("planks", 6); RefreshLive(); await Frames(2);
            await Click(bag.FindChildren("*", "GridContainer", true, false)[0].GetChildren().OfType<ItemSlot>().First(c => c.TooltipText.StartsWith("Wood planks")));
            Require(workbench.Inventory["planks"] == 1 && world.S.Backpack["planks"] == 5, "Clicking a backpack item at the bench must load just one.");
            await DoubleClick(Named("craft_crate")!);
            Require(workbench.Inventory["planks"] == 6 && world.S.Backpack["planks"] == 0, "Double-clicking a bench recipe must load one batch of its inputs.");
            await SplitTo(Named("input_planks")!, bag.FindChildren("*", "GridContainer", true, false)[0].GetChildren().OfType<ItemSlot>().First(c => c.TooltipText == ""));
            Require(workbench.Inventory["planks"] == 3 && world.S.Backpack["planks"] == 3, "Right-clicking a bench input must pick up half of it.");
            world.S.Things.Remove(workbench); world.S.Backpack.Remove("planks", 3); ClosePanel();
            var furnace = world.Add(ThingKind.Reinforced, world.Player + new Vector2(1, 0));
            Select(furnace);
            Require(Named("input_iron_bar") != null && Named("input_coal") != null && Named("output_steel_bar") != null, "The steel furnace must show one slot per input and output.");
            world.S.Backpack.Add("iron_bar", 5); world.S.Backpack.Add("coal", 100); RefreshLive();
            await DragTo(Cell(0, c => c.TooltipText.StartsWith("Iron bar")), Named("input_iron_bar")!);
            Require(furnace.Inventory["iron_bar"] == 5 && world.S.Backpack["iron_bar"] == 0, "Dragging bars onto the matching input slot must load them.");
            await DragTo(Cell(0, c => c.TooltipText.StartsWith("Mined coal")), Named("input_iron_bar")!);
            Require(furnace.Inventory["coal"] == 0, "An input slot must reject a different item.");
            await Click(Cell(0, c => c.TooltipText.StartsWith("Mined coal")));
            Require(furnace.Inventory["coal"] == 1, "Clicking a backpack item must add one to its input slot.");
            await Click(Named("input_iron_bar")!);
            Require(furnace.Inventory["iron_bar"] == 4 && world.S.Backpack["iron_bar"] == 1, "Clicking an input slot must return exactly one item.");
            await KeyStroke(Key.Space);
            Require(view.PlayerAnimation == ActorAction.Pump, "Manual bellows input must trigger its dedicated cycle.");
            await Until(() => view.PlayerAnimation != ActorAction.Pump, "the bellows action pulse finishing before pause checks", 3);
            ClosePanel();
            await KeyStroke(Key.Escape);
            Require(paused, "Escape must pause when no panel is open.");
            float heatBeforePausedActions = furnace.Heat;
            var actionBeforePausedActions = view.PlayerAnimation;
            float pausedActionTime = view.PlayerAnimationTime;
            await KeyStroke(Key.Space);
            Require(Math.Abs(furnace.Heat - heatBeforePausedActions) < .001f, "Space must not pump or attack while paused.");
            Select(furnace);
            var pumpButton = Named("pump");
            Require(pumpButton != null, "The selected furnace should still expose its pump control while paused.");
            await Click(pumpButton!);
            Require(Math.Abs(furnace.Heat - heatBeforePausedActions) < .001f, "The pump button must not change heat while paused.");
            Require(view.PlayerAnimation == actionBeforePausedActions && Math.Abs(view.PlayerAnimationTime - pausedActionTime) < .001f, "Paused Space and pump-button input must not start an animation pulse.");
            ClosePanel();
            float animationTime = view.PlayerAnimationTime;
            await Seconds(.2);
            Require(Math.Abs(view.PlayerAnimationTime - animationTime) < .001f, "Pausing must freeze actor animation clocks.");
            await KeyStroke(Key.Escape);
            Require(!paused, "Escape must resume after animation pause check.");
            await Frames();
            Require(view.PlayerAnimation != ActorAction.Pump, "Actions rejected during pause must not play delayed pulses after resume.");
            var occluded = new Thing { Kind = ThingKind.Reinforced, Pos = world.Player };
            var foregroundTree = new Thing { Kind = ThingKind.Tree, Pos = world.Player + Vector2.One };
            Require(view.TreeCanopyCoversThing(foregroundTree, occluded), "A foreground canopy overlapping a furnace must fade.");
            foregroundTree.Pos = world.Player - Vector2.One;
            Require(!view.TreeCanopyCoversThing(foregroundTree, occluded), "Trees behind buildings must retain their opacity.");
            foregroundTree.Pos = world.Player + new Vector2(8, -8);
            Require(!view.TreeCanopyCoversThing(foregroundTree, occluded), "A distant tree must not fade for a building.");
            ClosePanel(); selected = 0;
            world.S.Backpack.Remove("steel_axe", 99);
            await KeyStroke(Key.Space);
            Require(view.PlayerAnimation == ActorAction.Attack, "Space without an axe must trigger the dedicated unarmed attack.");
            await Until(() => view.PlayerAnimation != ActorAction.Attack, "the unarmed attack finishing", 3);
            var gatheringPosition = Terrain.Spawn - new Vector2(30, 30);
            // Full-size tree crowns reach several tiles up the screen, so clear enough room for a clean click.
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(gatheringPosition) < 12);
            world.Player = gatheringPosition;
            // Far enough that the player has to keep walking after the button is released.
            var looseOre = world.Add(ThingKind.LooseCoal, gatheringPosition + new Vector2(2, 0), 2);
            int oreBefore = world.S.Backpack["coal"];
            await Frames();
            point = view.Screen(looseOre.Pos) - new Vector2(0, 12) * view.Zoom;
            Require(view.Pick(point)?.Id == looseOre.Id, "The loose ore fixture must be selectable.");
            windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
            bool harvestAnimation = false;
            for (int i = 0; i < 120 && world.Find(looseOre.Id) != null; i++) { await Frames(); harvestAnimation |= view.PlayerAnimation == ActorAction.Gather; }
            Require(world.Find(looseOre.Id) == null && world.S.Backpack["coal"] == oreBefore + 2, "One right-click must walk over and pick up the whole loose piece.");
            Require(!harvestAnimation, "Picking up loose ore must not play the gathering animation.");
            // Exercise the rendered bush's independent fruit handle through real gathering input.
            var berryBush = world.Add(ThingKind.Berries, world.Player + new Vector2(1, -1), 4);
            world.S.Backpack.Remove("berries", 99);
            await Frames(2);
            var bushProp = (BushProp)view.PropOf(berryBush.Id)!;
            Require(bushProp.Fruit is { Visible: true }, "A ripe berry bush must display its fruit.");
            point = view.Screen(berryBush.Pos, .35f);
            Require(view.Pick(point)?.Id == berryBush.Id, "The bush foliage must select its berry resource.");
            windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Until(() => berryBush.Remaining == 0, "holding RMB on a berry bush picking it clean", world.Data.GatherSeconds + 3);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
            await Frames(2);
            Require(berryBush.Remaining == 0 && world.S.Backpack["berries"] == 4 && !bushProp.Fruit.Visible && bushProp.Body.Visible,
                "Harvesting must collect the berries and hide only the fruit, leaving the bush visible.");
            berryBush.Remaining = 4; await Frames(2);
            Require(bushProp.Fruit.Visible, "A replenished bush must show its fruit again.");
            await CheckCraftingAndTorch();
            await CheckCrateOverflow();
            await CheckSurvivalInventoryUI();
            await CheckCrateInCrate();
            RemoveCheckFiles();
            await CheckFreshModelsAfterRestart();
            GD.Print("UI-TEST PASS"); Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
