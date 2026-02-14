using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace PatMeMoodles.Windows;

public class MainWindow(Plugin plugin) : Window("PatMeMoodles")
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
            var headerLabel = string.IsNullOrWhiteSpace(config.Title) ? $"Moodle {i + 1} (Empty)" : config.Title;
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
                ushort id = entry.Key;
                int actualValue = entry.Value;

                // Column 1: Name
                ImGui.TableNextColumn();
                var emoteName = emoteSheet?.GetRow(id).Name.ToString() ?? "Unknown";
                ImGui.Text($"{emoteName} (${id})");

                // Column 2: Value Logic
                ImGui.TableNextColumn();

                // Initialization
                if (!lastSeenValues.ContainsKey(id)) lastSeenValues[id] = actualValue;
                if (!editBuffer.ContainsKey(id)) editBuffer[id] = actualValue;

                // LOGIC: If the value in the configuration changed (meaning the Hook updated it),
                // we overwrite the buffer regardless of whether the user is typing.
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
                    configuration.emoteCounter[id] = editBuffer[id];
                    lastSeenValues[id] = editBuffer[id]; // Update lastSeen so we don't trigger an overwrite
                    configuration.Save();
                    plugin.IPCService.UpdateMoodles();
                }

                // Column 4: Delete
                ImGui.TableNextColumn();
                if (ImGui.Button($"X##{id}"))
                {
                    configuration.emoteCounter.Remove(id);
                    editBuffer.Remove(id);
                    lastSeenValues.Remove(id);
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
                lastSeenValues.Clear();
                configuration.Save();
                plugin.IPCService.UpdateMoodles();
            }
        }
    }
}
