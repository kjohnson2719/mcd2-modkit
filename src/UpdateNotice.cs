using System.Collections.Generic;
using NeoRune;
using UE.CoreUObject;
using UE.Engine;
using UE.GameplayMessageRuntime;
using UE.GameplayTags;
using UE.SlateCore;
using UE.UMG;

namespace ModKit;

/// <summary>
/// The update notice: made of the game's own panel (its item panel's ribbon over its tooltip frame, in its font, see
/// <see cref="GamePanel"/>), small, in the game's toast area, for a few seconds. One notice lists every mod that has an
/// update (see <see cref="UpdateCheck"/>, which gathers them).
/// </summary>
public class UpdateNotice : AActor
{
    const double Seconds = 9;
    const double FadeSeconds = 0.35;

    GameSlot? slot;
    List<string> titles;
    List<string> versions;
    List<string> installed;
    bool filled;
    bool shown;
    double shownAt;
    double giveUp;
    bool waitingForLevel;
    double waitUntil;
    UAsyncAction_ListenForGameplayMessage? transition;

    /// <summary>Shows a notice for some mods: their titles, their new versions, and the versions installed.</summary>
    public static UpdateNotice? Show(UObject context, List<string> titles, List<string> versions, List<string> installed)
    {
        var notice = World.Spawn(context, Unreal.ClassOf<UpdateNotice>(), new FVector()) as UpdateNotice;
        notice?.Begin(titles, versions, installed);
        return notice;
    }

    void Begin(List<string> modTitles, List<string> modVersions, List<string> modInstalled)
    {
        titles = modTitles;
        versions = modVersions;
        installed = modInstalled;
        SetTickableWhenPaused(true);
        slot = GameUI.AddToSlot(this, GameSlots.Toasts, Unreal.ClassOf<UpdateNoticeWidget>(), 0);
        // In a level's first seconds its loading screen may still cover the toast area: wait for the level to show.
        double now = UGameplayStatics.GetTimeSeconds(this);
        if (!World.LevelName(this).StartsWith("Menu") && now < 15)
        {
            waitingForLevel = true;
            waitUntil = now + 15;
            transition = GameEvents.Listen(this, GameChannels.WorldTransitionEnd, null, false);
            if (transition != null) transition.OnMessageReceived += OnLevelShown;
        }
        giveUp = World.RealTime(this) + 40;
        Timer.Start(this, nameof(Step), 0.03f, true);
    }

    void OnLevelShown(UAsyncAction_ListenForGameplayMessage? message, FGameplayTag channel)
    {
        waitingForLevel = false;
        GameEvents.Stop(transition);
    }

    void Step()
    {
        double now = World.RealTime(this);
        if (waitingForLevel && UGameplayStatics.GetTimeSeconds(this) >= waitUntil) waitingForLevel = false;
        var widget = slot?.Widget as UpdateNoticeWidget;
        if (widget != null)
        {
            // The slot makes the widget (again if the game rebuilds its screen): fill it in each time it's new.
            if (!filled || !widget.HasContent())
            {
                filled = true;
                widget.Fill(titles, versions, installed);
            }
            if (!shown && !waitingForLevel)
            {
                shown = true;
                shownAt = now;
                Sounds.Play(this, Sounds.ToastTag);
            }
            double t = shown ? now - shownAt : 0;
            double opacity = !shown ? 0 : t < FadeSeconds ? t / FadeSeconds : t > Seconds - FadeSeconds ? (Seconds - t) / FadeSeconds : 1;
            widget.SetRenderOpacity((float)(opacity < 0 ? 0 : opacity));
            widget.SetRenderTranslation(new FVector2D { X = (1 - (opacity > 1 ? 1 : opacity)) * 40, Y = 0 });
        }
        if (shown ? now - shownAt >= Seconds : now >= giveUp)
        {
            Timer.Stop(this, nameof(Step));
            slot?.Remove();
            K2_DestroyActor();
        }
    }
}

