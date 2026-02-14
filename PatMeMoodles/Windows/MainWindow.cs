using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace PatMeMoodles.Windows;

public class MainWindow(Plugin plugin) : Window("PatMeMoodles")
{
    private readonly Configuration configuration = plugin.Configuration;

    // The buffer to store typing changes without immediate saving
    private readonly Dictionary<ushort, int> editBuffer = new();

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
            plugin.IPCService.UpdateMoodles();
        }

        ImGui.SameLine();

        if (ImGui.Button("New"))
        {
            configuration.counterMoodles.Add(new CounterMoodle());
            configuration.Save();
        }
    }

    private void DrawMoodleList()
    {
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 1f, 1f), "Moodle Configurations");

        for (var i = 0; i < configuration.counterMoodles.Count; i++)
        {
            var config = configuration.counterMoodles[i];
            ImGui.PushID($"counterMoodle_{i}");

            var headerLabel = string.IsNullOrWhiteSpace(config.Title)
                ? $"Moodle {i + 1} (Empty)"
                : config.Title;

            if (ImGui.CollapsingHeader($"{headerLabel}###header"))
            {
                ImGui.Indent();

                ImGui.AlignTextToFramePadding();
                ImGui.Text("GUID:"); ImGui.SameLine();
                if (ImGui.InputText("##id", ref config.Id, 128)) configuration.Save();

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Title:"); ImGui.SameLine();
                if (ImGui.InputText("##title", ref config.Title, 128)) configuration.Save();

                ImGui.Text("Description ($ID to inject counter):");
                if (ImGui.InputTextMultiline("##desc", ref config.Description, 500, new Vector2(-1, 60)))
                    configuration.Save();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.4f, 0.1f, 0.1f, 1f));
                if (ImGui.Button("Delete This Moodle"))
                {
                    configuration.counterMoodles.RemoveAt(i);
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

        var emoteSheet = Plugin.DataManager.GetExcelSheet<Emote>();

        // Increased table columns to 4 to accommodate the 'Set' button
        if (ImGui.BeginTable("##counterTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 200)))
        {
            ImGui.TableSetupColumn("Emote Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Set", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Del", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableHeadersRow();

            foreach (var entry in configuration.emoteCounter.ToList())
            {
                ImGui.TableNextRow();

                // Column 1: Name
                ImGui.TableNextColumn();
                var emoteName = emoteSheet?.GetRow(entry.Key).Name.ToString() ?? "Unknown";
                ImGui.Text($"{emoteName} (${entry.Key})");

                // Column 2: Value (Buffered Input)
                ImGui.TableNextColumn();

                // Initialize buffer with actual value if not present
                if (!editBuffer.ContainsKey(entry.Key))
                    editBuffer[entry.Key] = entry.Value;

                int val = editBuffer[entry.Key];
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt($"##edit_{entry.Key}", ref val, 0, 0))
                {
                    editBuffer[entry.Key] = val;
                }

                // Column 3: Set Button
                ImGui.TableNextColumn();
                if (ImGui.Button($"Set##{entry.Key}"))
                {
                    configuration.emoteCounter[entry.Key] = editBuffer[entry.Key];
                    configuration.Save();
                    plugin.IPCService.UpdateMoodles();
                }

                // Column 4: Delete Button
                ImGui.TableNextColumn();
                if (ImGui.Button($"X##{entry.Key}"))
                {
                    configuration.emoteCounter.Remove(entry.Key);
                    editBuffer.Remove(entry.Key);
                    configuration.Save();
                    plugin.IPCService.UpdateMoodles();
                }
            }
            ImGui.EndTable();
        }

        if (ImGui.Button("Reset All Counters"))
        {
            if (ImGui.GetIO().KeyShift)
            {
                configuration.emoteCounter.Clear();
                editBuffer.Clear();
                configuration.Save();
                plugin.IPCService.UpdateMoodles();
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Hold SHIFT to clear everything.");
    }
}
