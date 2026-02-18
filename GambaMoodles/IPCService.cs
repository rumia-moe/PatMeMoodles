using ECommons.EzIpcManager;
using MemoryPack;
using Moodles.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;

namespace GambaMoodles;

public class IPCService : IDisposable
{
    private Plugin plugin;

    private List<MyStatus> pendingStatusList = null;
    private HashSet<Guid> guidsInTransition = new();
    private string lastKnownGoodPack = string.Empty;

    [EzIPC] private readonly Func<nint, string> GetStatusManagerByPtrV2;
    [EzIPC] private readonly Action<nint, string> SetStatusManagerByPtrV2;

    public IPCService(Plugin plugin)
    {
        this.plugin = plugin;
        EzIPC.Init(this, "Moodles");
        Plugin.Condition.ConditionChange += OnConditionChange;
        Plugin.Log.Information("[MoodlesSync] IPCService Initialized.");
    }

    public void Dispose()
    {
        Plugin.Condition.ConditionChange -= OnConditionChange;
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if ((flag == ConditionFlag.BetweenAreas || flag == ConditionFlag.OccupiedInCutSceneEvent) && value == false)
        {
            var playerAddress = Plugin.ObjectTable.LocalPlayer?.Address ?? nint.Zero;
            // Restore EVERYTHING (Managed + Unmanaged + Gamba) captured before/during transition
            if (playerAddress != nint.Zero && !string.IsNullOrEmpty(lastKnownGoodPack))
            {
                Plugin.Log.Info($"[MoodlesSync] Zone recovery: Restoring full state.");
                SetStatusManagerByPtrV2(playerAddress, lastKnownGoodPack);
                guidsInTransition.Clear();
                pendingStatusList = null;
            }
        }
    }

    [EzIPCEvent]
    private void Ready() => UpdateMoodles();

    [EzIPCEvent]
    private void StatusManagerModified(nint charaPtr)
    {
        if (charaPtr != Plugin.ObjectTable.LocalPlayer?.Address) return;

        if (guidsInTransition.Count > 0 && pendingStatusList != null)
        {
            var currentRaw = GetStatusManagerByPtrV2(charaPtr);
            var currentStatuses = Unpack(currentRaw);

            // Check if the specific GUIDs we are trying to update are gone yet
            bool oldGuidsPresent = currentStatuses.Any(x => guidsInTransition.Contains(x.GUID));

            if (!oldGuidsPresent)
            {
                Plugin.Log.Info("[MoodlesSync] Wipe confirmed. Re-applying with updated text.");
                // Update our backup with the full list we're about to send
                lastKnownGoodPack = Pack(pendingStatusList);

                SetStatusManagerByPtrV2(charaPtr, lastKnownGoodPack);
                guidsInTransition.Clear();
                pendingStatusList = null;
            }
            return;
        }

        UpdateMoodles();
    }

    public void UpdateMoodles()
    {
        var playerAddress = Plugin.ObjectTable.LocalPlayer?.Address ?? nint.Zero;
        if (playerAddress == nint.Zero || guidsInTransition.Count > 0) return;

        var raw = GetStatusManagerByPtrV2(playerAddress);
        if (string.IsNullOrEmpty(raw)) return;

        var currentStatuses = Unpack(raw);
        bool needsSync = false;
        var guidsToWipe = new HashSet<Guid>();

        foreach (var status in currentStatuses)
        {
            var config = plugin.Configuration.moodles.FirstOrDefault(x => x.Id == status.GUID.ToString());

            if (config != null)
            {
                var pTitle = Parse(config.Title);
                var pDesc = Parse(config.Description);

                if (status.Title != pTitle || status.Description != pDesc)
                {
                    needsSync = true;
                    status.Title = pTitle;
                    status.Description = pDesc;
                    guidsToWipe.Add(status.GUID);
                }
            }
            // If config is null, it belongs to Gamba or is unmanaged. We leave it alone.
        }

        if (needsSync)
        {
            // pendingStatusList contains the full merged state (Our updates + their moodles)
            pendingStatusList = currentStatuses;
            guidsInTransition = guidsToWipe;

            // Send a list that includes ALL moodles EXCEPT the ones we are refreshing
            var wipeList = currentStatuses.Where(s => !guidsToWipe.Contains(s.GUID)).ToList();
            SetStatusManagerByPtrV2(playerAddress, Pack(wipeList));
        }
        else
        {
            // If no sync is needed, just keep our backup of the live state fresh
            lastKnownGoodPack = raw;
        }
    }

    private List<MyStatus> Unpack(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return new List<MyStatus>();
        try { return MemoryPackSerializer.Deserialize<List<MyStatus>>(Convert.FromBase64String(base64), SerializerOptions); }
        catch { return new List<MyStatus>(); }
    }

    private string Pack(List<MyStatus> list) => Convert.ToBase64String(MemoryPackSerializer.Serialize(list, SerializerOptions));

    private static readonly MemoryPackSerializerOptions SerializerOptions = new() { StringEncoding = StringEncoding.Utf16 };

    public string Parse(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, @"@gamba", match =>
        {

            if (plugin.Bank.dealer == null) return "0.00";

            return Plugin.FormatNumber(plugin.Bank.sources.TryGetValue(plugin.Bank.dealer, out var val) ? val : 0);

        });
    }
}
