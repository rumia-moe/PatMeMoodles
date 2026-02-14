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
        Plugin.Log.Information("[MoodlesSync] IPCService Initialized with Heavy Logging.");
    }

    public void Dispose()
    {
        Plugin.Condition.ConditionChange -= OnConditionChange;
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if ((flag == ConditionFlag.BetweenAreas || flag == ConditionFlag.OccupiedInCutSceneEvent) && value == false)
        {
            Plugin.Log.Info($"[MoodlesSync] Transition Ended ({flag}). Triggering Recovery Re-apply.");

            var playerAddress = Plugin.ObjectTable.LocalPlayer?.Address ?? nint.Zero;
            if (playerAddress != nint.Zero && !string.IsNullOrEmpty(lastKnownGoodPack))
            {
                SetStatusManagerByPtrV2(playerAddress, lastKnownGoodPack);
                guidsInTransition.Clear();
                pendingStatusList = null;
            }
        }
    }

    [EzIPCEvent]
    private void Ready()
    {
        Plugin.Log.Info("[MoodlesSync] IPC Ready Event received from Moodles.");
        UpdateMoodles();
    }

    [EzIPCEvent]
    private void StatusManagerModified(nint charaPtr)
    {
        if (charaPtr != Plugin.ObjectTable.LocalPlayer?.Address) return;

        // VERBOSE: Log when the sync loop checks for confirmation
        if (guidsInTransition.Count > 0 && pendingStatusList != null)
        {
            Plugin.Log.Debug($"[MoodlesSync] StatusModified Event: Checking if {guidsInTransition.Count} GUIDs are wiped...");
            var currentStatuses = Unpack(GetStatusManagerByPtrV2(charaPtr));

            bool oldGuidsPresent = currentStatuses.Any(x => guidsInTransition.Contains(x.GUID));

            if (!oldGuidsPresent)
            {
                Plugin.Log.Info("[MoodlesSync] RE-APPLY PHASE: Wipe confirmed. Pushing final updated list.");
                lastKnownGoodPack = Pack(pendingStatusList);

                var dataToSend = lastKnownGoodPack;
                guidsInTransition.Clear();
                pendingStatusList = null;

                SetStatusManagerByPtrV2(charaPtr, dataToSend);
            }
            else
            {
                Plugin.Log.Warning("[MoodlesSync] RE-APPLY DELAYED: Old GUIDs still visible in manager. Waiting for next cycle.");
            }
            return;
        }

        UpdateMoodles();
    }

    public void UpdateMoodles()
    {
        // ENTRY LOGGING
        Plugin.Log.Verbose("[MoodlesSync] Function 'UpdateMoodles' called.");

        var playerAddress = Plugin.ObjectTable.LocalPlayer?.Address ?? nint.Zero;
        if (playerAddress == nint.Zero)
        {
            Plugin.Log.Warning("[MoodlesSync] Update aborted: LocalPlayer address is null.");
            return;
        }

        if (guidsInTransition.Count > 0)
        {
            Plugin.Log.Debug($"[MoodlesSync] Update throttled: {guidsInTransition.Count} GUIDs currently in transition.");
            return;
        }

        var raw = GetStatusManagerByPtrV2(playerAddress);
        if (string.IsNullOrEmpty(raw))
        {
            Plugin.Log.Debug("[MoodlesSync] Update skipped: Status Manager is currently empty (Base64 null).");
            return;
        }

        var currentStatuses = Unpack(raw);
        Plugin.Log.Verbose($"[MoodlesSync] Processing {currentStatuses.Count} active moodles.");

        bool needsSync = false;
        var nextList = new List<MyStatus>();
        var wipeList = new List<MyStatus>();
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
                    Plugin.Log.Info($"[MoodlesSync] SYNC REQUIRED: GUID {status.GUID} text changed.");
                    needsSync = true;
                    status.Title = pTitle;
                    status.Description = pDesc;
                    guidsToWipe.Add(status.GUID);
                }
                nextList.Add(status);
            }
            else
            {
                nextList.Add(status);
            }
        }

        if (needsSync)
        {
            Plugin.Log.Info($"[MoodlesSync] WIPE PHASE: Removing {guidsToWipe.Count} statuses to force network refresh.");
            pendingStatusList = nextList;
            guidsInTransition = guidsToWipe;

            // CLEARING LOGIC: Build list excluding the ones being wiped
            foreach (var s in nextList)
            {
                if (!guidsToWipe.Contains(s.GUID)) wipeList.Add(s);
            }

            Plugin.Log.Debug($"[MoodlesSync] Sending wipe-list with {wipeList.Count} statuses remaining.");
            SetStatusManagerByPtrV2(playerAddress, Pack(wipeList));
        }
        else
        {
            lastKnownGoodPack = raw;
            Plugin.Log.Verbose("[MoodlesSync] Update finished: No text changes detected.");
        }
    }

    private List<MyStatus> Unpack(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return new List<MyStatus>();
        try { return MemoryPackSerializer.Deserialize<List<MyStatus>>(Convert.FromBase64String(base64), SerializerOptions); }
        catch (Exception e)
        {
            Plugin.Log.Error($"[MoodlesSync] Unpack Error: {e.Message}");
            return new List<MyStatus>();
        }
    }

    private string Pack(List<MyStatus> list) => Convert.ToBase64String(MemoryPackSerializer.Serialize(list, SerializerOptions));

    private static readonly MemoryPackSerializerOptions SerializerOptions = new() { StringEncoding = StringEncoding.Utf16 };

    public string Parse(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, @"@gamba", match =>
        {

            if (plugin.Bank.dealer == null) return "0.00";

            return plugin.Bank.sources.TryGetValue(plugin.Bank.dealer, out var val) ? Plugin.FormatNumber(val).ToString() : "0.00";

        });
    }
}
