using NeoRune;
using UE.CommonUI;
using UE.CoreUObject;
using UE.Engine;
using UE.SlateCore;
using UE.UMG;

namespace ModKit;

/// <summary>
/// A button of yours that looks like one of the game's (e.g. the inventory page's Effects &amp; Stats): that button's own
/// style (background, hover and press), its label's font, and the game's icon for your key. It's laid out in the game
/// screen's units at its scale, and shown on screen over a rectangle you give (e.g. beside the game's button); the label
/// gets smaller when the space is narrow.
/// <code>
/// button = Ui.Create(this, Unreal.ClassOf&lt;StyledButton&gt;()) as StyledButton;
/// button.SetLabel("Loadouts");
/// button.Clicked += OnClicked;
/// ...
/// button.Show(statsButton, topLeft, bottomRight, iconRow, hasIconRow);   // every frame while it's wanted
/// button.Hide();
/// </code>
/// </summary>
public class StyledButton : ModWidget
{
    const float IconSize = 80;
    const float LabelPadding = 20;

    /// <summary>Clicked (with the mouse: the controller uses your key).</summary>
    public event TimerEvent Clicked;

    UScaleBox? scaler;
    USizeBox? sizer;
    UButton? button;
    UTextBlock? label;
    UCommonActionWidget? icon;
    USizeBox? iconBox;
    string shownRow;
    bool styled;
    double fittedFor;

    protected override UWidget? Build(UWidgetTree tree)
    {
        shownRow = "";
        fittedFor = -1;
        scaler = UGameplayStatics.SpawnObject(Unreal.ClassOf<UScaleBox>(), tree) as UScaleBox;
        sizer = UGameplayStatics.SpawnObject(Unreal.ClassOf<USizeBox>(), tree) as USizeBox;
        button = UGameplayStatics.SpawnObject(Unreal.ClassOf<UButton>(), tree) as UButton;
        var row = Ui.Row(tree);
        label = Ui.Text(tree, "", 44);
        icon = UGameplayStatics.SpawnObject(Unreal.ClassOf<UCommonActionWidget>(), tree) as UCommonActionWidget;
        iconBox = UGameplayStatics.SpawnObject(Unreal.ClassOf<USizeBox>(), tree) as USizeBox;
        if (scaler == null || sizer == null || button == null || row == null || label == null || icon == null || iconBox == null) return null;
        scaler.SetStretch(UE.Slate.EStretch.UserSpecified);
        var labelSlot = row.AddChild(label) as UHorizontalBoxSlot;
        labelSlot?.SetVerticalAlignment(EVerticalAlignment.VAlign_Center);
        labelSlot?.SetPadding(new FMargin { Left = LabelPadding, Right = LabelPadding });
        iconBox.SetWidthOverride(IconSize);
        iconBox.SetHeightOverride(IconSize);
        iconBox.AddChild(icon);
        var iconSlot = row.AddChild(iconBox) as UHorizontalBoxSlot;
        iconSlot?.SetVerticalAlignment(EVerticalAlignment.VAlign_Center);
        button.AddChild(row);
        var rowSlot = row.Slot as UButtonSlot;
        rowSlot?.SetHorizontalAlignment(EHorizontalAlignment.HAlign_Center);
        rowSlot?.SetVerticalAlignment(EVerticalAlignment.VAlign_Center);
        // The controller presses it with your key, not by moving onto it.
        button.IsFocusable = false;
        button.OnClicked += OnClick;
        button.OnHovered += OnHover;
        sizer.AddChild(button);
        scaler.AddChild(sizer);
        return scaler;
    }

    public void SetLabel(string text)
    {
        label?.SetText(text);
        fittedFor = -1;
    }

    /// <summary>
    /// Shows the button over a rectangle of the screen (absolute coordinates), looking like <paramref name="like"/> (one of
    /// the game's buttons, laid out at the scale of its screen), with the icon of an input action row (or none).
    /// </summary>
    public void Show(UCommonButtonBase like, FVector2D topLeft, FVector2D bottomRight, FDataTableRowHandle row, bool hasRow)
    {
        // Over the game's menus.
        if (!IsInViewport()) AddToViewport(10000);
        double scale = Screens.Scale(like);
        double viewportScale = UWidgetLayoutLibrary.GetViewportScale(this);
        if (!(scale > 0) || !(viewportScale > 0)) return;
        double width = (bottomRight.X - topLeft.X) / scale;
        double height = (bottomRight.Y - topLeft.Y) / scale;
        double toViewport = scale / viewportScale;
        Refresh(like, width, height, toViewport, row, hasRow);
        USlateBlueprintLibrary.AbsoluteToViewport(this, topLeft, out FVector2D pixel, out FVector2D at);
        SetDesiredSizeInViewport(new FVector2D { X = width * toViewport, Y = height * toViewport });
        SetPositionInViewport(at, false);
    }

    /// <summary>Takes the button off the screen.</summary>
    public void Hide()
    {
        if (IsInViewport()) RemoveFromParent();
    }

    void Refresh(UCommonButtonBase like, double width, double height, double scale, FDataTableRowHandle row, bool hasRow)
    {
        if (button == null || label == null || icon == null || iconBox == null || scaler == null || sizer == null) return;
        if (!styled)
        {
            styled = true;
            button.SetStyle(like.NormalStyle);
            var ore = like as UE.OreUI.UOreUIButtonBase;
            if (ore != null && ore.TextBlockWidget != null)
            {
                label.SetFont(ore.TextBlockWidget.Font);
                label.SetColorAndOpacity(ore.TextBlockWidget.ColorAndOpacity);
                label.SetShadowOffset(ore.TextBlockWidget.ShadowOffset);
                label.SetShadowColorAndOpacity(ore.TextBlockWidget.ShadowColorAndOpacity);
            }
        }
        scaler.SetUserSpecifiedScale((float)scale);
        sizer.SetWidthOverride((float)width);
        sizer.SetHeightOverride((float)height);
        string rowName = hasRow ? $"{row.RowName}" : "";
        if (hasRow && rowName != shownRow) icon.SetInputAction(row);
        shownRow = rowName;
        bool hasIcon = hasRow && icon.GetIcon().ResourceObject != null;
        iconBox.SetVisibility(hasIcon ? ESlateVisibility.HitTestInvisible : ESlateVisibility.Collapsed);
        Fit(width, hasIcon);
    }

    /// <summary>The label as large as the game button's while it fits; smaller when it doesn't.</summary>
    void Fit(double width, bool hasIcon)
    {
        if (label == null) return;
        double key = width + (hasIcon ? 0.5 : 0);
        if (key == fittedFor) return;
        fittedFor = key;
        double room = width - LabelPadding * 2 - (hasIcon ? IconSize : 0) - 40;
        var font = label.Font;
        for (int size = 44; size >= 24; size -= 2)
        {
            font.Size = size;
            label.SetFont(font);
            label.ForceLayoutPrepass();
            if (label.GetDesiredSize().X <= room) break;
        }
    }

    void OnClick()
    {
        Sounds.Click(this);
        Clicked();
    }

    void OnHover() => Sounds.Hover(this);
}
