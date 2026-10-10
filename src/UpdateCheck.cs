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
/// (<see cref="ModInfo.Repo"/>, at most every few hours), and when it's newer than the mod's own version, shows the
/// <see cref="UpdateNotice"/>: once per new version, and again a day later if it's still not updated. Start it when a level
/// begins:
/// <code>
/// if (CheckUpdates) UpdateCheck.Start(this);
/// </code>
/// Several mods with the kit share one notice. Each check is an actor in the level, tagged with what it found; once all
/// of them have their answer, the first one with an update shows one notice listing every mod that has one, and tags the
/// others as told. (Tags, because the mods can't call each other: each has its own copy of the kit.)
/// Mods that use it should say so on their page, and have a setting to turn it off.
/// </summary>
public class UpdateCheck : AActor
{
    const long CheckSeconds = 6 * 3600;
    const long RemindSeconds = 24 * 3600;
    const string SaveName = "updates";
    // The tags the checks of all the mods read on each other. Keep them as they are: other mods' copies of the kit use them.
    const string CheckTag = "ModKit.UpdateCheck";
    const string DoneTag = "ModKit.UpdateCheck.Done";
    const string PendingTag = "ModKit.UpdateCheck.Pending=";   // + title|new version|installed version
    const string ToldTag = "ModKit.UpdateCheck.Told";
    const double WaitForOthers = 15;   // seconds: the longest to wait for the other mods' answers
    const double WaitForLeader = 25;   // seconds: a check that's never told shows its own notice after this

    Http? http;
    int request = -1;
    UpdateState state;
    string newer = "";
    bool decided;
    bool pending;
    double startedAt;

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
        Tags.Add(CheckTag);
        startedAt = World.RealTime(this);
        Timer.Start(this, nameof(Gather), 0.5f, true);
        if (!ModSave.Load(SaveName, out UpdateState saved)) saved = new UpdateState { Latest = "", Told = "" };
        state = saved;
        // Asked lately: what GitHub said then will do.
        if (UKismetStringLibrary.Len(state.Latest) > 0 && Now() - state.CheckedAt < CheckSeconds)
        {
            Decide(state.Latest);
            return;
        }
        http = Http.Create(this);
        if (http == null)
        {
            Decide("");
            return;
        }
        http.Responded += OnResponse;
        var headers = new Dictionary<string, string>();
        // GitHub's API wants to know who's asking.
        headers["User-Agent"] = ModInfo.Title + "/" + ModInfo.Version;
        headers["Accept"] = "application/vnd.github+json";
        request = http.Send($"https://api.github.com/repos/{ModInfo.Repo}/releases/latest", "GET", "", headers);
        if (request < 0) Decide("");
    }

    void OnResponse(HttpResponse response)
    {
        if (response.Request != request) return;
        string tag = response.Status == 200 ? Tag(response.Body) : "";
        if (UKismetStringLibrary.Len(tag) > 0)
        {
            state.Latest = tag;
            state.CheckedAt = Now();
            ModSave.Save(SaveName, state);
        }
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

    /// <summary>The answer is in (nothing: no answer): tags this check done, and pending when there's news to tell.</summary>
    void Decide(string latest)
    {
        if (decided) return;
        decided = true;
        Tags.Add(DoneTag);
        if (UKismetStringLibrary.Len(latest) == 0 || !IsNewer(latest, ModInfo.Version)) return;
        bool toldThis = state.Told == latest && Now() - state.ToldAt < RemindSeconds;
        if (toldThis) return;
        newer = latest;
        pending = true;
        Tags.Add(PendingTag + ModInfo.Title + "|" + Bare(latest) + "|" + ModInfo.Version);
    }

    /// <summary>
    /// Twice a second: once every check has its answer, the first one with news shows the notice for all of them and tags
    /// the others told; the others wait to be told (or, if they never are, show their own).
    /// </summary>
    void Gather()
    {
        double waited = World.RealTime(this) - startedAt;
        if (!decided && waited < WaitForOthers) return;
        if (!pending)
        {
            Finish(false);
            return;
        }
        if (ActorHasTag(ToldTag))
        {
            Finish(true);
            return;
        }
        UGameplayStatics.GetAllActorsWithTag(this, CheckTag, out var checks);
        bool allDone = true;
        AActor? leader = null;
        foreach (var check in checks)
        {
            if (check == null) continue;
            if (!check.ActorHasTag(DoneTag)) allDone = false;
            if (leader == null && PendingOf(check) != "") leader = check;
        }
        if (!allDone && waited < WaitForOthers) return;
        if (leader != this)
        {
            // Another mod's check shows the notice and tags this one told; if it never does, this one shows its own.
            if (waited < WaitForLeader) return;
            var title = new List<string> { ModInfo.Title };
            var version = new List<string> { Bare(newer) };
            var have = new List<string> { ModInfo.Version };
            UpdateNotice.Show(this, title, version, have);
            Finish(true);
            return;
        }
        // This one leads: every mod with news, in one notice.
        var titles = new List<string>();
        var versions = new List<string>();
        var installed = new List<string>();
        foreach (var check in checks)
        {
            string news = check != null ? PendingOf(check) : "";
            if (news == "") continue;
            var parts = UKismetStringLibrary.ParseIntoArray(news, "|", false);
            if (parts.Count < 3) continue;
            titles.Add(parts[0]);
            versions.Add(parts[1]);
            installed.Add(parts[2]);
            if (check != this && check != null && !check.ActorHasTag(ToldTag)) check.Tags.Add(ToldTag);
        }
        UpdateNotice.Show(this, titles, versions, installed);
        Finish(true);
    }

    /// <summary>A check's news ("title|new|installed"), from its pending tag; nothing when it has none.</summary>
    static string PendingOf(AActor check)
    {
        foreach (var tag in check.Tags)
        {
            string name = $"{tag}";
            if (UKismetStringLibrary.StartsWith(name, PendingTag, ESearchCase.CaseSensitive))
                return UKismetStringLibrary.RightChop(name, UKismetStringLibrary.Len(PendingTag));
        }
        return "";
    }

    void Finish(bool told)
    {
        Timer.Stop(this, nameof(Gather));
        if (told)
        {
            state.Told = newer;
            state.ToldAt = Now();
            ModSave.Save(SaveName, state);
        }
        K2_DestroyActor();
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
