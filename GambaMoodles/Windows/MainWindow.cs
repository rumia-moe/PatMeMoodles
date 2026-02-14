using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace GambaMoodles.Windows;

public class MainWindow(Plugin plugin) : Window("GambaMoodles")
{
    private readonly Configuration configuration = plugin.Configuration;

    // Buffer to hold what you're currently typing in the input boxes
    private readonly Dictionary<string, int> editBuffer = new();

    // Tracks the balance we last displayed to detect if a trade updated it in the background
    private readonly Dictionary<string, int> lastSeenBalances = new();

    // Buffer for the "Add New Player" text input
    private string newPlayerName = string.Empty;

    public override void Draw()
    {
        DrawGlobalControls();
        ImGui.Separator();

        DrawMoodleList();
        ImGui.Separator();

        DrawManualAdd();
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

                    // Initialize buffers
                    editBuffer[cleanedName] = 0;
                    lastSeenBalances[cleanedName] = 0;

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
                string name = entry.Key;
                int actualBalance = entry.Value;

                // Column 1: Name
                ImGui.TableNextColumn();
                ImGui.Text(name);

                // Column 2: Money Amount (Smart Buffer)
                ImGui.TableNextColumn();

                // 1. Initialize buffers if we've never seen this player before
                if (!lastSeenBalances.ContainsKey(name)) lastSeenBalances[name] = actualBalance;
                if (!editBuffer.ContainsKey(name)) editBuffer[name] = actualBalance;

                // 2. DETECTION: If the actual balance in the Bank changed (background trade),
                // we overwrite the edit buffer immediately so the UI shows the new total.
                if (actualBalance != lastSeenBalances[name])
                {
                    editBuffer[name] = actualBalance;
                    lastSeenBalances[name] = actualBalance;
                }

                int bufferVal = editBuffer[name];
                ImGui.SetNextItemWidth(-1);

                if (ImGui.InputInt($"##edit_{name}", ref bufferVal, 0, 0))
                {
                    editBuffer[name] = bufferVal;
                }

                // Column 3: Actions
                ImGui.TableNextColumn();
                if (ImGui.Button($"Set##{name}"))
                {
                    // Update bank and save
                    plugin.Bank.sources[name] = editBuffer[name];

                    // IMPORTANT: Update lastSeenBalance to match what we just set 
                    // so we don't trigger the "background change" logic on our own change.
                    lastSeenBalances[name] = editBuffer[name];

                    plugin.Configuration.Save();
                    plugin.IPCService.UpdateMoodles();
                }

                ImGui.SameLine();

                if (ImGui.Button($"Delete##{name}"))
                {
                    plugin.Bank.sources.Remove(name);
                    editBuffer.Remove(name);
                    lastSeenBalances.Remove(name);
                    if (plugin.Bank.dealer == name) plugin.Bank.dealer = null;
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
                lastSeenBalances.Clear();
                plugin.Bank.dealer = null;
                plugin.Configuration.Save();
                plugin.IPCService.UpdateMoodles();
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Hold SHIFT to clear everything.");
    }
}
