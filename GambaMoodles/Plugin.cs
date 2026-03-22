using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Events;
using GambaMoodles.Windows;
using System;

namespace GambaMoodles;

public sealed class Plugin : IDalamudPlugin
{

    private const string CommandName = "/gm";

    public Configuration Configuration { get; init; }
    public Bank Bank { get; init; }
    public MoodlesBridge MoodlesBridge { get; init; }
    private TradeHook TradeHook { get; init; }

    public readonly WindowSystem WindowSystem = new("GambaMoodles");
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        Configuration = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Bank = new();

        MoodlesBridge = new(Configuration, Bank);
        TradeHook = new(this);

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        Svc.Commands.AddHandler(CommandName, new CommandInfo((string command, string args) => ConfigWindow.Toggle())
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        Svc.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += ConfigWindow.Toggle;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ConfigWindow.Toggle;
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= ConfigWindow.Toggle;
        
        WindowSystem.RemoveAllWindows();

        Svc.Commands.RemoveHandler(CommandName);

        TradeHook.Dispose();

        MoodlesBridge.Dispose();

        ECommonsMain.Dispose();
    }

    public static string FormatNumber(double value)
    {
        double absValue = Math.Abs(value);
        int sign = Math.Sign(value);

        if (absValue >= 1000000)
        {
            double mValue = (absValue / 1000000D) * sign;
            // "0.##" shows decimals only if they exist (e.g., 1.5M or 2M)
            return mValue.ToString("0.##") + "M";
        }

        if (absValue >= 1000)
        {
            double kValue = (absValue / 1000D) * sign;
            return kValue.ToString("0.##") + "K";
        }

        // For normal numbers, use N0 to add commas but NO decimals
        // e.g., 950, -42, 125
        return value.ToString("N0");
    }

}
