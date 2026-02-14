using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace GambaMoodles.Windows;

public class MainWindow(Plugin plugin) : Window("GambaMoodles")
{
    private readonly Configuration configuration = plugin.Configuration;

    // Buffer to hold what you're typing in the table
    private readonly Dictionary<string, int> editBuffer = new();

    // Buffer for the "Add New Player" text input
    private string newPlayerName = string.Empty;

    public override void Draw()
    {
        DrawGlobalControls();
        ImGui.Separator();

        DrawMoodleList();
        ImGui.Separator();

        DrawManualAdd(); // New Section
        ImGui.Spacing();

        DrawCounterTable();
    }

    private void DrawGlobalControls()
    {
        if (ImGui.Button("Force Update"))
        {
            plugin.IPCService.UpdateMoodles();
        }

        ImGui.SameLine();

        if (ImGui.Button("New Moodle"))
        {
            configuration.moodles.Add(new GambaMoodle());
            configuration.Save();
        }

        ImGui.Spacing();

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Active Dealer:");
        ImGui.SameLine();

        string preview = plugin.Bank.dealer ?? "None Selected";

        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("##DealerSelect", preview))
        {
            if (ImGui.Selectable("None", plugin.Bank.dealer == null))
            {
                plugin.Bank.dealer = null;
                plugin.IPCService.UpdateMoodles();
            }

            foreach (var playerName in plugin.Bank.sources.Keys)
            {
                bool isSelected = plugin.Bank.dealer == playerName;
                if (ImGui.Selectable($"{playerName}##{playerName}", isSelected))
                {
                    plugin.Bank.dealer = playerName;
                    plugin.IPCService.UpdateMoodles();
                }
            }
            ImGui.EndCombo();
        }
    }

    private void DrawManualAdd()
    {
        ImGui.TextColored(new Vector4(1f, 0.8f, 0.4f, 1f), "Add Player Manually");

        ImGui.SetNextItemWidth(250f);
        ImGui.InputTextWithHint("##NewPlayerInput", "Enter Character Name...", ref newPlayerName, 100);

        ImGui.SameLine();

        if (ImGui.Button("Add Player"))
        {
            if (!string.IsNullOrWhiteSpace(newPlayerName))
            {
                string cleanedName = newPlayerName.Trim();
                if (!plugin.Bank.sources.ContainsKey(cleanedName))
                {
                    // Add to bank with 0 balance
                    plugin.Bank.sources[cleanedName] = 0;
                    // Initialize the edit buffer so it shows up in the table correctly
                    editBuffer[cleanedName] = 0;

                    plugin.Configuration.Save();
                    newPlayerName = string.Empty; // Clear input
                    Plugin.Log.Info($"Manually added player: {cleanedName}");
                }
            }
        }
    }

    private void DrawMoodleList()
    {
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 1f, 1f), "Moodle Configurations");

        for (var i = 0; i < configuration.moodles.Count; i++)
        {
            var config = configuration.moodles[i];
            ImGui.PushID($"gambaMoodle_{i}");

            var headerLabel = string.IsNullOrWhiteSpace(config.Title) ? $"Moodle {i + 1}" : config.Title;

            if (ImGui.CollapsingHeader($"{headerLabel}###header"))
            {
                ImGui.Indent();
                ImGui.Text("GUID:"); ImGui.SameLine();
                if (ImGui.InputText("##id", ref config.Id, 128)) configuration.Save();

                ImGui.Text("Title:"); ImGui.SameLine();
                if (ImGui.InputText("##title", ref config.Title, 128)) configuration.Save();

                ImGui.Text("Description (@gamba to inject counter):");
                if (ImGui.InputTextMultiline("##desc", ref config.Description, 500, new Vector2(-1, 60)))
                    configuration.Save();

                if (ImGui.Button("Delete This Moodle"))
                {
                    configuration.moodles.RemoveAt(i);
                    configuration.Save();
                }
                ImGui.Unindent();
            }
            ImGui.PopID();
        }
    }

    private void DrawCounterTable()
    {
        ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "Player Trade Records");

        if (ImGui.BeginTable("##tradeTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 250)))
        {
            ImGui.TableSetupColumn("Player Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Money Amount", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableHeadersRow();

            foreach (var entry in plugin.Bank.sources.ToList())
            {
                ImGui.TableNextRow();

                // Column 1: Name
                ImGui.TableNextColumn();
                ImGui.Text(entry.Key);

                // Column 2: Money Amount (Buffered)
                ImGui.TableNextColumn();

                // Only initialize if we've NEVER seen this player in the buffer this session
                if (!editBuffer.ContainsKey(entry.Key))
                {
                    editBuffer[entry.Key] = entry.Value;
                }

                int val = editBuffer[entry.Key];
                ImGui.SetNextItemWidth(-1);

                if (ImGui.InputInt($"##edit_{entry.Key}", ref val, 0, 0))
                {
                    editBuffer[entry.Key] = val;
                }

                // Column 3: Actions
                ImGui.TableNextColumn();
                if (ImGui.Button($"Set##{entry.Key}"))
                {
                    plugin.Bank.sources[entry.Key] = editBuffer[entry.Key];
                    plugin.Configuration.Save();
                    plugin.IPCService.UpdateMoodles();
                }

                ImGui.SameLine();

                if (ImGui.Button($"Delete##{entry.Key}"))
                {
                    plugin.Bank.sources.Remove(entry.Key);
                    editBuffer.Remove(entry.Key);
                    if (plugin.Bank.dealer == entry.Key) plugin.Bank.dealer = null;
                    plugin.Configuration.Save();
                    plugin.IPCService.UpdateMoodles();
                }
            }
            ImGui.EndTable();
        }

        if (ImGui.Button("Clear All Records"))
        {
            if (ImGui.GetIO().KeyShift)
            {
                plugin.Bank.sources.Clear();
                editBuffer.Clear();
                plugin.Bank.dealer = null;
                plugin.Configuration.Save();
                plugin.IPCService.UpdateMoodles();
            }
        }
    }

    // Add this method inside the MainWindow class
    public void UpdateEditBuffer(string name, int newValue)
    {
        // This forces the UI to show the new trade value, even if you were typing
        editBuffer[name] = newValue;
    }
}
