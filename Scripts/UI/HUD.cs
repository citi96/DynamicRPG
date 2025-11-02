using System;
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

    public ActionMenu? ActionMenu { get; private set; }

    public override void _Ready()
    {
        _combatLog = GetNodeOrNull<RichTextLabel>("LogPanel/CombatLog");
        ActionMenu = GetNodeOrNull<ActionMenu>("ActionPanel");

        // Ottieni riferimenti ai nuovi pannelli
        _hpLabel = GetNodeOrNull<Label>("StatusPanel/HPLabel");
        _manaLabel = GetNodeOrNull<Label>("StatusPanel/ManaLabel");
        _turnLabel = GetNodeOrNull<Label>("TurnPanel/TurnLabel");
        _statusEffectsLabel = GetNodeOrNull<Label>("StatusPanel/StatusEffectsLabel");
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
        if (_hpLabel is not null)
        {
            _hpLabel.Text = $"HP: {currentHP}/{maxHP}";
        }

        if (_manaLabel is not null)
        {
            _manaLabel.Text = $"Mana: {currentMana}/{maxMana}";
        }
    }

    /// <summary>
    /// Updates the turn indicator showing whose turn it is.
    /// </summary>
    public void UpdateTurnIndicator(string characterName, int roundNumber)
    {
        if (_turnLabel is not null)
        {
            _turnLabel.Text = $"Turno di: {characterName} (Round {roundNumber})";
        }
    }

    /// <summary>
    /// Updates the status effects display for the player.
    /// </summary>
    public void UpdateStatusEffects(string statusText)
    {
        if (_statusEffectsLabel is not null)
        {
            _statusEffectsLabel.Text = string.IsNullOrWhiteSpace(statusText)
                ? "Nessun effetto"
                : statusText;
        }
    }
}