using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.GameHelpers;
using System;

namespace GambaMoodles;

public unsafe class TradeHook : IDisposable
{
    private readonly Plugin plugin;

    private readonly TradeDetectionManager.OnTradeEndDelegate onTradeEndDelegate;

    public TradeHook(Plugin plugin)
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
        plugin.MoodlesBridge.Set();

    }

}
