using KSA;

namespace OhMyStars;

/// <summary>
/// Resolves the directory this mod is actually installed/loaded from, so asset files
/// (skycultures/, lines_in_20.txt, athyg_32_reduced_m10.csv, settings.ini) are found
/// whether the game loads the mod from the Documents mods folder or an external
/// modloader loads it from an instance-specific folder.
/// </summary>
internal static class ModPaths {
    private static string? _modRoot;

    /// <summary>The folder containing the mod's files (mod.toml, assets, and OhMyStars.dll).</summary>
    public static string ModRoot {
        get {
            _modRoot ??= ResolveModRoot();
            return _modRoot;
        }
    }

    public static string Combine(params string[] paths) {
        string[] all = new string[paths.Length + 1];
        all[0] = ModRoot;
        Array.Copy(paths, 0, all, 1, paths.Length);
        return Path.Combine(all);
    }

    private static string ResolveModRoot() {
        // Prefer the running assembly's own directory: the mod's assets are always
        // deployed next to OhMyStars.dll, and this stays correct even when an external
        // modloader loads the mod from a folder the game's ModLibrary doesn't know about
        // (or when a second, stale copy exists in the default mods folder).
        string? assemblyDirectory = Path.GetDirectoryName(typeof(ModPaths).Assembly.Location);
        if(!string.IsNullOrWhiteSpace(assemblyDirectory)) {
            return assemblyDirectory;
        }

        // Fall back to the game's own mod registry (mod id == mod folder name).
        Mod? mod = ModLibrary.Find("OhMyStars");
        if(mod is not null && mod != Mod.Empty && !string.IsNullOrWhiteSpace(mod.DirectoryPath)) {
            return mod.DirectoryPath;
        }

        // Last resort: the default game mods folder under Documents.
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Kitten Space Agency",
            "mods",
            "OhMyStars");
    }
}
