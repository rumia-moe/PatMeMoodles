using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace PatMeMoodles;

public class CounterMoodle
{
    public string Id = string.Empty;
    public string Title = string.Empty;
    public string Description = string.Empty;
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public List<CounterMoodle> counterMoodles = [];

    public Dictionary<ushort, int> emoteCounter = [];

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
