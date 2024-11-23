using JetBrains.Annotations;

namespace SpaceWarp.API;

/// <summary>
/// Contains paths to various directories.
/// </summary>
[PublicAPI]
public static class CommonPaths
{
#if UNITY_EDITOR
    public const string MODS_FOLDER = "Assets/Mods";
    public const string DISABLED_PLUGINS = "Assets/disabled_plugins.cfg";
    public const string HASH_LOCATION = "Assets/mods_list_hash.txt";
#else
    public const string MODS_FOLDER = "./mods/";
    public const string DISABLED_PLUGINS = "disabled_plugins.cfg";
    public const string HASH_LOCATION = "mods_list_hash.txt";
#endif
}