/// <summary>The update notice's look: the game's ribbon ("Update available") over its tooltip frame, small.</summary>
public class UpdateNoticeWidget : ModWidget
{
    const float Width = 1050;   // in the game's 4K units
    const float Scale = 0.42f;

    UVerticalBox? lines;
    UTextBlock? footer;
    UTextBlock? titleText;
    UUserWidget? ribbon;
    UObject? font;
    bool hasContent;

    protected override UWidget? Build(UWidgetTree tree)
    {
        var scaler = UGameplayStatics.SpawnObject(Unreal.ClassOf<UScaleBox>(), tree) as UScaleBox;
        var sizer = UGameplayStatics.SpawnObject(Unreal.ClassOf<USizeBox>(), tree) as USizeBox;
        lines = Ui.Column(tree);
        footer = Ui.Text(tree, "", 34);
        var lower = Ui.Column(tree);
        if (scaler == null || sizer == null || lines == null || footer == null || lower == null) return null;
        scaler.SetStretch(UE.Slate.EStretch.UserSpecified);
        scaler.SetUserSpecifiedScale(Scale);
        sizer.SetWidthOverride(Width);
        footer.SetAutoWrapText(true);
        lower.AddChild(footer);
        var panel = GamePanel.Build(this, tree, "Update available", lines, lower, null, out titleText, out ribbon);
        font = GameLook.FontOf(titleText);
        if (panel != null) sizer.AddChild(panel);
        else
        {
            // Without the game's panel: a plain one.
            var plain = Ui.Column(tree);
            plain?.AddChild(lines);
            plain?.AddChild(lower);
            sizer.AddChild(Ui.Panel(tree, plain, Ui.Color(0.03f, 0.05f, 0.07f, 0.95f), 60));
        }
        scaler.AddChild(sizer);
        // Off the screen's edge, like the game's toasts; it doesn't take clicks.
        var holder = Ui.Panel(tree, scaler, Ui.Color(0, 0, 0, 0), 12);
        SetVisibility(ESlateVisibility.HitTestInvisible);
        SetRenderOpacity(0);
        return holder;
    }

    public bool HasContent() => hasContent;

    /// <summary>One line per mod: its name and new version, and the version installed.</summary>
    public void Fill(List<string> titles, List<string> versions, List<string> installed)
    {
        if (lines == null) return;
        hasContent = true;
        lines.ClearChildren();
        GamePanel.KeepTitle(ribbon, titleText, titles.Count > 1 ? "Updates available" : "Update available");
        for (int i = 0; i < titles.Count; i++)
        {
            var row = Ui.Row(WidgetTree);
            var name = Ui.Text(WidgetTree, $"{titles[i]} {versions[i]}", 45);
            var have = Ui.Text(WidgetTree, $"you have {installed[i]}", 34);
            if (row == null || name == null || have == null) continue;
            GameLook.Style(name, font, GameLook.TitleFace, 45, Ui.Color(1, 1, 1, 1));
            GameLook.Style(have, font, GameLook.TextFace, 34, GameLook.TextGrey());
            var nameSlot = row.AddChild(name) as UHorizontalBoxSlot;
            nameSlot?.SetVerticalAlignment(EVerticalAlignment.VAlign_Center);
            var haveSlot = row.AddChild(have) as UHorizontalBoxSlot;
            haveSlot?.SetVerticalAlignment(EVerticalAlignment.VAlign_Center);
            haveSlot?.SetHorizontalAlignment(EHorizontalAlignment.HAlign_Right);
            haveSlot?.SetSize(new FSlateChildSize { Value = 1, SizeRule = ESlateSizeRule.Fill });
            haveSlot?.SetPadding(new FMargin { Left = 30 });
            var slot = lines.AddChild(row) as UVerticalBoxSlot;
            slot?.SetPadding(new FMargin { Bottom = 10 });
        }
        footer?.SetText(titles.Count > 1 ? "Get them on Nexus Mods or GitHub." : "Get it on Nexus Mods or GitHub.");
        GameLook.Style(footer, font, GameLook.TextFace, 34, GameLook.TextGrey());
    }
}
