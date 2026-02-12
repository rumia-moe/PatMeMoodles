using ECommons.EzIpcManager;
using MemoryPack;
using Moodles.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;

namespace PatMeMoodles;

public class IPCService : IDisposable
{
    private Plugin plugin;
    private List<MyStatus> pendingUpdate = null;
    private HashSet<string> guidsWaitingForWipe = new();

    // Stores the last data we successfully set, so we can force it after zoning
    private string lastAppliedData = string.Empty;

    [EzIPC] private readonly Func<nint, string> GetStatusManagerByPtrV2;
    [EzIPC] private readonly Action<nint, string> SetStatusManagerByPtrV2;

    public IPCService(Plugin plugin)
    {
        this.plugin = plugin;
        EzIPC.Init(this, "Moodles");

        // Use ConditionChange as the universal "Transition Ended" trigger
        Plugin.Condition.ConditionChange += OnConditionChange;
    }

    public void Dispose()
    {
        Plugin.Condition.ConditionChange -= OnConditionChange;
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        // When any loading/zoning/occupancy flag turns OFF, it's our cue
        if ((flag == ConditionFlag.BetweenAreas ||
             flag == ConditionFlag.BetweenAreas51 ||
             flag == ConditionFlag.OccupiedInCutSceneEvent) && value == false)
        {
            Plugin.Log.Debug($"Transition '{flag}' ended. Forcing re-sync.");

            // If we have a known good state, push it immediately to overcome Moodles' silence
            if (!string.IsNullOrEmpty(lastAppliedData))
            {
                var playerAddress = Plugin.ObjectTable.LocalPlayer?.Address ?? nint.Zero;
                if (playerAddress != nint.Zero)
                {
                    SetStatusManagerByPtrV2(playerAddress, lastAppliedData);
                }
            }

            // Then run a standard update to ensure counters are current
            UpdateMoodles();
        }
    }

    private static readonly MemoryPackSerializerOptions SerializerOptions = new() { StringEncoding = StringEncoding.Utf16 };

    [EzIPCEvent]
    private void Ready() => UpdateMoodles();

    [EzIPCEvent]
    private void StatusManagerModified(nint charaPtr)
    {
        if (charaPtr != Plugin.ObjectTable.LocalPlayer?.Address) return;

        // Surgical re-application logic
        if (pendingUpdate != null && guidsWaitingForWipe.Count > 0)
        {
            var currentData = GetStatusManagerByPtrV2(charaPtr);
            if (string.IsNullOrEmpty(currentData)) return;

            var currentStatuses = MemoryPackSerializer.Deserialize<List<MyStatus>>(Convert.FromBase64String(currentData), SerializerOptions);

            if (!currentStatuses.Any(x => guidsWaitingForWipe.Contains(x.GUID.ToString())))
            {
                var finalData = Convert.ToBase64String(MemoryPackSerializer.Serialize(pendingUpdate));

                pendingUpdate = null;
                guidsWaitingForWipe.Clear();

                lastAppliedData = finalData; // Remember this for next zone change
                SetStatusManagerByPtrV2(charaPtr, finalData);
            }
            return;
        }

        UpdateMoodles();
    }

    public void UpdateMoodles()
    {
        var playerAddress = Plugin.ObjectTable.LocalPlayer?.Address ?? nint.Zero;
        if (playerAddress == nint.Zero) return;

        var raw = GetStatusManagerByPtrV2(playerAddress);
        if (string.IsNullOrEmpty(raw)) return;

        var currentStatuses = MemoryPackSerializer.Deserialize<List<MyStatus>>(Convert.FromBase64String(raw), SerializerOptions);
        if (currentStatuses.Count == 0 && guidsWaitingForWipe.Count == 0) return;

        bool needsUpdate = false;
        var processedList = new List<MyStatus>();
        var wipeList = new List<MyStatus>();
        var currentlyChangingGuids = new HashSet<string>();

        foreach (var status in currentStatuses)
        {
            var statusGuidStr = status.GUID.ToString();
            var config = plugin.Configuration.counterMoodles.FirstOrDefault(x => x.Id == statusGuidStr);

            if (config != null)
            {
                var pTitle = Parse(config.Title);
                var pDesc = Parse(config.Description);

                if (status.Title != pTitle || status.Description != pDesc)
                {
                    status.Title = pTitle;
                    status.Description = pDesc;
                    needsUpdate = true;
                    currentlyChangingGuids.Add(statusGuidStr);
                    processedList.Add(status);
                }
                else
                {
                    processedList.Add(status);
                    wipeList.Add(status);
                }
            }
            else
            {
                processedList.Add(status);
                wipeList.Add(status);
            }
        }

        if (needsUpdate)
        {
            if (wipeList.Count == 0 && currentStatuses.Count > 0 && currentlyChangingGuids.Count != currentStatuses.Count) return;

            pendingUpdate = processedList;
            guidsWaitingForWipe = currentlyChangingGuids;

            // We don't update lastAppliedData here because we are in the "Wipe" phase
            SetStatusManagerByPtrV2(playerAddress, Convert.ToBase64String(MemoryPackSerializer.Serialize(wipeList)));
        }
        else
        {
            // If everything is already correct, just save the current state as our recovery point
            lastAppliedData = raw;
        }
    }

    public string Parse(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, @"\$(\d+)", match =>
        {
            if (ushort.TryParse(match.Groups[1].Value, out var id))
            {
                return plugin.Configuration.emoteCounter.TryGetValue(id, out var val) ? val.ToString() : "0";
            }
            return match.Value;
        });
    }
}
