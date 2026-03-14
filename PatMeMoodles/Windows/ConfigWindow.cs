using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PatMeMoodles.Windows;

public class ConfigWindow(Plugin plugin) : Window("PatMeMoodles")
{
    private readonly Configuration configuration = plugin.Configuration;

    // Buffer for what the user is currently typing
    private readonly Dictionary<ushort, int> editBuffer = new();

    // Tracks the value the UI "knows" about to detect background changes
    private readonly Dictionary<ushort, int> lastSeenValues = new();

    public override void Draw()
    {
        DrawGlobalControls();
        ImGui.Separator();
        DrawMoodleList();
        ImGui.Separator();
        DrawCounterTable();
    }

    private void DrawGlobalControls()
    {
        if (ImGui.Button("Force Update"))
        {
            plugin.MoodlesBridge.Set();
        }
        ImGui.SameLine();
        if (ImGui.Button("Add New Moodle"))
        {
            // Add a new entry with a fresh Guid and empty strings
            configuration.moodles.Add(Guid.NewGuid(), (string.Empty, string.Empty));
            configuration.Save();
        }
    }

    private void DrawMoodleList()
    {
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 1f, 1f), "Moodle Configurations");

        // Use ToList() to avoid "Collection modified" errors if we delete during iteration
        foreach (var kvp in configuration.moodles.ToList())
        {
            var guid = kvp.Key;
            var (title, description) = kvp.Value;

            ImGui.PushID(guid.ToString());

            var headerLabel = string.IsNullOrWhiteSpace(title) ? $"Moodle (New/Empty)" : title;
            if (ImGui.CollapsingHeader($"{headerLabel}###header"))
            {
                ImGui.Indent();

                ImGui.TextDisabled($"ID: {guid}");

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Title:"); ImGui.SameLine();
                if (ImGui.InputText("##title", ref title, 128))
                {
                    configuration.moodles[guid] = (title, description);
                    configuration.Save();
                }

                ImGui.Text("Description ($ID to inject counter):");
                if (ImGui.InputTextMultiline("##desc", ref description, 500, new Vector2(-1, 60)))
                {
                    configuration.moodles[guid] = (title, description);
                    configuration.Save();
                }

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.4f, 0.1f, 0.1f, 1f));
                if (ImGui.Button("Delete This Moodle"))
                {
                    configuration.moodles.Remove(guid);
                    configuration.Save();
                }
                ImGui.PopStyleColor();

                ImGui.Unindent();
                ImGui.Spacing();
            }
            ImGui.PopID();
        }
    }

    private void DrawCounterTable()
    {
        ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "Active Counters");
        var emoteSheet = Svc.Data.GetExcelSheet<Emote>();

        if (ImGui.BeginTable("##counterTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 200)))
        {
            ImGui.TableSetupColumn("Emote Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Set", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Del", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableHeadersRow();

            // Iterate through the renamed 'emotes' dictionary
            foreach (var entry in configuration.emotes.ToList())
            {
                ImGui.TableNextRow();
                ushort id = entry.Key;
                int actualValue = entry.Value;

                ImGui.TableNextColumn();
                var emoteName = emoteSheet.GetRowOrDefault(id).Value.Name.ToString() ?? "Unknown";
                ImGui.Text($"{emoteName} (${id})");

                ImGui.TableNextColumn();

                if (!lastSeenValues.ContainsKey(id)) lastSeenValues[id] = actualValue;
                if (!editBuffer.ContainsKey(id)) editBuffer[id] = actualValue;

                if (actualValue != lastSeenValues[id])
                {
                    editBuffer[id] = actualValue;
                    lastSeenValues[id] = actualValue;
                }

                int bufferVal = editBuffer[id];
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt($"##edit_{id}", ref bufferVal, 0, 0))
                {
                    editBuffer[id] = bufferVal;
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"Set##{id}"))
                {
                    configuration.emotes[id] = editBuffer[id];
                    lastSeenValues[id] = editBuffer[id];
                    configuration.Save();
                    plugin.MoodlesBridge.Set();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"X##{id}"))
                {
                    configuration.emotes.Remove(id);
                    editBuffer.Remove(id);
                    lastSeenValues.Remove(id);
                    configuration.Save();
                    plugin.MoodlesBridge.Set();
                }
            }
            ImGui.EndTable();
        }

        if (ImGui.Button("Reset All Counters"))
        {
            if (ImGui.GetIO().KeyShift)
            {
                configuration.emotes.Clear();
                editBuffer.Clear();
                lastSeenValues.Clear();
                configuration.Save();
                plugin.MoodlesBridge.Set();
            }
            else
            {
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Hold SHIFT to reset all");
            }
        }
    }
}
