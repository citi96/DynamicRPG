using System;
using System.Collections.Generic;
using System.Text;
using DynamicRPG.Systems.Combat;
using Godot;

#nullable enable

namespace DynamicRPG.UI;

/// <summary>
/// Provides a combat heads-up display (HUD) for showing combat log messages and player stats.
/// </summary>
public sealed partial class HUD : Control
{
    private RichTextLabel? _combatLog;
    private Label? _hpLabel;
    private Label? _manaLabel;
    private Label? _turnLabel;
    private Label? _statusEffectsLabel;
    private Control? _turnPanel;

    public ActionMenu? ActionMenu { get; private set; }

    public override void _Ready()
    {
        _combatLog = GetNodeOrNull<RichTextLabel>("LogPanel/CombatLog");
        ActionMenu = GetNodeOrNull<ActionMenu>("ActionPanel");

        // Ottieni riferimenti ai nuovi pannelli
        _hpLabel = GetNodeOrNull<Label>("StatusPanel/StatsContainer/HPLabel");
        _manaLabel = GetNodeOrNull<Label>("StatusPanel/StatsContainer/ManaLabel");
        _turnPanel = GetNodeOrNull<Control>("TurnPanel");
        _turnLabel = _turnPanel?.GetNodeOrNull<Label>("TurnLabel");
        _statusEffectsLabel = GetNodeOrNull<Label>("StatusPanel/StatsContainer/StatusEffectsLabel");

        ClearTurnIndicator();
    }

    /// <summary>
    /// Appends a message to the combat log and keeps the scroll position at the bottom.
    /// Falls back to <see cref="GD.Print(object)"/> when the UI label is not available.
    /// </summary>
    /// <param name="text">Message to append.</param>
    public void AddLogMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_combatLog is null)
        {
            GD.Print(text);
            return;
        }

        _combatLog.AppendText(text + "\n");
        _combatLog.ScrollToLine(Math.Max(0, _combatLog.GetLineCount() - 1));
    }

    /// <summary>
    /// Clears all entries from the combat log.
    /// </summary>
    public void ClearLog()
    {
        if (_combatLog is null)
        {
            return;
        }

        _combatLog.Clear();
    }

    /// <summary>
    /// Updates the player stats display (HP and Mana).
    /// </summary>
    public void UpdatePlayerStats(int currentHP, int maxHP, int currentMana, int maxMana)
    {
        // TODO: Add visual feedback (e.g., flashing text) when the player takes damage.
        if (_hpLabel is not null)
        {
            _hpLabel.Text = $"HP: {currentHP}/{maxHP}";
        }

        if (_manaLabel is not null)
        {
            _manaLabel.Text = $"Mana: {currentMana}/{maxMana}";
        }

        if (Game.Instance?.Player is { StatusEffects: var statusEffects })
        {
            UpdateStatusEffects(statusEffects);
        }
    }

    /// <summary>
    /// Updates the turn indicator showing whose turn it is.
    /// </summary>
    public void UpdateTurnIndicator(string characterName, int roundNumber)
    {
        if (_turnLabel is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(characterName))
        {
            characterName = "Sconosciuto";
        }

        _turnLabel.Text = $"Turno di: {characterName} (Round {Math.Max(1, roundNumber)})";
        _turnLabel.Visible = true;
        _turnPanel?.Show();
    }

    /// <summary>
    /// Clears the turn indicator text and hides the panel from the HUD.
    /// </summary>
    public void ClearTurnIndicator()
    {
        if (_turnLabel is not null)
        {
            _turnLabel.Text = string.Empty;
            _turnLabel.Visible = false;
        }

        _turnPanel?.Hide();
    }

    /// <summary>
    /// Updates the status effects display for the player.
    /// </summary>
    public void UpdateStatusEffects(IEnumerable<StatusEffect>? statusEffects)
    {
        if (_statusEffectsLabel is null)
        {
            return;
        }

        _statusEffectsLabel.Text = FormatStatusEffectsText(statusEffects);
    }

    private static string FormatStatusEffectsText(IEnumerable<StatusEffect>? statusEffects)
    {
        if (statusEffects is null)
        {
            return "Nessun effetto";
        }

        var builder = new StringBuilder();
        var hasEffects = false;

        foreach (var effect in statusEffects)
        {
            if (effect is null)
            {
                continue;
            }

            hasEffects = true;
            builder.Append(effect.Type);

            if (effect.RemainingDuration > 0)
            {
                builder.Append('(');
                builder.Append(effect.RemainingDuration);
                builder.Append(')');
            }

            builder.Append(' ');
        }

        if (!hasEffects)
        {
            return "Nessun effetto";
        }

        return builder.ToString().TrimEnd();
    }
}
