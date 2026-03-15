using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using ECommons.Logging;
using System;
using System.Linq;

namespace PatMeMoodles;

public class EmoteHook : IDisposable
{

    private Plugin plugin;

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private Hook<OnEmoteFuncDelegate>? HookEmote { get; init; }

    public EmoteHook(Plugin plugin)
    {
        this.plugin = plugin;
        HookEmote = Svc.Hook.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
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
            if (Svc.Objects.LocalPlayer == null) return;
            if (Svc.Objects.FirstOrDefault(x => (ulong)x.Address == instigatorAddr) is IPlayerCharacter instigator && instigator.GameObjectId == targetId) return;
            if (targetId != Svc.Objects.LocalPlayer.GameObjectId) return;
            var newVal = 1;
            if (plugin.Configuration.emotes.TryGetValue(emoteId, out var oldVal))
            {
                newVal += oldVal;
            }
            plugin.Configuration.emotes[emoteId] = newVal;
            plugin.Configuration.Save();
            plugin.MoodlesBridge.Set();
        }
        catch (Exception e)
        {
            PluginLog.Error(e.Message);
        }
        HookEmote?.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }
}
