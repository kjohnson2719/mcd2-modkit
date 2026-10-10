# ModKit

Shared code for Minecraft Dungeons II mods made with [NeoRune](https://www.nexusmods.com/minecraftdungeons2/mods/47),
from [Waypoints](https://github.com/kjohnson2719/mcd2-waypoints) and
[Loadouts](https://github.com/kjohnson2719/mcd2-loadouts). It's compiled into each mod: players install nothing extra.

What's in it, all found out in game while making those mods:

| | |
| --- | --- |
| `UpdateCheck` | Tells the player when a newer version of the mod is out on GitHub (a toast, once per version). |
| `MenuKeys` | Keys while one of the game's menus is open (which the usual key listeners don't hear). |
| `ActionIcons` | The game's button icons by key, for hints that switch with the controller like the game's. |
| `Screens` | Whether a widget really shows (through switchers), the controller in use, a screen's scale. |
| `GameLook` | The game's font and typefaces, and its colours. |
| `GamePanel` | The game's item panel (ribbon, frame, blur, button bar) for panels and drawers of your own. |
| `StyledButton` | A button made like one of the game's, shown beside it. |
| `StyleDump` | Logs how the game draws its widgets (brushes, fonts, materials and their parameters). |
| `ModInfo` | The mod's title, version and GitHub repository (made from its .csproj at build time). |

The targets file also gives development builds the `DEBUG` symbol. NeoRune compiles the sources with its own symbols,
so without it `#if DEBUG` is always false.

## Using it

Put the kit next to the mod (`..\ModKit`) and add to the end of the mod's `.csproj`:

```xml
<PropertyGroup>
  <ModKitRepo>owner/repo</ModKitRepo>
  <ModKitDir Condition="'$(ModKitDir)' == '' and Exists('$(MSBuildProjectDirectory)\ModKit\ModKit.targets')">$(MSBuildProjectDirectory)\ModKit\</ModKitDir>
  <ModKitDir Condition="'$(ModKitDir)' == ''">$(MSBuildProjectDirectory)\..\ModKit\</ModKitDir>
</PropertyGroup>
<Import Project="$(ModKitDir)ModKit.targets" />
```

On GitHub Actions, check the kit out into the mod's folder:

```yaml
- name: Check out the mod kit
  uses: actions/checkout@v4
  with:
    repository: kjohnson2719/mcd2-modkit
    path: ModKit
```

Then, for example:

```csharp
using ModKit;

// The update check, a moment after the level starts (with a setting to turn it off):
if (CheckUpdates) UpdateCheck.Start(this);

// Keys on a menu screen:
keys = MenuKeys.Create(this);
keys.Track(openKey);
keys.Pressed += OnKey;
keys.SetOn(menuOpen);   // every frame
```

Mods that use `UpdateCheck` should say so on their page: it asks GitHub for the latest release when the game starts (at
most every 6 hours).

## License

MIT
