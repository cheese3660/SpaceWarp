﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using ReduxLib.Configuration;
using ReduxLib.Logging;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Mods.JSON;
using UnityEditor.VersionControl;

namespace SpaceWarp.API.Backend.Modding;

internal static class PluginRegister
{
    
    
    
    public static void RegisterAllMods()
    {
        RegisterSpaceWarp();
        RegisterInternalMods();
        RegisterMods();
        DisableMods();
    }

    private static void RegisterInternalMods()
    {
        foreach (var mod in IInternalModRegister.Instance.InternalPluginDescriptors)
        {
            mod.Plugin.SWLogger ??= ReduxLib.ReduxLib.GetLogger(mod.Guid);
            mod.Plugin.SWConfiguration = mod.ConfigFile = new JsonConfigFile(Path.Combine(mod.Folder.FullName,"config.json"));
            mod.IsCore = true;
            PluginList.RegisterPlugin(mod);
        }
    }


    private static readonly ILogger Logger = SpaceWarpPlugin.Logger;

    private static bool AssertFolderPath(ISpaceWarpMod plugin, string folderPath)
    {
        if (Path.GetFileName(folderPath) != "mods") return true;

        Logger.LogError(
            $"Found Space Warp mod in the BepInEx/plugins directory. This mod will " +
            $"not be initialized."
        );

    return false;
}

    private static bool AssertModInfoExistence(ISpaceWarpMod plugin, string modInfoPath, string folderPath)
    {
        if (File.Exists(modInfoPath))
        {
            return true;
        }

        Logger.LogError(
            $"Found Space Warp plugin at {modInfoPath} without a swinfo.json in its folder. This mod " +
            $"will not be initialized."
        );

        PluginList.NoteMissingSwinfoError(new SpaceWarpPluginDescriptor(plugin, "unknown", Path.GetFileName(folderPath),
            new ModInfo(), new DirectoryInfo(folderPath)));

    return false;
}

    private static bool TryReadModInfo(
        ISpaceWarpMod plugin,
        string modInfoPath,
        string folderPath,
        out ModInfo? metadata
    )
    {
        try
        {
            metadata = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(modInfoPath));
        }
        catch
        {
            Logger.LogError(
                $"Error reading metadata for spacewarp plugin at {folderPath}. This mod will not be initialized");
            PluginList.NoteMissingSwinfoError(new SpaceWarpPluginDescriptor(plugin, "unknown", Path.GetFileName(folderPath),
                new ModInfo(), new DirectoryInfo(folderPath)));
            metadata = null;
            return false;
        }

        return true;
    }

    private static void RegisterSingleSpaceWarpPlugin(ISpaceWarpMod plugin, string folderPath)
    {

        if (!AssertFolderPath(plugin, folderPath))
        {
            return;
        }

        var modInfoPath = Path.Combine(folderPath!, "swinfo.json");

        if (!AssertModInfoExistence(plugin, modInfoPath, folderPath))
        {
            return;
        }

        if (!TryReadModInfo(plugin, modInfoPath, folderPath, out var metadata))
        {
            return;
        }

        var directoryInfo = new DirectoryInfo(folderPath);
        var descriptor = new SpaceWarpPluginDescriptor(
            plugin,
            metadata!.ModID,
            metadata.Name,
            metadata,
            directoryInfo!,
            true,
            plugin.SWConfiguration
        );
        descriptor.Plugin!.SWMetadata = descriptor;
        
        PluginList.RegisterPlugin(descriptor);
    }

    private static void RegisterSpaceWarp()
    {
        var mod = new UnloadedMod(typeof(SpaceWarpPlugin))
        {
            SWLogger = SpaceWarpPlugin.Logger,
            SWConfiguration = ReduxLib.ReduxLib.ReduxCoreConfig
        };
        var descriptor = new SpaceWarpPluginDescriptor(mod,
            SpaceWarpPlugin.SpaceWarpModInfo.ModID, SpaceWarpPlugin.SpaceWarpModInfo.Name,
            SpaceWarpPlugin.SpaceWarpModInfo, new DirectoryInfo(ReduxLib.ReduxLib.REDUX_FOLDER), true, ReduxLib.ReduxLib.ReduxCoreConfig);
        mod.SWMetadata = descriptor;
        descriptor.IsCore = true;
        PluginList.RegisterPlugin(descriptor);
    }
    private static void RegisterMods()
    {
        var pluginPath = new DirectoryInfo(CommonPaths.MODS_FOLDER);
        if (!pluginPath.Exists)
        {
            pluginPath.Create();
        }
        foreach (var swinfo in pluginPath.GetFiles("swinfo.json", SearchOption.AllDirectories))
        {
            ModInfo swinfoData;
            try
            {
                swinfoData = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(swinfo.FullName));
            }
            catch
            {
                Logger.LogError($"Error reading metadata file: {swinfo.FullName}, this mod will be ignored");
                continue;
            }

            if (swinfoData.Spec < SpecVersion.V1_3)
            {
                Logger.LogWarning(
                    $"Found swinfo information for: {swinfoData.Name}, but its spec is less than 1.3, as such this " +
                    $"mod will be ignored"
                );
                continue;
            }

            // Load the libraries as we get them
            if (Directory.Exists(Path.Combine(swinfo.Directory!.FullName, "lib")) && !ModList.DisabledPluginGuids.Contains(swinfoData.ModID))
            {
                var dirInfo = new DirectoryInfo(Path.Combine(swinfo.Directory!.FullName, "lib"));
                foreach (var dll in dirInfo.GetFiles("*.dll", SearchOption.AllDirectories))
                {
                    Assembly.LoadFile(dll.FullName);
                }
            }
            
            // But then load the 
            ISpaceWarpMod swMod = new AssetOnlyMod(swinfoData.Name);
            if (swinfoData.MainAssembly != null && !ModList.DisabledPluginGuids.Contains(swinfoData.ModID))
            {
                var dll = Path.Combine(swinfo.Directory!.FullName, swinfoData.MainAssembly);
                if (!File.Exists(dll))
                {
                    // TODO: Add a bad assembly error to the mods list
                    Logger.LogError(
                        $"Main assembly {swinfoData.MainAssembly} for {swinfoData.Name} could not be found, this mod will be ignored");
                    continue;
                }

                var asm = Assembly.LoadFile(dll);

                foreach (var type in asm.GetTypes())
                {
                    if (!typeof(ISpaceWarpMod).IsAssignableFrom(type) || type.IsAbstract) continue;
                    swMod = new UnloadedMod(type);
                    break;
                }
            }

            swMod.SWLogger = ReduxLib.ReduxLib.GetLogger(swinfoData.ModID);

            swMod.SWConfiguration =
                new JsonConfigFile(Path.Combine(swinfo.Directory.FullName, swinfoData.ModID + "-config.json"));

            var descriptor = new SpaceWarpPluginDescriptor(
                swMod,
                swinfoData.ModID,
                swinfoData.Name,
                swinfoData, swinfo.Directory,
                true,
                swMod.SWConfiguration
            );
            swMod.SWMetadata = descriptor;

            Logger.LogInfo($"Attempting to register mod: {swinfoData.ModID}, {swinfoData.Name}");

            if (PluginList.AllPlugins.Any(
                    x => string.Equals(x.Guid, swinfoData.ModID, StringComparison.InvariantCultureIgnoreCase)
                ))
            {
                continue;
            }

            // Now we can just add it to our plugin list
            PluginList.RegisterPlugin(descriptor);
        }
    }

    private static void DisableMods()
    {
        foreach (var mod in ModList.DisabledPluginGuids)
        {
            PluginList.Disable(mod);
        }
    }
    

}