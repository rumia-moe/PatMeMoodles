using Dalamud.Configuration;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;

namespace PatMeMoodles;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public Dictionary<Guid, (string, string)> moodles = [];
    public Dictionary<ushort, int> emotes = [];

    public static Configuration Load(IDalamudPluginInterface pi)
    {
        var config = pi.GetPluginConfig();

        if (config == null) return new Configuration();

        if (config.Version < 1)
        {
            var json = System.IO.File.ReadAllText(pi.ConfigFile.FullName);
            var oldConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<Version0Migrator>(json);

            if (oldConfig != null)
            {
                var migrated = oldConfig.DoMigrate();
                migrated.Save();
                return migrated;
            }
        }

        return (Configuration)config;
    }

    public void Save() => Svc.PluginInterface.SavePluginConfig(this);

    private class Version0Migrator
    {
        public List<OldMoodle> counterMoodles = [];
        public Dictionary<ushort, int> emoteCounter = [];

        public struct OldMoodle { public string Id; public string Title; public string Description; }

        public Configuration DoMigrate()
        {
            var cfg = new Configuration { Version = 1, emotes = this.emoteCounter };
            foreach (var m in counterMoodles)
            {
                cfg.moodles[Guid.Parse(m.Id)] = (m.Title, m.Description);
            }
            return cfg;
        }
    }
}
