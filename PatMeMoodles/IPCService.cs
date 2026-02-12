using ECommons.EzIpcManager;
using MemoryPack;
using Moodles.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PatMeMoodles;

public class IPCService
{
    private Plugin plugin;
    // Stores the final version to re-apply
    private List<MyStatus> pendingUpdate = null;
    // Tracks specifically which GUIDs are currently in the "Wipe" phase
    private HashSet<string> guidsWaitingForWipe = new();

    [EzIPC] private readonly Func<nint, string> GetStatusManagerByPtrV2;
    [EzIPC] private readonly Action<nint, string> SetStatusManagerByPtrV2;

    public IPCService(Plugin plugin)
    {
        this.plugin = plugin;
        EzIPC.Init(this, "Moodles");
    }

    private static readonly MemoryPackSerializerOptions SerializerOptions = new() { StringEncoding = StringEncoding.Utf16 };

    [EzIPCEvent]
    private void Ready() => UpdateMoodles();

    [EzIPCEvent]
    private void StatusManagerModified(nint charaPtr)
    {
        if (charaPtr != Plugin.ObjectTable.LocalPlayer?.Address) return;

        // STATE 2: Check if the surgical wipe for specific GUIDs is finished
        if (pendingUpdate != null && guidsWaitingForWipe.Count > 0)
        {
            var currentData = GetStatusManagerByPtrV2(charaPtr);
            var currentStatuses = string.IsNullOrEmpty(currentData)
                ? new List<MyStatus>()
                : MemoryPackSerializer.Deserialize<List<MyStatus>>(Convert.FromBase64String(currentData), SerializerOptions);

            // Verify that NONE of the specific GUIDs we are resetting are present
            if (!currentStatuses.Any(x => guidsWaitingForWipe.Contains(x.GUID.ToString())))
            {
                Plugin.Log.Info($"Surgical wipe confirmed for: {string.Join(", ", guidsWaitingForWipe)}. Re-applying.");

                var finalData = Convert.ToBase64String(MemoryPackSerializer.Serialize(pendingUpdate));

                // Clear states
                pendingUpdate = null;
                guidsWaitingForWipe.Clear();

                SetStatusManagerByPtrV2(charaPtr, finalData);
            }
            return;
        }

        UpdateMoodles();
    }

    public void UpdateMoodles()
    {
        var playerAddress = Plugin.ObjectTable.LocalPlayer?.Address ?? nint.Zero;
        var raw = GetStatusManagerByPtrV2(playerAddress);
        if (string.IsNullOrEmpty(raw)) return;

        var currentStatuses = MemoryPackSerializer.Deserialize<List<MyStatus>>(Convert.FromBase64String(raw), SerializerOptions);

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
                var parsedTitle = Parse(config.Title);
                var parsedDesc = Parse(config.Description);

                // Check if this specific Moodle needs an update
                if (status.Title != parsedTitle || status.Description != parsedDesc)
                {
                    status.Title = parsedTitle;
                    status.Description = parsedDesc;

                    needsUpdate = true;
                    currentlyChangingGuids.Add(statusGuidStr);

                    // Do NOT add to wipeList (this is the surgical removal)
                    processedList.Add(status);
                }
                else
                {
                    // Tracked but NO change: keep it in both lists so it doesn't blink
                    processedList.Add(status);
                    wipeList.Add(status);
                }
            }
            else
            {
                // Unrelated Moodle: Keep in both
                processedList.Add(status);
                wipeList.Add(status);
            }
        }

        if (needsUpdate)
        {
            Plugin.Log.Info($"Surgically resetting {currentlyChangingGuids.Count} Moodles.");

            pendingUpdate = processedList;
            guidsWaitingForWipe = currentlyChangingGuids;

            // Step 1: Send the list with only the CHANGED Moodles removed
            SetStatusManagerByPtrV2(playerAddress, Convert.ToBase64String(MemoryPackSerializer.Serialize(wipeList)));
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
