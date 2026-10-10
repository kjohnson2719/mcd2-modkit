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
/// tracked key, with the key in <see cref="LastKey"/> (a key already down when it comes on isn't a press, so the key that
/// opened a screen doesn't also act on it). With <see cref="Repeats"/>, a held key presses again and again, like typing.
/// <code>
/// keys = MenuKeys.Create(this);
/// if (keys != null) { keys.Track(openKey); keys.Pressed += OnMenuKey; }
/// ...
/// keys?.SetOn(inventoryOpen);   // every frame
/// </code>
/// </summary>
public class MenuKeys : UObject
{
    // The tracker reports the keys held, again and again (it's made for panning with held keys), and may or may not say
    // when one is let go or that a report is a repeat: it learns which as it goes. Without that, a key not reported for a
    // few of its frames was let go (found in game: a fixed 0.2 seconds lost quick presses of the same key, like Backspace).
    const double MinGap = 0.06;
    const double MaxGap = 0.2;
    const double StaleSeconds = 1;   // a key not reported for this long was let go, whatever the tracker says
    const double SettleSeconds = 0.15;   // keys already down when the tracker comes on are held, not pressed
    const double RepeatDelay = 0.45;   // with Repeats: held this long, a key presses again every RepeatRate seconds
    const double RepeatRate = 0.05;
    const double Never = 1e9;

    public event TimerEvent Pressed;
    /// <summary>The key of the last press.</summary>
    public FKey LastKey;
    public bool IsOn;
    /// <summary>Whether a held key presses again and again, like typing (otherwise holding it is one press).</summary>
    public bool Repeats;
    /// <summary>How many more of the tracker's reports to log, to see how it reports keys (development).</summary>
    public int LogReports;

    UKeyboardDirectionTickWrapper? tracker;
    List<FKey> keys;
    // The keys down now: when each was last reported, when it went down (Never: down before the tracker came on), and when it last pressed.
    List<FKey> downKeys;
    List<double> lastSeen;
    List<double> downSince;
    List<double> lastFired;
    double onSince;
    double frameGap;
    bool reportsReleases;
    bool reportsRepeats;

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
        ForgetDown();
        frameGap = MaxGap;
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
        // What was down is reported again while it's held: from the start.
        ForgetDown();
        if (on) onSince = World.RealTime(this);
    }

    /// <summary>Whether a tracked key is down now.</summary>
    public bool IsDown(FKey key)
    {
        int at = IndexOf(key);
        return at >= 0 && Down(at, World.RealTime(this));
    }

    /// <summary>Whether any tracked key is down (to keep the tracker on until the key that closed a screen is let go).</summary>
    public bool AnyDown()
    {
        double now = World.RealTime(this);
        for (int i = 0; i < downKeys.Count; i++)
            if (Down(i, now)) return true;
        return false;
    }

    /// <summary>Whether a key is a bound key ("None" isn't) and this one.</summary>
    public static bool Is(FKey key, FKey bound) => bound.KeyName != "None" && UKismetInputLibrary.EqualEqual_KeyKey(key, bound);

    /// <summary>
    /// A key's report: a new press is one that wasn't down (a press after a let-go, or one the tracker says is new), a
    /// repeat or a report of a key held is not (but repeats when <see cref="Repeats"/>), and a key down before the tracker
    /// came on is only held.
    /// </summary>
    void OnTracked(float DeltaTime, FKey Key, EInputEvent InputEvent)
    {
        double now = World.RealTime(this);
        if (LogReports > 0)
        {
            LogReports--;
            Log.Write($"keys: {Key.KeyName} {EventName(InputEvent)}, frame {DeltaTime:0.000}, at {now:0.000}");
        }
        if (DeltaTime > 0)
        {
            double gap = DeltaTime * 3;
            frameGap = gap < MinGap ? MinGap : gap > MaxGap ? MaxGap : gap;
        }
        int at = IndexOf(Key);
        if (InputEvent == EInputEvent.IE_Released)
        {
            reportsReleases = true;
            if (at >= 0) Forget(at);
            return;
        }
        if (InputEvent != EInputEvent.IE_Pressed && InputEvent != EInputEvent.IE_Repeat) return;
        if (InputEvent == EInputEvent.IE_Repeat) reportsRepeats = true;
        bool fresh;
        if (at < 0) fresh = InputEvent == EInputEvent.IE_Pressed;
        else if (InputEvent == EInputEvent.IE_Repeat) fresh = false;
        else if (reportsRepeats) fresh = true;
        else fresh = !Down(at, now);
        if (!fresh && at >= 0)
        {
            lastSeen[at] = now;
            if (!Repeats || !IsOn || now - downSince[at] < RepeatDelay || now - lastFired[at] < RepeatRate) return;
            lastFired[at] = now;
            Fire(Key);
            return;
        }
        if (at >= 0) Forget(at);
        // Down before the tracker came on (or first heard as a repeat): held, and it doesn't repeat.
        bool before = !fresh || !IsOn || now - onSince < SettleSeconds;
        downKeys.Add(Key);
        lastSeen.Add(now);
        downSince.Add(before ? Never : now);
        lastFired.Add(now);
        if (!before) Fire(Key);
    }

    void Fire(FKey key)
    {
        LastKey = key;
        Pressed();
    }

    /// <summary>Whether a key in the list is still down: the tracker said nothing of its being let go, and it's been reported lately.</summary>
    bool Down(int at, double now) => now - lastSeen[at] <= (reportsReleases ? StaleSeconds : frameGap);

    int IndexOf(FKey key)
    {
        for (int i = 0; i < downKeys.Count; i++)
            if (UKismetInputLibrary.EqualEqual_KeyKey(downKeys[i], key)) return i;
        return -1;
    }

    void Forget(int at)
    {
        downKeys.RemoveAt(at);
        lastSeen.RemoveAt(at);
        downSince.RemoveAt(at);
        lastFired.RemoveAt(at);
    }

    void ForgetDown()
    {
        downKeys = new List<FKey>();
        lastSeen = new List<double>();
        downSince = new List<double>();
        lastFired = new List<double>();
    }

    static string EventName(EInputEvent inputEvent)
    {
        if (inputEvent == EInputEvent.IE_Pressed) return "pressed";
        if (inputEvent == EInputEvent.IE_Released) return "released";
        if (inputEvent == EInputEvent.IE_Repeat) return "repeat";
        if (inputEvent == EInputEvent.IE_DoubleClick) return "double click";
        return "axis";
    }
}
