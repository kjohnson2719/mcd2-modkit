using System.Collections.Generic;
using NeoRune;
using UE.CoreUObject;
using UE.Engine;

namespace ModKit;

/// <summary>What the update check remembers between sessions (one per mod).</summary>
public struct UpdateState
{
    // The newest version GitHub had, and when it was asked (Unix seconds).
    public string Latest;
    public long CheckedAt;
    // The version the player was last told about, and when.
    public string Told;
    public long ToldAt;
}

/// <summary>
/// Tells the player when a newer version of the mod is out. It asks GitHub for the latest release of the mod's repository
/// (<see cref="ModInfo.Repo"/>, at most every few hours), and when it's newer than the mod's own version, shows the game's
/// toast: once per new version, and again a day later if it's still not updated. Start it when a level begins:
/// <code>
/// if (CheckUpdates) UpdateCheck.Start(this);
/// </code>
/// Mods that use it should say so on their page, and have a setting to turn it off.
/// </summary>
public class UpdateCheck : AActor
{
    const long CheckSeconds = 6 * 3600;
    const long RemindSeconds = 24 * 3600;
    const string SaveName = "updates";

    Http? http;
    int request = -1;
    UpdateState state;
    string newer = "";
    double waitedSince;

    /// <summary>Starts the check (nothing when the mod has no repository set in its .csproj).</summary>
    public static UpdateCheck? Start(UObject context)
    {
        if (UKismetStringLibrary.Len(ModInfo.Repo) == 0) return null;
        var check = World.Spawn(context, Unreal.ClassOf<UpdateCheck>(), new FVector()) as UpdateCheck;
        check?.Begin();
        return check;
    }

    void Begin()
    {
        if (!ModSave.Load(SaveName, out UpdateState saved)) saved = new UpdateState { Latest = "", Told = "" };
        state = saved;
        long now = Now();
        // Asked lately: what GitHub said then will do.
        if (UKismetStringLibrary.Len(state.Latest) > 0 && now - state.CheckedAt < CheckSeconds)
        {
            Decide(state.Latest);
            return;
        }
        http = Http.Create(this);
        if (http == null) return;
        http.Responded += OnResponse;
        var headers = new Dictionary<string, string>();
        // GitHub's API wants to know who's asking.
        headers["User-Agent"] = ModInfo.Title + "/" + ModInfo.Version;
        headers["Accept"] = "application/vnd.github+json";
        request = http.Send($"https://api.github.com/repos/{ModInfo.Repo}/releases/latest", "GET", "", headers);
    }

    void OnResponse(HttpResponse response)
    {
        if (response.Request != request) return;
        if (response.Status != 200) return;
        string tag = Tag(response.Body);
        if (UKismetStringLibrary.Len(tag) == 0) return;
        state.Latest = tag;
        state.CheckedAt = Now();
        ModSave.Save(SaveName, state);
        Decide(tag);
    }

    /// <summary>The release's tag ("v1.1") from GitHub's answer, or nothing.</summary>
    static string Tag(string body)
    {
        int key = UKismetStringLibrary.FindSubstring(body, "\"tag_name\"", true, false, 0);
        if (key < 0) return "";
        int open = UKismetStringLibrary.FindSubstring(body, "\"", true, false, key + 10);
        if (open < 0) return "";
        int close = UKismetStringLibrary.FindSubstring(body, "\"", true, false, open + 1);
        if (close <= open) return "";
        return UKismetStringLibrary.GetSubstring(body, open + 1, close - open - 1);
    }

    void Decide(string latest)
    {
        if (!IsNewer(latest, ModInfo.Version)) return;
        bool toldThis = state.Told == latest && Now() - state.ToldAt < RemindSeconds;
        if (toldThis) return;
        newer = latest;
        // Toasts show on the main menu and in levels; give the level a moment to settle first.
        waitedSince = World.RealTime(this);
        Timer.Start(this, nameof(TryTell), 1f, true);
    }

    void TryTell()
    {
        if (World.RealTime(this) - waitedSince < 6) return;
        Timer.Stop(this, nameof(TryTell));
        GameUI.Toast(this, $"{ModInfo.Title} {Bare(newer)} is out (you have {ModInfo.Version}). Get it on Nexus or GitHub.", 8f);
        state.Told = newer;
        state.ToldAt = Now();
        ModSave.Save(SaveName, state);
    }

    /// <summary>Whether a version ("v1.10", "1.2.3") is newer than another, part by part.</summary>
    public static bool IsNewer(string version, string than)
    {
        var a = UKismetStringLibrary.ParseIntoArray(Bare(version), ".", true);
        var b = UKismetStringLibrary.ParseIntoArray(Bare(than), ".", true);
        int parts = a.Count > b.Count ? a.Count : b.Count;
        for (int i = 0; i < parts; i++)
        {
            int x = i < a.Count ? UKismetStringLibrary.Conv_StringToInt(a[i]) : 0;
            int y = i < b.Count ? UKismetStringLibrary.Conv_StringToInt(b[i]) : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    /// <summary>A version without its tag's "v".</summary>
    static string Bare(string version) =>
        UKismetStringLibrary.StartsWith(version, "v", ESearchCase.IgnoreCase) ? UKismetStringLibrary.RightChop(version, 1) : version;

    static long Now() => UKismetMathLibrary.ToUnixTimestamp(UKismetMathLibrary.UtcNow());
}
