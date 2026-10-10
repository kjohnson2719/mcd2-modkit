using NeoRune;
using UE.CommonInput;
using UE.CoreUObject;
using UE.Engine;
using UE.SlateCore;
using UE.UMG;

namespace ModKit;

/// <summary>
/// Facts about the game's screens: whether a widget really shows, which controller is in use, and the scale a screen is
/// drawn at. Things found in game while making mods for it.
/// </summary>
public static class Screens
{
    /// <summary>
    /// Whether a widget shows, up to <paramref name="top"/> (null: all the way up): "yes", or the first widget that hides
    /// it. A widget shows when it and everything it's in is visible, through the user widgets it's part of, and it's the
    /// page each switcher it's in shows: switchers hide their other pages without changing their visibility.
    /// (Found in game: the inventory's screen flips between activated and not every frame while it shows, so a screen's
    /// being activated doesn't tell; this does.)
    /// </summary>
    public static string ShownTrace(UWidget widget, UWidget? top)
    {
        UObject? at = widget;
        for (int i = 0; i < 80 && at != null; i++)
        {
            if (top != null && at == top) return "yes";
            var current = at as UWidget;
            if (current != null)
            {
                if (!current.IsVisible()) return $"no: {UKismetSystemLibrary.GetObjectName(current)} ({GameUI.ClassPath(current)}) is hidden";
                var parent = current.GetParent();
                if (parent != null)
                {
                    var switcher = parent as UWidgetSwitcher;
                    if (switcher != null && switcher.GetActiveWidget() != current)
                        return $"no: switcher {UKismetSystemLibrary.GetObjectName(switcher)} shows {UKismetSystemLibrary.GetObjectName(switcher.GetActiveWidget())}, not {UKismetSystemLibrary.GetObjectName(current)}";
                    at = parent;
                    continue;
                }
            }
            // The root of a user widget's tree: on to the user widget (the tree's outer).
            at = UKismetSystemLibrary.GetOuterObject(at);
        }
        return "yes";
    }

    /// <summary>Whether a widget shows, up to <paramref name="top"/> (see <see cref="ShownTrace"/>).</summary>
    public static bool Shown(UWidget widget, UWidget? top) => ShownTrace(widget, top) == "yes";

    /// <summary>Whether the player is on a controller now (the game's hints switch with it).</summary>
    public static bool Gamepad(UObject context)
    {
        var controller = World.PlayerController(context);
        var input = controller == null ? null
            : USubsystemBlueprintLibrary.GetLocalPlayerSubSystemFromPlayerController(controller, Unreal.ClassOf<UCommonInputSubsystem>()) as UCommonInputSubsystem;
        return input != null && input.GetCurrentInputType() == ECommonInputType.Gamepad;
    }

    /// <summary>
    /// The scale a widget is drawn at: absolute pixels per unit of its own layout. The game lays its screens out at 4K
    /// sizes and scales them to fit, so its sizes (fonts, paddings) times this are the pixels; 0 before it's laid out.
    /// </summary>
    public static double Scale(UWidget widget)
    {
        var geometry = widget.GetCachedGeometry();
        var size = USlateBlueprintLibrary.GetLocalSize(geometry);
        if (!(size.X > 1)) return 0;
        var a = USlateBlueprintLibrary.LocalToAbsolute(geometry, new FVector2D { X = 0, Y = 0 });
        var b = USlateBlueprintLibrary.LocalToAbsolute(geometry, new FVector2D { X = size.X, Y = 0 });
        return (b.X - a.X) / size.X;
    }

    /// <summary>A widget's rectangle on screen, in absolute coordinates; false before it's laid out.</summary>
    public static bool Rect(UWidget widget, out FVector2D topLeft, out FVector2D bottomRight)
    {
        var geometry = widget.GetCachedGeometry();
        var size = USlateBlueprintLibrary.GetLocalSize(geometry);
        topLeft = USlateBlueprintLibrary.LocalToAbsolute(geometry, new FVector2D { X = 0, Y = 0 });
        bottomRight = USlateBlueprintLibrary.LocalToAbsolute(geometry, size);
        return size.X > 1 && size.Y > 1 && bottomRight.X - topLeft.X > 1;
    }
}
