using NeoRune;
using UE.Angelscript;
using UE.CoreUObject;
using UE.Engine;
using UE.SlateCore;
using UE.UMG;

namespace ModKit;

/// <summary>
/// The game's item panel, made of its own widgets, for a panel or drawer of yours that looks like the inventory page's:
/// its header ribbon (the "Equipped" module, retitled) over its tooltip panel (frame, background blur and button bar).
/// Your content goes in the panel's upper part, its scrolling lower part and its button bar.
/// <code>
/// var panel = GamePanel.Build(this, tree, "Loadouts", upper, lower, hints, out var title, out var ribbon);
/// if (panel == null) { /* the game's widgets are gone (a game update): a panel of your own */ }
/// ...
/// GamePanel.KeepTitle(ribbon, title, "Loadouts");   // every frame: the ribbon can put its own words back
/// </code>
/// Lay the content out at the game's sizes (its 4K units) inside a scale box, as the inventory page is.
/// </summary>
public static class GamePanel
{
    public const string PanelClass = "/OreUI/UI/Widget/Panel/W_TooltipContentPanel.W_TooltipContentPanel_C";
    public const string RibbonClass = "/OreUI/UI/Widget/Tooltip/Modules/W_TooltipModule_Equipped.W_TooltipModule_Equipped_C";

    /// <summary>
    /// The ribbon over the panel, in a column; null when the game's panel isn't there. <paramref name="bar"/> can be null
    /// (no button bar).
    /// </summary>
    public static UVerticalBox? Build(UUserWidget owner, UWidgetTree tree, string title, UWidget upper, UWidget lower, UWidget? bar,
        out UTextBlock? titleText, out UUserWidget? ribbon)
    {
        titleText = null;
        ribbon = null;
        var panelWidget = Create(owner, PanelClass);
        var panel = panelWidget as UAS_SpicewoodTooltipContentPanel;
        var upperSlot = panelWidget != null ? GameUI.Find(panelWidget, "UpperContent") as UNamedSlot : null;
        var column = Ui.Column(tree);
        if (panel == null || upperSlot == null || panel.LowerContent == null || column == null) return null;
        ribbon = Create(owner, RibbonClass);
        if (ribbon != null)
        {
            titleText = GameUI.FindOfClass(ribbon, Unreal.ClassOf<UTextBlock>()) as UTextBlock;
            titleText?.SetText(title);
            column.AddChild(ribbon);
        }
        upperSlot.SetContent(upper);
        panel.LowerContent.SetContent(lower);
        if (bar != null && panel.ActionBarBorder != null)
        {
            // In place of the game's own bar (it shows the actions of the game's screens).
            panel.ActionBarBorder.SetContent(bar);
            panel.IsActionBarVisible = true;
            panel.ActionBarBorder.SetVisibility(ESlateVisibility.Visible);
        }
        var slot = column.AddChild(panelWidget) as UVerticalBoxSlot;
        slot?.SetSize(new FSlateChildSize { Value = 1, SizeRule = ESlateSizeRule.Fill });
        return column;
    }

    /// <summary>Keeps the ribbon showing, with your title (it's one of the game's tooltip modules: it can change itself).</summary>
    public static void KeepTitle(UUserWidget? ribbon, UTextBlock? titleText, string title)
    {
        if (ribbon == null) return;
        if (!ribbon.IsVisible()) ribbon.SetVisibility(ESlateVisibility.HitTestInvisible);
        titleText?.SetText(title);
    }

    /// <summary>One of the game's widgets by its class path, for the owner's player; null when the class isn't there.</summary>
    public static UUserWidget? Create(UUserWidget owner, string path)
    {
        var widgetClass = Unreal.LoadClass<UUserWidget>(path);
        if (widgetClass == null) return null;
        Trail.Begin("GamePanel: " + path);
        var widget = UWidgetBlueprintLibrary.Create(owner, widgetClass, World.PlayerController(owner));
        Trail.End("GamePanel: " + path);
        return widget;
    }
}
