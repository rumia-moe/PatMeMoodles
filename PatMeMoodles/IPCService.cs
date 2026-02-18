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
        var playerAddress = Plugin.ObjectTable.LocalPlayer?.Address ?? nint.Zero;
        if (playerAddress == nint.Zero) return;

        // Check if we are already waiting for a wipe to finish
        if (guidsInTransition.Count > 0) return;

        var raw = GetStatusManagerByPtrV2(playerAddress);
        if (string.IsNullOrEmpty(raw)) return;

        var currentStatuses = Unpack(raw);
        bool needsSync = false;

        // FIX: Define the HashSet here so it exists in the whole function context
        var guidsToWipe = new HashSet<Guid>();
        var nextList = new List<MyStatus>();

        foreach (var status in currentStatuses)
        {
            // Only look for Moodles that belong to PatMe
            var config = plugin.Configuration.counterMoodles.FirstOrDefault(x => x.Id == status.GUID.ToString());

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

            // Always add the status to our 'next' list (preserves Gamba/Unmanaged moodles)
            nextList.Add(status);
        }

        if (needsSync)
        {
            pendingStatusList = nextList;
            guidsInTransition = guidsToWipe;

            // Build a list that keeps Gamba moodles but removes the PatMe ones we want to refresh
            var wipeList = nextList.Where(s => !guidsToWipe.Contains(s.GUID)).ToList();

            Plugin.Log.Info($"[PatMe] Syncing {guidsToWipe.Count} moodles. Preserving others.");
            SetStatusManagerByPtrV2(playerAddress, Pack(wipeList));
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
