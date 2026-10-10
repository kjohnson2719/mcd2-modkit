using System.Collections.Generic;
using NeoRune;
using UE.CoreUObject;
using UE.Engine;
using UE.SlateCore;
using UE.UMG;

namespace ModKit;

/// <summary>
/// Keeps the game's menu where it is while something of the mod's is over it: the controller's stick moves the menu's
/// focus even when the mod reads the stick itself (found in game: the inventory's items changed under the loadouts
/// drawer). The mod can't take the focus (the game's menus take it back), so this stops the focused widget passing it on:
/// its navigation, every way, is "stop" until it's let go, then what it was.
/// <code>
/// lock = NavigationLock.Create(this);
/// ...
/// if (overlayOpen) lock?.Hold(); else lock?.Release();   // every frame
/// </code>
/// </summary>
public class NavigationLock : UObject
{
    const double CheckSeconds = 0.2;   // how often to look for the focused widget (the menu may move its focus)

    UUserWidget? held;
    List<EUINavigationRule> heldRules;
    double checkedAt;

    public static NavigationLock? Create(UObject owner)
    {
        var navigationLock = UGameplayStatics.SpawnObject(Unreal.ClassOf<NavigationLock>(), owner) as NavigationLock;
        if (navigationLock == null) return null;
        navigationLock.heldRules = new List<EUINavigationRule>();
        navigationLock.checkedAt = -100;
        return navigationLock;
    }

    /// <summary>Whether a widget's being held now.</summary>
    public bool Holding() => held != null;

    /// <summary>Holds the focused widget where it is (looked for now and then; cheap to call every frame).</summary>
    public void Hold()
    {
        double now = World.RealTime(this);
        if (now - checkedAt < CheckSeconds) return;
        checkedAt = now;
        var focused = Focused();
        if (focused == held) return;
        Release();
        if (focused != null) Lock(focused);
    }

    /// <summary>Lets the held widget go: its navigation as it was.</summary>
    public void Release()
    {
        checkedAt = -100;
        var widget = held;
        held = null;
        if (widget == null || !UKismetSystemLibrary.IsValid(widget) || heldRules.Count < 4) return;
        widget.SetNavigationRuleBase(EUINavigation.Up, heldRules[0]);
        widget.SetNavigationRuleBase(EUINavigation.Down, heldRules[1]);
        widget.SetNavigationRuleBase(EUINavigation.Left, heldRules[2]);
        widget.SetNavigationRuleBase(EUINavigation.Right, heldRules[3]);
    }

    /// <summary>
    /// The innermost user widget with the focus or holding it (navigation goes from the focused widget out, and the first
    /// widget with a rule other than "escape" decides: inside a list, before the list moves its own selection).
    /// </summary>
    UUserWidget? Focused()
    {
        UWidgetBlueprintLibrary.GetAllWidgetsOfClass(this, out var all, Unreal.ClassOf<UUserWidget>(), false);
        UUserWidget? best = null;
        int bestDepth = -1;
        foreach (var widget in all)
        {
            if (widget == null || !(widget.HasAnyUserFocus() || widget.HasFocusedDescendants())) continue;
            int depth = Depth(widget);
            if (depth <= bestDepth) continue;
            best = widget;
            bestDepth = depth;
        }
        return best;
    }

    /// <summary>How deep a widget is: the widgets and user widgets it's in.</summary>
    static int Depth(UWidget widget)
    {
        UObject? at = widget;
        int depth = 0;
        for (; depth < 200 && at != null; depth++)
        {
            var current = at as UWidget;
            var parent = current != null ? current.GetParent() : null;
            at = parent != null ? parent : UKismetSystemLibrary.GetOuterObject(at);
        }
        return depth;
    }

    /// <summary>Stops a widget's navigation every way, remembering its rules (not one with rules naming a widget or a function: those can't be put back).</summary>
    void Lock(UUserWidget widget)
    {
        var rules = new List<EUINavigationRule>();
        var navigation = widget.Navigation;
        if (navigation != null)
        {
            rules.Add(navigation.Up.Rule);
            rules.Add(navigation.Down.Rule);
            rules.Add(navigation.Left.Rule);
            rules.Add(navigation.Right.Rule);
        }
        else
        {
            for (int i = 0; i < 4; i++) rules.Add(EUINavigationRule.Escape);
        }
        foreach (var rule in rules)
            if (rule == EUINavigationRule.Explicit || rule == EUINavigationRule.Custom || rule == EUINavigationRule.CustomBoundary) return;
        heldRules = rules;
        held = widget;
        widget.SetNavigationRuleBase(EUINavigation.Up, EUINavigationRule.Stop);
        widget.SetNavigationRuleBase(EUINavigation.Down, EUINavigationRule.Stop);
        widget.SetNavigationRuleBase(EUINavigation.Left, EUINavigationRule.Stop);
        widget.SetNavigationRuleBase(EUINavigation.Right, EUINavigationRule.Stop);
    }

    /// <summary>The held widget's name and class, for the log.</summary>
    public string Describe() => held != null ? $"{UKismetSystemLibrary.GetObjectName(held)} ({GameUI.ClassPath(held)})" : "nothing";
}
