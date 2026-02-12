using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace PatMeMoodles.Windows;

public class MainWindow(Plugin plugin) : Window("PatMeMoodles")
{
    private readonly Configuration configuration = plugin.Configuration;

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

            // Create a unique header label
            var headerLabel = string.IsNullOrWhiteSpace(config.Title)
                ? $"Moodle {i + 1} (Empty)"
                : config.Title;

            if (ImGui.CollapsingHeader($"{headerLabel}###header"))
            {
                ImGui.Indent();

                // ID Input
                ImGui.AlignTextToFramePadding();
                ImGui.Text("GUID:"); ImGui.SameLine();
                if (ImGui.InputText("##id", ref config.Id, 128)) configuration.Save();

                // Title Input
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Title:"); ImGui.SameLine();
                if (ImGui.InputText("##title", ref config.Title, 128)) configuration.Save();

                // Description Input
                ImGui.Text("Description ($ID to inject counter):");
                if (ImGui.InputTextMultiline("##desc", ref config.Description, 500, new Vector2(-1, 60)))
                    configuration.Save();

                // Delete Button for this specific Moodle
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

        if (ImGui.BeginTable("##counterTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 200)))
        {
            ImGui.TableSetupColumn("Emote Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            // Use ToList to avoid "collection modified" errors if we remove during loop
            foreach (var entry in configuration.emoteCounter.ToList())
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var emoteName = emoteSheet?.GetRow(entry.Key).Name.ToString() ?? "Unknown";
                ImGui.Text($"{emoteName} (${entry.Key})");

                ImGui.TableNextColumn();
                ImGui.Text(entry.Value.ToString());

                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"Reset##{entry.Key}"))
                {
                    configuration.emoteCounter.Remove(entry.Key);
                    configuration.Save();
                    plugin.IPCService.UpdateMoodles();
                }
            }
            ImGui.EndTable();
        }

        if (ImGui.Button("Reset All Counters"))
        {
            configuration.emoteCounter.Clear();
            configuration.Save();
            plugin.IPCService.UpdateMoodles();
        }
    }
}
