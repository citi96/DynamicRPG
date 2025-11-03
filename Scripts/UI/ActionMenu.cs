using Godot;
using DynamicRPG.Systems.Combat;

#nullable enable

namespace DynamicRPG.UI;

/// <summary>
/// Presents the available player actions during a combat turn.
/// </summary>
public sealed partial class ActionMenu : Panel
{
    private Button? _moveButton;
    private Button? _attackButton;
    private Button? _endTurnButton;

    public override void _Ready()
    {
        _moveButton = GetNodeOrNull<Button>("VBoxContainer/MoveButton");
        _attackButton = GetNodeOrNull<Button>("VBoxContainer/AttackButton");
        _endTurnButton = GetNodeOrNull<Button>("VBoxContainer/EndTurnButton");

        HideMenu();
        ConnectButtonSignals();
    }

    private void ConnectButtonSignals()
    {
        if (_moveButton is not null)
        {
            _moveButton.Pressed += OnMovePressed;
        }

        if (_attackButton is not null)
        {
            _attackButton.Pressed += OnAttackPressed;
        }

        if (_endTurnButton is not null)
        {
            _endTurnButton.Pressed += OnEndTurnPressed;
        }
    }

    /// <summary>
    /// Shows the action menu and enables interaction with the buttons.
    /// </summary>
    public void ShowMenu()
    {
        Visible = true;
        SetMenuEnabled(true);
    }

    /// <summary>
    /// Hides the action menu while keeping its state ready for the next activation.
    /// </summary>
    public void HideMenu()
    {
        Visible = false;
        SetMenuEnabled(true);
    }

    /// <summary>
    /// Enables or disables all action buttons and updates the mouse filter accordingly.
    /// </summary>
    /// <param name="enabled">Whether the menu should be interactable.</param>
    public void SetMenuEnabled(bool enabled)
    {
        MouseFilter = enabled ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;

        if (_moveButton is not null)
        {
            _moveButton.Disabled = !enabled;
        }

        if (_attackButton is not null)
        {
            _attackButton.Disabled = !enabled;
        }

        if (_endTurnButton is not null)
        {
            _endTurnButton.Disabled = !enabled;
        }
    }

    private static CombatManager? GetCombatManager() => CombatManager.Instance ?? Game.Instance?.CombatManager;

    private void DisableMenuForAction()
    {
        SetMenuEnabled(false);
        Visible = false;
    }

    private void OnMovePressed()
    {
        DisableMenuForAction();
        GetCombatManager()?.PrepareMove();
    }

    private void OnAttackPressed()
    {
        DisableMenuForAction();
        GetCombatManager()?.HandlePlayerAttackRequest();
    }

    private void OnEndTurnPressed()
    {
        DisableMenuForAction();
        Game.Instance?.Hud?.AddLogMessage("Turno terminato da giocatore.");
        GetCombatManager()?.EndTurn();
    }
}
