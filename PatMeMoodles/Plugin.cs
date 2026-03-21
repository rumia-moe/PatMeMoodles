using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using PatMeMoodles.Windows;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Events;

namespace PatMeMoodles;

public sealed class Plugin : IDalamudPlugin
{

    private const string CommandName = "/pmm";

    public Configuration Configuration { get; init; }
    public MoodlesBridge MoodlesBridge { get; init; }
    private EmoteHook EmoteHook { get; init; }

    public readonly WindowSystem WindowSystem = new("PatMeMoodles");
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        Configuration = Configuration.Load(Svc.PluginInterface);

        MoodlesBridge = new(Configuration);
        EmoteHook = new(this);

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        Svc.Commands.AddHandler(CommandName, new CommandInfo((string command, string args) => ConfigWindow.Toggle())
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        Svc.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += ConfigWindow.Toggle;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ConfigWindow.Toggle;

        ProperOnLogin.RegisterAvailable(MoodlesBridge.Set, true);
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= ConfigWindow.Toggle;
        
        WindowSystem.RemoveAllWindows();

        Svc.Commands.RemoveHandler(CommandName);

        EmoteHook.Dispose();

        ECommonsMain.Dispose();
    }
}
