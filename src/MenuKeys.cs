using System.Collections.Generic;
using NeoRune;
using UE.CommonGame;
using UE.CoreUObject;
using UE.Engine;
using UE.InputCore;
using UE.SpicewoodUI;

namespace ModKit;

/// <summary>
/// Keys while one of the game's menus is open (the inventory, the map...), which the usual key listeners don't hear. It's
/// the game's own key tracker, and it keeps the keys it tracks from the menus, so turn it on only while it's wanted (found
/// in game: always on, the inventory couldn't sort with the left stick click). Pressed fires for each new press of a
/// tracked key, with the key in <see cref="LastKey"/>:
/// <code>
/// keys = MenuKeys.Create(this);
/// if (keys != null) { keys.Track(openKey); keys.Pressed += OnMenuKey; }
/// ...
/// keys?.SetOn(inventoryOpen);   // every frame
/// </code>
/// </summary>
public class MenuKeys : UObject
{
    const double KeyGap = 0.2;   // seconds without the key before it counts as pressed again (the tracker repeats held keys)

    public event TimerEvent Pressed;
    /// <summary>The key of the last press.</summary>
    public FKey LastKey;
    public bool IsOn;

    UKeyboardDirectionTickWrapper? tracker;
    List<FKey> keys;
    List<FKey> pressKeys;
    List<double> pressTimes;

    /// <summary>A key tracker for the local player, off; null without a player yet.</summary>
    public static MenuKeys? Create(UObject owner)
    {
        var localPlayer = UCommonUIExtensions.GetLocalPlayerFromController(World.PlayerController(owner));
        if (localPlayer == null) return null;
        var menuKeys = UGameplayStatics.SpawnObject(Unreal.ClassOf<MenuKeys>(), owner) as MenuKeys;
        return menuKeys != null && menuKeys.Init(localPlayer) ? menuKeys : null;
    }

    bool Init(ULocalPlayer localPlayer)
    {
        keys = new List<FKey>();
        pressKeys = new List<FKey>();
        pressTimes = new List<double>();
        tracker = UKeyboardDirectionTickWrapper.Create();
        if (tracker == null) return false;
        tracker.SetOwningPlayer(localPlayer);
        tracker.OnInputPerformedThisTick = OnTracked;
        tracker.SetIsEnabled(false);
        return true;
    }

    /// <summary>Tracks a key too (an unbound key, "None", is skipped).</summary>
    public void Track(FKey key)
    {
        if (tracker == null || key.KeyName == "None") return;
        foreach (var tracked in keys)
            if (UKismetInputLibrary.EqualEqual_KeyKey(tracked, key)) return;
        tracker.TrackKey(key);
        keys.Add(key);
    }

    /// <summary>Stops tracking all keys (to track others, e.g. after the player changed them).</summary>
    public void Clear()
    {
        if (tracker == null) return;
        foreach (var key in keys) tracker.UntrackKey(key);
        keys = new List<FKey>();
    }

    /// <summary>Turns the tracker on or off (cheap to call every frame).</summary>
    public void SetOn(bool on)
    {
        if (tracker == null || on == IsOn) return;
        IsOn = on;
        tracker.SetIsEnabled(on);
    }

    /// <summary>Whether a key is a bound key ("None" isn't) and this one.</summary>
    public static bool Is(FKey key, FKey bound) => bound.KeyName != "None" && UKismetInputLibrary.EqualEqual_KeyKey(key, bound);

    void OnTracked(float DeltaTime, FKey Key, EInputEvent InputEvent)
    {
        if (InputEvent == EInputEvent.IE_Released || !IsOn) return;
        double now = World.RealTime(this);
        if (!NewPress(Key, now)) return;
        LastKey = Key;
        Pressed();
    }

    bool NewPress(FKey key, double now)
    {
        for (int i = 0; i < pressKeys.Count; i++)
        {
            if (!UKismetInputLibrary.EqualEqual_KeyKey(pressKeys[i], key)) continue;
            bool pressed = now - pressTimes[i] > KeyGap;
            pressTimes[i] = now;
            return pressed;
        }
        pressKeys.Add(key);
        pressTimes.Add(now);
        return true;
    }
}
