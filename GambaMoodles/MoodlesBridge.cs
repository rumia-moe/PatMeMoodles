using ECommons;
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

public class MoodlesBridge
{

    private static readonly MemoryPackSerializerOptions SerializerOptions = new()
    {
        StringEncoding = StringEncoding.Utf16,
    };

    [EzIPC] private readonly Func<nint, string> GetStatusManagerByPtrV2;
    [EzIPC] private readonly Action<nint> ClearStatusManagerByPtrV2;
    [EzIPC] private readonly Action<nint, string> SetStatusManagerByPtrV2;

    private string base64;

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
    private Bank bank{ get; init; }

    public MoodlesBridge(Configuration config, Bank bank)
    {
        this.config = config;
        this.bank = bank;
        EzIPC.Init(this, "Moodles");
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

    public void Set()
    {

        Svc.Framework.RunOnFrameworkThread(() =>
        {
            if (Player.Object == null)
            return;

            var (statuses, _) = Get();

            for (var i = 0; i < statuses.Count; i++)
            {
                if (!config.moodles.TryGetFirst(x => x.Id == statuses[i].GUID.ToString(), out var moodle))
                    continue;

                statuses[i].Title = Parse(moodle.Title);
                statuses[i].Description = Parse(moodle.Description);
            }

            var base64New = Convert.ToBase64String(MemoryPackSerializer.Serialize(statuses, SerializerOptions));

            if (base64New == base64)
            {
                return;
            }

            base64 = base64New;

            ClearStatusManagerByPtrV2(Player.Object.Address);
            Svc.Framework.RunOnFrameworkThread(() => SetStatusManagerByPtrV2(Player.Object.Address, base64New));

        }).ConfigureAwait(false);
    }

}
