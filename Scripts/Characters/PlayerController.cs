using DynamicRPG;
using Godot;

#nullable enable

namespace DynamicRPG.Characters;

public partial class PlayerController : CharacterBody2D
{
    private const float DefaultSpeed = 180f;

    [Export]
    public float MoveSpeed { get; set; } = DefaultSpeed;

    [Export]
    public Rect2 PlayableArea { get; set; } = new Rect2(new Vector2(-640, -360), new Vector2(1280, 720));

    [Export]
    public NodePath? CameraPath { get; set; }

    private Camera2D? _camera;

    public override void _Ready()
    {
        base._Ready();

        EnsureInputActions();

        if (CameraPath is not null && !CameraPath.IsEmpty)
        {
            _camera = GetNodeOrNull<Camera2D>(CameraPath);
        }
        else
        {
            _camera = GetNodeOrNull<Camera2D>("Camera2D");
        }

        _camera?.MakeCurrent();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsMovementLocked())
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        var inputVector = Vector2.Zero;

        inputVector.X = GetAxis("move_left", "move_right");
        inputVector.Y = GetAxis("move_up", "move_down");

        if (inputVector == Vector2.Zero)
        {
            inputVector.X = GetAxis("ui_left", "ui_right");
            inputVector.Y = GetAxis("ui_up", "ui_down");
        }

        if (inputVector.LengthSquared() > 1f)
        {
            inputVector = inputVector.Normalized();
        }

        Velocity = inputVector * MoveSpeed;
        MoveAndSlide();

        if (PlayableArea.Size != Vector2.Zero)
        {
            var min = PlayableArea.Position;
            var max = PlayableArea.Position + PlayableArea.Size;
            var clamped = new Vector2(
                Mathf.Clamp(GlobalPosition.X, min.X, max.X),
                Mathf.Clamp(GlobalPosition.Y, min.Y, max.Y));

            GlobalPosition = clamped;
        }
    }

    private static float GetAxis(string negativeAction, string positiveAction)
    {
        var value = 0f;
        if (InputMap.HasAction(negativeAction) && Input.IsActionPressed(negativeAction))
        {
            value -= 1f;
        }

        if (InputMap.HasAction(positiveAction) && Input.IsActionPressed(positiveAction))
        {
            value += 1f;
        }

        return value;
    }

    private static void EnsureInputActions()
    {
        EnsureActionWithKeys("move_left", Key.A, Key.Left);
        EnsureActionWithKeys("move_right", Key.D, Key.Right);
        EnsureActionWithKeys("move_up", Key.W, Key.Up);
        EnsureActionWithKeys("move_down", Key.S, Key.Down);
    }

    private static void EnsureActionWithKeys(string actionName, Key primary, Key secondary)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName, 0.5f);
        }

        AddKeyIfMissing(actionName, primary);
        AddKeyIfMissing(actionName, secondary);
    }

    private static void AddKeyIfMissing(string actionName, Key key)
    {
        if (!InputMap.HasAction(actionName))
        {
            return;
        }

        foreach (var inputEvent in InputMap.ActionGetEvents(actionName))
        {
            if (inputEvent is InputEventKey keyEvent && (keyEvent.PhysicalKeycode == key || keyEvent.Keycode == key))
            {
                return;
            }
        }

        var newEvent = new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key,
        };

        InputMap.ActionAddEvent(actionName, newEvent);
    }

    private static bool IsMovementLocked()
    {
        var game = Game.Instance;
        if (game?.CombatManager is not { IsCombatActive: true })
        {
            return false;
        }

        return true;
    }
}
