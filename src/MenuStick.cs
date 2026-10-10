using NeoRune;
using UE.CommonGame;
using UE.CoreUObject;
using UE.Engine;
using UE.SpicewoodUI;
using UE.UIStateContainer;

namespace ModKit;

/// <summary>
/// The left stick while one of the game's menus is open, as steps (up, down, left, right) like the game's lists take:
/// one when the stick is pushed, then more while it's held. It's the game's own stick reader (the world map pans with it):
/// found in game, the stick's directions don't come as keys, so <see cref="MenuKeys"/> can't hear them.
/// <code>
/// stick = MenuStick.Create(this);
/// if (stick != null) stick.Stepped += OnStep;   // stick.Step: MenuStick.Up, Down, Left or Right
/// ...
/// stick?.SetOn(menuOpen);   // every frame
/// </code>
/// </summary>
public class MenuStick : UObject
{
    public const int None = 0;
    public const int Up = 1;
    public const int Down = 2;
    public const int Left = 3;
    public const int Right = 4;

    const double Press = 0.5;   // how far the stick goes before it's a step
    const double Release = 0.3;   // and how far back before it's let go
    const double FirstRepeat = 0.4;   // seconds held before it steps again
    const double Repeat = 0.15;
    const double Gap = 0.25;   // seconds without a report: the stick was let go (it may not report while centred)
    const double SettleSeconds = 0.15;   // a push already held when it comes on isn't a step

    public event TimerEvent Stepped;
    /// <summary>The direction of the last step.</summary>
    public int Step;
    public bool IsOn;
    /// <summary>The stick's last reading, for the log: x right, y down (found in game: the reader's y is the screen's).</summary>
    public FVector2D Last;

    UStickMouseInputWrapper? reader;
    int held;
    double nextRepeat;
    double lastReport;
    double onSince;

    /// <summary>A stick reader for the local player, off; null without a player yet.</summary>
    public static MenuStick? Create(UObject owner)
    {
        var localPlayer = UCommonUIExtensions.GetLocalPlayerFromController(World.PlayerController(owner));
        if (localPlayer == null) return null;
        var stick = UGameplayStatics.SpawnObject(Unreal.ClassOf<MenuStick>(), owner) as MenuStick;
        return stick != null && stick.Init(localPlayer) ? stick : null;
    }

    bool Init(ULocalPlayer localPlayer)
    {
        reader = UStickMouseInputWrapper.Create();
        // The reader wants the game's id for the player; the functions that make one aren't static (an instance does).
        var ids = UGameplayStatics.SpawnObject(Unreal.ClassOf<UUIPlayerId>(), this) as UUIPlayerId;
        if (reader == null || ids == null) return false;
        reader.SetOwningPlayer(ids.FromLocalPlayer(localPlayer));
        reader.OnLeftStickInput = OnStick;
        reader.ToggleEnabled(false);
        lastReport = -100;
        return true;
    }

    /// <summary>Turns the reader on or off (cheap to call every frame).</summary>
    public void SetOn(bool on)
    {
        if (reader == null || on == IsOn) return;
        IsOn = on;
        reader.ToggleEnabled(on);
        if (!on) return;
        onSince = World.RealTime(this);
        held = None;
    }

    void OnStick(float DeltaTime, FVector2D InputAxes)
    {
        if (!IsOn) return;
        Last = InputAxes;
        double now = World.RealTime(this);
        if (now - lastReport > Gap) held = None;
        lastReport = now;
        int direction = Direction(InputAxes);
        if (direction == None)
        {
            held = None;
            return;
        }
        if (direction != held)
        {
            held = direction;
            nextRepeat = now + FirstRepeat;
            // Pushed before it came on (e.g. while opening what turned it on): held, not a step.
            if (now - onSince < SettleSeconds) return;
            Fire(direction);
            return;
        }
        if (now < nextRepeat) return;
        nextRepeat = now + Repeat;
        Fire(direction);
    }

    /// <summary>The way the stick's pushed most (none near the middle; once pushed, it has to come back further to let go).</summary>
    int Direction(FVector2D axes)
    {
        double x = Abs(axes.X);
        double y = Abs(axes.Y);
        double threshold = held != None ? Release : Press;
        if (x < threshold && y < threshold) return None;
        if (y >= x) return axes.Y < 0 ? Up : Down;
        return axes.X > 0 ? Right : Left;
    }

    void Fire(int direction)
    {
        Step = direction;
        Stepped();
    }

    static double Abs(double value) => value < 0 ? -value : value;
}
