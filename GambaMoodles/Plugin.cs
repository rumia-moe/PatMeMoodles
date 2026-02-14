using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.EzEventManager;
using ECommons.Logging;
using GambaMoodles.Windows;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.BannerHelper.Delegates;

namespace GambaMoodles;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; set; } = null!;
    [PluginService] internal static ICondition Condition { get; set; } = null!;
    [PluginService] internal static IFramework Framework { get; set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; set; } = null!;
    [PluginService] internal static IChatGui ChatGUI { get; set; } = null!;

    private const string CommandName = "/gm";

    public Configuration Configuration { get; init; }
    public Bank Bank { get; init; }

    public readonly WindowSystem WindowSystem = new("GambaMoodles");
    public MainWindow MainWindow { get; init; }
    private Hook Hook { get; init; }
    public IPCService IPCService { get; init; }

    private int loginTicks = 0;

    public Plugin(IDalamudPluginInterface pi)
    {
        ECommonsMain.Init(pi, this);
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Bank = new();
        MainWindow = new MainWindow(this);
        Hook = new Hook(this);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the main window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"{PluginInterface.Manifest.Name} loaded.");

        // Initialize the IPC Service (Do not touch the code inside IPCService.cs)
        this.IPCService = new IPCService(this);

        // SAFETY: We attach to the Framework and Login events.
        // We DO NOT call UpdateMoodles here to avoid the "Not on main thread" crash.
        Framework.Update += OnFrameworkUpdate;
        ClientState.Login += OnLogin;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // If not logged in, keep resetting the counter
        if (!ClientState.IsLoggedIn || ClientState.LocalPlayer == null)
        {
            loginTicks = 0;
            return;
        }

        // Once logged in (or on plugin reload), wait for the game to settle.
        // 30 frames is roughly 0.5s at 60fps.
        if (loginTicks < 30)
        {
            loginTicks++;
            if (loginTicks == 30)
            {
                // Now safely on the main thread, trigger the initial sync
                Log.Debug("Login/Load settled on main thread. Triggering initial Moodles sync.");
                this.IPCService.UpdateMoodles();
            }
        }
    }

    private void OnLogin()
    {
        // Reset ticks so the framework loop triggers for the new character
        loginTicks = 0;
    }

    public void Dispose()
    {
        // Unsubscribe to prevent the "Leaked hook" warning and memory leaks
        Framework.Update -= OnFrameworkUpdate;
        ClientState.Login -= OnLogin;

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);

        Hook.Dispose();
        ECommonsMain.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        MainWindow.Toggle();
    }
    
    public void ToggleMainUi() => MainWindow.Toggle();

    public static string FormatNumber(double value)
    {
        // Store the sign and work with the absolute value for scaling
        double absValue = Math.Abs(value);
        string suffix = "";
        double scaledValue = absValue;

        if (absValue >= 1000000)
        {
            scaledValue = absValue / 1000000D;
            suffix = "M";
        }
        else if (absValue >= 1000)
        {
            scaledValue = absValue / 1000D;
            suffix = "K";
        }

        // Multiply the scaled value back by the original sign
        double finalValue = scaledValue * Math.Sign(value);

        return finalValue.ToString("0.00") + suffix;
    }

}
