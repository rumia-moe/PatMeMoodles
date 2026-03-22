using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PatMeMoodles.Windows;

public class ConfigWindow(Plugin plugin) : Window("PatMeMoodles")
{
    private readonly Configuration configuration = plugin.Configuration;

    // Buffers for Moodle creation
    private string newIdBuffer = string.Empty;

    // Buffers for Counter editing
    private readonly Dictionary<ushort, int> editBuffer = new();
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
        if (ImGui.Button("Force Update Moodles"))
        {
            plugin.MoodlesBridge.Set(true);
        }
    }

    private void DrawMoodleList()
    {
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 1f, 1f), "Moodle Configurations");

        // --- Add New Moodle Section ---
        ImGui.SetNextItemWidth(300);
        ImGui.InputTextWithHint("##new_guid", "Enter GUID for new Moodle...", ref newIdBuffer, 128);
        ImGui.SameLine();

        bool isValidGuid = Guid.TryParse(newIdBuffer, out var newGuid);
        if (!isValidGuid) ImGui.BeginDisabled();

        if (ImGui.Button("Add New"))
        {
            if (!configuration.moodles.ContainsKey(newGuid))
            {
                configuration.moodles.Add(newGuid, ("New Moodle", "Description with $ID"));
                configuration.Save();
                newIdBuffer = string.Empty;
            }
        }

        if (!isValidGuid)
        {
            ImGui.EndDisabled();
            if (!string.IsNullOrWhiteSpace(newIdBuffer))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Invalid GUID");
            }
        }

        ImGui.Spacing();

        // --- Existing Moodles ---
        foreach (var kvp in configuration.moodles.ToList())
        {
            var guid = kvp.Key;
            var (title, description) = kvp.Value;

            ImGui.PushID(guid.ToString());

            var headerLabel = string.IsNullOrWhiteSpace(title) ? $"{guid}" : title;
            if (ImGui.CollapsingHeader($"{headerLabel}###header"))
            {
                ImGui.Indent();

                ImGui.TextDisabled($"GUID: {guid}");

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
                if (ImGui.Button("Delete Moodle Entry"))
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

            foreach (var entry in configuration.emotes.ToList())
            {
                ImGui.TableNextRow();
                ushort id = entry.Key;
                int actualValue = entry.Value;

                // Column 1: Name
                ImGui.TableNextColumn();
                var emoteName = emoteSheet.GetRowOrDefault(id)?.Name ?? "Unknown";
                ImGui.Text($"{emoteName} (${id})");

                // Column 2: Value Logic
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

                // Column 3: Set
                ImGui.TableNextColumn();
                if (ImGui.Button($"Set##{id}"))
                {
                    configuration.emotes[id] = editBuffer[id];
                    lastSeenValues[id] = editBuffer[id];
                    configuration.Save();
                    plugin.MoodlesBridge.Set();
                }

                // Column 4: Delete
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
        }
        if (ImGui.IsItemHovered() && !ImGui.GetIO().KeyShift)
        {
            ImGui.SetTooltip("Hold SHIFT to reset all");
        }
    }
}
