using NeoRune;
using UE.CoreUObject;
using UE.SlateCore;
using UE.UMG;

namespace ModKit;

/// <summary>
/// The game's look, as its own screens use it (found by dumping them with <see cref="StyleDump"/>). Its font is
/// FF_Spicewood, in several typefaces; sizes are in its screens' units (they're laid out at 4K sizes and scaled to fit:
/// lay yours out at the same sizes in a scale box, see <see cref="Screens.Scale"/>).
/// </summary>
public static class GameLook
{
    /// <summary>Titles and numbers: item names (57), the "Equipped" ribbon (39), the level (120).</summary>
    public const string TitleFace = "TenV2";
    /// <summary>Headers ("Level", "Effects": 45) and descriptions (36).</summary>
    public const string TextFace = "Sixteen Semibold";
    /// <summary>Button labels (44) and hints.</summary>
    public const string ButtonFace = "Sixteen Bold";
    /// <summary>Small capital tags ("Enchanted": 29, "Unique": 36).</summary>
    public const string TagFace = "FiveV2";

    /// <summary>Headers' grey ("Level", "Weapons", "Effects").</summary>
    public static FLinearColor HeaderGrey() => Ui.Color(0.57f, 0.59f, 0.65f, 1);
    /// <summary>Descriptions' grey.</summary>
    public static FLinearColor TextGrey() => Ui.Color(0.57f, 0.59f, 0.64f, 1);
    /// <summary>Light text (enchantment names).</summary>
    public static FLinearColor LightText() => Ui.Color(0.8f, 0.82f, 0.91f, 1);
    /// <summary>The item power's blue.</summary>
    public static FLinearColor PowerBlue() => Ui.Color(0.08f, 0.65f, 0.98f, 1);
    /// <summary>Enchantments' purple.</summary>
    public static FLinearColor EnchantPurple() => Ui.Color(0.56f, 0.1f, 0.91f, 1);

    /// <summary>The font object of one of the game's text blocks (any will do: the typeface is picked separately).</summary>
    public static UObject? FontOf(UTextBlock? text) => text != null ? text.Font.FontObject : null;

    /// <summary>The game's font in a typeface (<see cref="TitleFace"/>...) at a size.</summary>
    public static FSlateFontInfo Font(UObject? fontObject, string typeface, int size)
    {
        var font = new FSlateFontInfo();
        font.FontObject = fontObject;
        font.TypefaceFontName = typeface;
        font.Size = size;
        return font;
    }

    /// <summary>Text in the game's font, with its usual soft shadow.</summary>
    public static void Style(UTextBlock? text, UObject? fontObject, string typeface, int size, FLinearColor colour)
    {
        if (text == null) return;
        if (fontObject != null) text.SetFont(Font(fontObject, typeface, size));
        text.SetColorAndOpacity(Ui.SlateColor(colour));
        text.SetShadowOffset(new FVector2D { X = 0, Y = 4 });
        text.SetShadowColorAndOpacity(Ui.Color(0, 0, 0, 0.5f));
    }
}
