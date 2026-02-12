using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PatMeMoodles;

public class Hook : IDisposable
{

    private Plugin plugin;

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private Hook<OnEmoteFuncDelegate>? HookEmote { get; init; }

    public Hook(Plugin plugin)
    {
        this.plugin = plugin;
        HookEmote = Plugin.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
        HookEmote.Enable();
    }

    public void Dispose()
    {
        HookEmote?.Dispose();
    }

    private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            //OnEmote?.Invoke(instigatorAddr, emoteId, targetId);
            if (Plugin.ObjectTable.LocalPlayer == null) return;
            if (Plugin.ObjectTable.FirstOrDefault(x => (ulong)x.Address == instigatorAddr) is IPlayerCharacter instigator && instigator.GameObjectId == targetId) return;
            if (targetId != Plugin.ObjectTable.LocalPlayer.GameObjectId) return;
            var newVal = 1;
            if (plugin.Configuration.emoteCounter.TryGetValue(emoteId, out var oldVal))
            {
                newVal += oldVal;
            }
            plugin.Configuration.emoteCounter[emoteId] = newVal;
            plugin.Configuration.Save();
            plugin.IPCService.UpdateMoodles();
        }
        catch (Exception e)
        {
            PluginLog.Error(e.Message);
        }
        HookEmote?.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }
}
