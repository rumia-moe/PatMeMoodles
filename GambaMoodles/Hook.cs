using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using GambaMoodles.Windows;
using System;

namespace GambaMoodles;

public unsafe class Hook : IDisposable
{
    private readonly Plugin plugin;

    private int pendingIn = 0;
    private int pendingOut = 0;
    private string pendingTargetName = string.Empty;

    public Hook(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "Trade", OnTradeUpdate);
        Plugin.ChatGUI.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnTradeUpdate);
        Plugin.ChatGUI.ChatMessage -= OnChatMessage;
    }

    private void OnTradeUpdate(AddonEvent type, AddonArgs args)
    {
        var tradeArray = TradeNumberArray.Instance();
        if (tradeArray == null) return;

        // State 3 = Both players accepted
        if (tradeArray->OtherState == 3 && tradeArray->SelfState == 3)
        {
            if (string.IsNullOrEmpty(pendingTargetName))
            {
                var addon = (AddonTrade*)args.Addon.Address;

                // Pull name from UI node 20
                var targetNameNode = addon->AtkUnitBase.UldManager.NodeList[20]->GetAsAtkTextNode();
                string targetName = targetNameNode->NodeText.ToString();

                if (!string.IsNullOrEmpty(targetName))
                {
                    pendingIn = (int)tradeArray->ReceiveGilCount;
                    pendingOut = (int)tradeArray->SendGilCount;
                    pendingTargetName = targetName;

                    Plugin.Log.Info($"[PRIME] Trade with {pendingTargetName} | In: {pendingIn} | Out: {pendingOut}");
                }
            }
        }
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if ((ushort)type == 57 && !string.IsNullOrEmpty(pendingTargetName))
        {
            string text = message.TextValue;
            if (text.Contains("Trade complete", StringComparison.OrdinalIgnoreCase))
            {
                UpdateBank();
                Reset();
            }
            else if (text.Contains("Trade canceled", StringComparison.OrdinalIgnoreCase))
            {
                Reset();
            }
        }
    }

    private void UpdateBank()
    {
        var bank = plugin.Bank.sources;

        if (!bank.ContainsKey(pendingTargetName))
            bank[pendingTargetName] = 0;

        int netChange = pendingIn - pendingOut;
        bank[pendingTargetName] += netChange;

        plugin.Configuration.Save();
        Plugin.Log.Info($"[SUCCESS] {pendingTargetName} balance: {bank[pendingTargetName]}");
        plugin.IPCService?.UpdateMoodles();
    }

    private void Reset()
    {
        pendingIn = 0;
        pendingOut = 0;
        pendingTargetName = string.Empty;
    }
}
