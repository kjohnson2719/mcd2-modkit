using System.Collections.Generic;
using NeoRune;
using UE.CommonUI;
using UE.CoreUObject;
using UE.Engine;
using UE.InputCore;
using UE.UMG;

namespace ModKit;

/// <summary>
/// The game's button icons, by key: the rows of its input action tables (what its hints show icons for), found through the
/// action icons on screen, each table's rows by the key they're on for the controller and for the keyboard. Give a row to
/// a UCommonActionWidget (SetInputAction) and it shows that button's icon for the controller in use, as the game's do.
/// <code>
/// icons = ActionIcons.Create(this);
/// icons.Find();   // a few times while the game's menus are on screen: their tables come with them
/// if (icons.Row("Gamepad_FaceButton_Left", true, out var row)) actionWidget.SetInputAction(row);
/// </code>
/// Not every key has one: the game only has icons for keys it uses itself.
/// </summary>
public class ActionIcons : UObject
{
    Dictionary<string, FDataTableRowHandle> padRows;
    Dictionary<string, FDataTableRowHandle> keyRows;
    List<UDataTable> tables;
    /// <summary>How many times <see cref="Find"/> has looked.</summary>
    public int Searches;

    public static ActionIcons? Create(UObject owner)
    {
        var icons = UGameplayStatics.SpawnObject(Unreal.ClassOf<ActionIcons>(), owner) as ActionIcons;
        icons?.Init();
        return icons;
    }

    void Init()
    {
        padRows = new Dictionary<string, FDataTableRowHandle>();
        keyRows = new Dictionary<string, FDataTableRowHandle>();
        tables = new List<UDataTable>();
    }

    /// <summary>The row on a key (its name, e.g. "Gamepad_FaceButton_Bottom" or "S"), for the controller or the keyboard.</summary>
    public bool Row(string key, bool gamepad, out FDataTableRowHandle row)
    {
        if (gamepad) return padRows.TryGetValue(key, out row);
        return keyRows.TryGetValue(key, out row);
    }

    public bool Has(string key, bool gamepad) => gamepad ? padRows.ContainsKey(key) : keyRows.ContainsKey(key);

    /// <summary>
    /// Looks through the action icons on screen for their tables, and adds each new table's rows. Returns how many rows
    /// it has now.
    /// </summary>
    public int Find()
    {
        Searches++;
        var found = new List<UDataTable>();
        UWidgetBlueprintLibrary.GetAllWidgetsOfClass(this, out var roots, Unreal.ClassOf<UUserWidget>(), false);
        var pending = new List<UWidget>();
        int visited = 0;
        foreach (var root in roots)
        {
            if (root == null || root.WidgetTree == null || root.WidgetTree.RootWidget == null) continue;
            pending.Add(root.WidgetTree.RootWidget);
            while (pending.Count > 0 && visited < 50000)
            {
                var widget = pending[pending.Count - 1];
                pending.RemoveAt(pending.Count - 1);
                visited++;
                if (widget == null || widget is UUserWidget) continue;
                var icon = widget as UCommonActionWidget;
                if (icon != null)
                {
                    foreach (var row in icon.InputActions)
                        if (row.DataTable != null && !tables.Contains(row.DataTable) && !found.Contains(row.DataTable)) found.Add(row.DataTable);
                    continue;
                }
                var panel = widget as UPanelWidget;
                if (panel != null) pending.AddRange(panel.GetAllChildren());
            }
            pending.Clear();
        }
        foreach (var table in found)
        {
            tables.Add(table);
            UDataTableFunctionLibrary.GetDataTableRowNames(table, out var names);
            foreach (var name in names)
            {
                if (!Unreal.DataTableRow(table, name, out FCommonInputActionDataBase row)) continue;
                var handle = new FDataTableRowHandle { DataTable = table, RowName = name };
                string pad = $"{row.DefaultGamepadInputTypeInfo.Key.KeyName}";
                string keyboard = $"{row.KeyboardInputTypeInfo.Key.KeyName}";
                if (pad != "None" && !padRows.ContainsKey(pad)) padRows.Add(pad, handle);
                if (keyboard != "None" && !keyRows.ContainsKey(keyboard)) keyRows.Add(keyboard, handle);
            }
        }
        return padRows.Count + keyRows.Count;
    }

    /// <summary>The keyboard keys that have icons (for the log).</summary>
    public string KeyboardKeys()
    {
        string text = "";
        foreach (var pair in keyRows) text += " " + pair.Key;
        return text;
    }

    /// <summary>A key's short name, for when there's no icon for it: "R3" for the right stick's click, "A", "Esc"...</summary>
    public static string Label(FKey key) => LabelOf($"{key.KeyName}", key);

    public static string LabelOf(string name, FKey key)
    {
        if (name == "Gamepad_LeftThumbstick") return "L3";
        if (name == "Gamepad_RightThumbstick") return "R3";
        if (name == "Gamepad_FaceButton_Bottom") return "A";
        if (name == "Gamepad_FaceButton_Left") return "X";
        if (name == "Gamepad_FaceButton_Top") return "Y";
        if (name == "Gamepad_FaceButton_Right") return "B";
        if (name == "Gamepad_Special_Left") return "View";
        if (name == "Gamepad_Special_Right") return "Menu";
        if (name == "Escape") return "Esc";
        if (name == "Delete") return "Del";
        return $"{UKismetInputLibrary.Key_GetDisplayName(key, true)}";
    }
}
