using System;
using Godot;

#nullable enable

namespace DynamicRPG.UI;

/// <summary>
/// Provides a combat heads-up display (HUD) for showing combat log messages.
/// </summary>
public sealed partial class HUD : Control
{
    private RichTextLabel? _combatLog;

    public ActionMenu? ActionMenu { get; private set; }

    public override void _Ready()
    {
        _combatLog = GetNodeOrNull<RichTextLabel>("LogPanel/CombatLog");
        ActionMenu = GetNodeOrNull<ActionMenu>("ActionPanel");
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
}
