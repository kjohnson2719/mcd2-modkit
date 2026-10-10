using System.Collections.Generic;
using NeoRune;
using UE.CoreUObject;
using UE.Engine;
using UE.SlateCore;
using UE.UMG;

namespace ModKit;

/// <summary>
/// Logs how some of the game's widgets are drawn: each widget's brush (its texture or material, how it's drawn, its
/// margins and tint), its text's font and colour, and its padding. To make the mod's widgets from the same pieces.
/// </summary>
public static class StyleDump
{
    public static void Write(UWidget? root, string title, int maxLines)
    {
        if (root == null)
        {
            Log.Write(title + ": not found");
            return;
        }
        var lines = new List<string>();
        lines.Add(title);
        Visit(root, "  ", 0, lines, maxLines);
        Log.WriteAll(lines);
    }

    static void Visit(UWidget widget, string indent, int userDepth, List<string> lines, int maxLines)
    {
        if (lines.Count >= maxLines) return;
        string line = $"{indent}{UKismetSystemLibrary.GetObjectName(widget)} : {GameUI.ClassPath(widget)}{(widget.IsVisible() ? "" : " [hidden]")}{SlotText(widget)}";
        var image = widget as UImage;
        if (image != null) line += $" | image {Brush(image.Brush)}, colour {Colour(image.ColorAndOpacity)}";
        var border = widget as UBorder;
        if (border != null) line += $" | border {Brush(border.Background)}, colour {Colour(border.BrushColor)}, padding {Margin(border.Padding)}";
        var text = widget as UTextBlock;
        if (text != null)
        {
            string shown = UKismetTextLibrary.Conv_TextToString(text.GetText());
            if (UKismetStringLibrary.Len(shown) > 30) shown = UKismetStringLibrary.Left(shown, 30);
            var font = text.Font;
            line += $" | text \"{shown}\" font {UKismetSystemLibrary.GetObjectName(font.FontObject)}/{font.TypefaceFontName} {font.Size:0} spacing {font.LetterSpacing}, colour {Colour(text.ColorAndOpacity.SpecifiedColor)}, shadow {text.ShadowOffset.X:0},{text.ShadowOffset.Y:0} {Colour(text.ShadowColorAndOpacity)}";
        }
        var box = widget as USizeBox;
        if (box != null) line += $" | size {box.WidthOverride:0}x{box.HeightOverride:0} (min {box.MinDesiredWidth:0}x{box.MinDesiredHeight:0})";
        lines.Add(line);

        var user = widget as UUserWidget;
        if (user != null)
        {
            // Into the widgets it's made of, a few levels down.
            if (userDepth < 4 && user.WidgetTree != null && user.WidgetTree.RootWidget != null)
                Visit(user.WidgetTree.RootWidget, indent + "  ", userDepth + 1, lines, maxLines);
            return;
        }
        var panel = widget as UPanelWidget;
        if (panel == null) return;
        foreach (var child in panel.GetAllChildren())
            if (child != null) Visit(child, indent + "  ", userDepth, lines, maxLines);
    }

    static string SlotText(UWidget widget)
    {
        var vertical = widget.Slot as UVerticalBoxSlot;
        if (vertical != null) return $" [pad {Margin(vertical.Padding)} {vertical.HorizontalAlignment}/{vertical.VerticalAlignment}]";
        var horizontal = widget.Slot as UHorizontalBoxSlot;
        if (horizontal != null) return $" [pad {Margin(horizontal.Padding)} {horizontal.HorizontalAlignment}/{horizontal.VerticalAlignment}]";
        var overlay = widget.Slot as UOverlaySlot;
        if (overlay != null) return $" [pad {Margin(overlay.Padding)} {overlay.HorizontalAlignment}/{overlay.VerticalAlignment}]";
        return "";
    }

    /// <summary>
    /// A material for the log: its parent and the values of its parameters (and its parent's). The game draws much of its
    /// UI with materials whose look is in their parameters, e.g. its item slots' rarity colour (TagColor).
    /// </summary>
    public static string Material(UObject? material)
    {
        var instance = material as UMaterialInstance;
        if (instance == null) return UKismetSystemLibrary.GetObjectName(material);
        string text = $"{UKismetSystemLibrary.GetObjectName(instance)} (parent {UKismetSystemLibrary.GetObjectName(instance.Parent)})" + Parameters(instance);
        var parent = instance.Parent as UMaterialInstance;
        if (parent != null) text += " | parent's:" + Parameters(parent);
        return text;
    }

    static string Parameters(UMaterialInstance instance)
    {
        string text = "";
        foreach (var value in instance.VectorParameterValues)
            text += $" {value.ParameterInfo.Name}=({value.ParameterValue.R:0.00},{value.ParameterValue.G:0.00},{value.ParameterValue.B:0.00},{value.ParameterValue.A:0.00})";
        foreach (var value in instance.ScalarParameterValues) text += $" {value.ParameterInfo.Name}={value.ParameterValue:0.00}";
        foreach (var value in instance.TextureParameterValues) text += $" {value.ParameterInfo.Name}={UKismetSystemLibrary.GetObjectName(value.ParameterValue)}";
        return text;
    }

    static string Brush(FSlateBrush brush) =>
        $"{UKismetSystemLibrary.GetObjectName(brush.ResourceObject)} {brush.DrawAs} {brush.ImageSize.X:0}x{brush.ImageSize.Y:0} margin {Margin(brush.Margin)} tint {Colour(brush.TintColor.SpecifiedColor)} tiling {brush.Tiling} outline {brush.OutlineSettings.Width:0.0}";

    static string Margin(FMargin margin) => $"{margin.Left:0.00},{margin.Top:0.00},{margin.Right:0.00},{margin.Bottom:0.00}";

    static string Colour(FLinearColor colour) => $"{colour.R:0.00},{colour.G:0.00},{colour.B:0.00},{colour.A:0.00}";
}
