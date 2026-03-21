using Dalamud.Configuration;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;

namespace GambaMoodles;

public class GambaMoodle
{
    public string Id = string.Empty;
    public string Title = string.Empty;
    public string Description = string.Empty;
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public List<GambaMoodle> moodles = [];

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Svc.PluginInterface.SavePluginConfig(this);
    }
}

public class Bank
{
    public Dictionary<string, int> sources = [];
    public string dealer = string.Empty;
}
