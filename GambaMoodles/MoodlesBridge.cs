using ECommons;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using MemoryPack;
using Moodles.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GambaMoodles;

public class MoodlesBridge : IDisposable
{

    private static readonly MemoryPackSerializerOptions SerializerOptions = new()
    {
        StringEncoding = StringEncoding.Utf16,
    };

    [EzIPC] private readonly Func<nint, string> GetStatusManagerByPtrV2;
    [EzIPC] private readonly Action<nint> ClearStatusManagerByPtrV2;
    [EzIPC] private readonly Action<nint, string> SetStatusManagerByPtrV2;

    [EzIPCEvent]
    private void StatusManagerModified(nint characterPointer)
    {
        if (Player.Object == null)
            return;
        if (characterPointer != Player.Object.Address)
            return;

        Set();

    }

    private Configuration config { get; init; }
    private string base64 = "";

    private Bank bank{ get; init; }

    public MoodlesBridge(Configuration config, Bank bank)
    {
        this.config = config;
        this.bank = bank;
        EzIPC.Init(this, "Moodles");

        Svc.ClientState.TerritoryChanged += OnZoneChange;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= WaitUntilReady;
        Svc.ClientState.TerritoryChanged -= OnZoneChange;
    }

    private void OnZoneChange(ushort territoryId)
    {
        Svc.Framework.Update += WaitUntilReady;
    }

    private void WaitUntilReady(IFramework framework)
    {
        if (Player.Object == null) return;

        if (Svc.Condition[ConditionFlag.BetweenAreas] ||
            Svc.Condition[ConditionFlag.BetweenAreas51])
        {
            return;
        }

        Svc.Framework.Update -= WaitUntilReady;

        Svc.Framework.RunOnFrameworkThread(() => SetStatusManagerByPtrV2(Player.Object.Address, base64)).ConfigureAwait(false);
    }

    private (List<MyStatus>, string) Get()
    {
        if (Player.Object == null)
            return ([], "");

        var base64 = GetStatusManagerByPtrV2(Player.Object.Address);

        return (MemoryPackSerializer.Deserialize<List<MyStatus>>(Convert.FromBase64String(base64), SerializerOptions) ?? [], base64);
    }
    private string Parse(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, @"@gamba", match =>
        {

            if (bank.dealer == null) return "0.00";

            return Plugin.FormatNumber(bank.sources.TryGetValue(bank.dealer, out var val) ? val : 0);

        });
    }

    public void Set(bool force = false)
    {

        Svc.Framework.RunOnFrameworkThread(() =>
        {
            if (Player.Object == null)
            return;

            var (statuses, base64Old) = Get();

            for (var i = 0; i < statuses.Count; i++)
            {
                if (!config.moodles.TryGetFirst(x => x.Id == statuses[i].GUID.ToString(), out var moodle))
                    continue;

                statuses[i].Title = Parse(moodle.Title);
                statuses[i].Description = Parse(moodle.Description);
            }

            base64 = Convert.ToBase64String(MemoryPackSerializer.Serialize(statuses, SerializerOptions));

            if (base64 == base64Old && !force)
            {
                return;
            }

            ClearStatusManagerByPtrV2(Player.Object.Address);
            Svc.Framework.RunOnFrameworkThread(() => SetStatusManagerByPtrV2(Player.Object.Address, base64)).ConfigureAwait(false);

        }).ConfigureAwait(false);
    }

}
