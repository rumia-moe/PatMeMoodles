using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace GambaMoodles;

public unsafe class Hook : IDisposable
{
    private readonly Plugin plugin;

    private readonly TradeDetectionManager.OnTradeEndDelegate onTradeEndDelegate;

    public Hook(Plugin plugin)
    {
        this.plugin = plugin;
        onTradeEndDelegate = new(OnTradeEnd);
        TradeDetectionManager.OnTradeEnd += onTradeEndDelegate;
    }

    public void Dispose()
    {
        TradeDetectionManager.OnTradeEnd -= onTradeEndDelegate;
    }

    private unsafe void OnTradeEnd(IPlayerCharacter? partner, TradeDetectionManager.TradeDescriptor? result)
    {


        if (partner == null) return;
        if (result == null || result.ReceivedGil == 0) return;


        var bank = plugin.Bank.sources;

        if (!bank.ContainsKey(partner.Name.TextValue))
            bank[partner.Name.TextValue] = 0;

        bank[partner.Name.TextValue] += result.ReceivedGil;

        plugin.Configuration.Save();
        Plugin.Log.Info($"[SUCCESS] {partner.Name.TextValue} balance: {bank[partner.Name.TextValue]}");
        Svc.Framework.RunOnFrameworkThread(() => plugin.IPCService?.UpdateMoodles()).ConfigureAwait(false);


    }


}
