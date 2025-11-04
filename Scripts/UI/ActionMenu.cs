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
    private Button? _cancelButton;

    public override void _Ready()
    {
        _moveButton = GetNodeOrNull<Button>("VBoxContainer/MoveButton");
        _attackButton = GetNodeOrNull<Button>("VBoxContainer/AttackButton");
        _endTurnButton = GetNodeOrNull<Button>("VBoxContainer/EndTurnButton");
        _cancelButton = GetNodeOrNull<Button>("VBoxContainer/CancelButton");

        ThemeHelper.ApplySharedTheme(this);
        ApplyButtonStyling();
        HideMenu();
        ConnectButtonSignals();

        if (_cancelButton is not null)
        {
            _cancelButton.Visible = false;
            _cancelButton.Disabled = true;
        }
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

        if (_cancelButton is not null)
        {
            _cancelButton.Pressed += OnCancelPressed;
        }
    }

    private void ApplyButtonStyling()
    {
        if (_moveButton is not null)
        {
            _moveButton.Icon = ThemeHelper.GetMoveIcon();
            _moveButton.IconAlignment = HorizontalAlignment.Left;
            _moveButton.ExpandIcon = true;
            _moveButton.TooltipText = "Sposta l'eroe verso una nuova posizione.";
        }

        if (_attackButton is not null)
        {
            _attackButton.Icon = ThemeHelper.GetAttackIcon();
            _attackButton.IconAlignment = HorizontalAlignment.Left;
            _attackButton.ExpandIcon = true;
            _attackButton.TooltipText = "Attacca un bersaglio nel raggio consentito.";
        }

        if (_endTurnButton is not null)
        {
            _endTurnButton.Icon = ThemeHelper.GetEndTurnIcon();
            _endTurnButton.IconAlignment = HorizontalAlignment.Left;
            _endTurnButton.ExpandIcon = true;
            _endTurnButton.TooltipText = "Chiudi il turno corrente e passa ai nemici.";
        }

        if (_cancelButton is not null)
        {
            _cancelButton.IconAlignment = HorizontalAlignment.Left;
            _cancelButton.ExpandIcon = true;
            _cancelButton.TooltipText = "Annulla l'azione corrente e riapri il menu.";
        }
    }

    /// <summary>
    /// Shows the action menu and enables interaction with the buttons.
    /// </summary>
    public void ShowMenu()
    {
        ExitTargetingMode();
        Visible = true;
        SetMenuEnabled(true);
    }

    /// <summary>
    /// Hides the action menu while keeping its state ready for the next activation.
    /// </summary>
    public void HideMenu()
    {
        Visible = false;
        ExitTargetingMode();
        SetMenuEnabled(false);
    }

    /// <summary>
    /// Enables or disables all action buttons and updates the mouse filter accordingly.
    /// </summary>
    /// <param name="enabled">Whether the menu should be interactable.</param>
    public void SetMenuEnabled(bool enabled)
    {
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

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

        if (_cancelButton is not null)
        {
            _cancelButton.Disabled = !enabled;
        }
    }

    private static CombatManager? GetCombatManager() => CombatManager.Instance ?? Game.Instance?.CombatManager;

    private void EnterTargetingMode()
    {
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;

        if (_moveButton is not null)
        {
            _moveButton.Disabled = true;
        }

        if (_attackButton is not null)
        {
            _attackButton.Disabled = true;
        }

        if (_endTurnButton is not null)
        {
            _endTurnButton.Disabled = true;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Visible = true;
            _cancelButton.Disabled = false;
        }
    }

    private void ExitTargetingMode()
    {
        if (_cancelButton is not null)
        {
            _cancelButton.Visible = false;
            _cancelButton.Disabled = true;
        }

        if (_moveButton is not null)
        {
            _moveButton.Disabled = false;
        }

        if (_attackButton is not null)
        {
            _attackButton.Disabled = false;
        }

        if (_endTurnButton is not null)
        {
            _endTurnButton.Disabled = false;
        }
    }

    private void OnMovePressed()
    {
        EnterTargetingMode();
        GetCombatManager()?.PrepareMove();
    }

    private void OnAttackPressed()
    {
        EnterTargetingMode();
        GetCombatManager()?.HandlePlayerAttackRequest();
    }

    private void OnEndTurnPressed()
    {
        HideMenu();
        Game.Instance?.HUD?.AddLogMessage("Turno terminato da giocatore.");
        GetCombatManager()?.EndTurn();
    }

    private void OnCancelPressed()
    {
        GetCombatManager()?.CancelPlayerAction();
    }
}
