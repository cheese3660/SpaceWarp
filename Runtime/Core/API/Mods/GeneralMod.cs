﻿using ReduxLib.Configuration;
using ReduxLib.Logging;

namespace SpaceWarp.API.Mods;

public abstract class GeneralMod : ISpaceWarpMod
{
    public virtual void OnPreInitialized()
    {
    }

    public void OnInitialized()
    {
    }

    public void OnPostInitialized()
    {
    }

    public ILogger SWLogger { get; set; }
    public IConfigFile SWConfiguration { get; set; }
    public SpaceWarpPluginDescriptor SWMetadata { get; set; }
}