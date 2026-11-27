using System;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>The hunger and effects shown together on a food tooltip or in the Recipe map sidebar.</summary>
public static class FoodDescription {
    public static ItemDef? Of(GameData data, string item) =>
        data.Items.TryGetValue(item, out var def) && def.FoodEffects.Length > 0 ? def : null;

    private static string Text(string item, ItemDef food, bool bold) {
        bool perLiter = item == BerryJam.Liquid || BerryJam.InBottle(item);
        float hunger = Math.Min(FoodEffects.NormalLimit, item == BerryJam.Liquid ? BerryJam.HungerPerLiter : food.Hunger);
        var text = new StringBuilder();
        if (food.Description.Length > 0) text.AppendLine(food.Description);
        text.Append("Hunger: ");
        if (Array.IndexOf(food.FoodEffects, FoodEffect.WellFed) >= 0) text.Append($"fills to {FoodEffects.OverloadLimit:0}%");
        else text.Append($"+{hunger:0.#}%").Append(perLiter ? " per liter" : "");
        text.AppendLine().Append("Effects:");
        foreach (FoodEffect effect in food.FoodEffects) {
            var (name, description) = FoodEffects.Describe(effect);
            text.AppendLine().Append("• ");
            if (bold) text.Append("[b]");
            text.Append(name);
            if (bold) text.Append("[/b]");
            text.Append(": ").Append(description);
        }
        return text.ToString();
    }

    internal static RichTextLabel Body(string item, ItemDef food, int size, Color color) {
        var text = new RichTextLabel {
            Name = "food_description",
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        text.AddThemeFontOverride("normal_font", Style.Body);
        text.AddThemeFontOverride("bold_font", Style.Bold);
        text.AddThemeFontSizeOverride("normal_font_size", size);
        text.AddThemeFontSizeOverride("bold_font_size", size);
        text.AddThemeColorOverride("default_color", color);
        text.Text = Text(item, food, bold: true);
        return text;
    }

    internal static void SetTooltip(ItemSlot slot, string title, string item, ItemDef food) {
        slot.TooltipText = $"{title}\n{Text(item, food, bold: false)}";
        slot.Popup = () => {
            var column = new VBoxContainer { CustomMinimumSize = new Vector2(390, 0) };
            column.AddThemeConstantOverride("separation", 5);
            column.AddChild(Style.Text(title, 16, Style.Rim));
            var body = Body(item, food, 15, Style.Rim);
            body.AutowrapMode = TextServer.AutowrapMode.Off;
            column.AddChild(body);
            return column;
        };
    }
}
