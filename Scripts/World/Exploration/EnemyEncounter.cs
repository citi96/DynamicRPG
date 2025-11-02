using System.Collections.Generic;
using DynamicRPG.Characters;
using Godot;

#nullable enable

namespace DynamicRPG.World.Exploration;

/// <summary>
/// Detects the player during exploration and starts a combat encounter using the configured enemy group.
/// </summary>
public partial class EnemyEncounter : Area2D
{
    private CollisionShape2D? _collisionShape;
    private bool _combatTriggered;

    [Export]
    public float DetectionRadius { get; set; } = 128f;

    [Export(PropertyHint.ResourceType, nameof(EnemyDefinition))]
    public Godot.Collections.Array<EnemyDefinition> Enemies { get; set; } = new();

    [Export(PropertyHint.MultilineText)]
    public string EncounterDescription { get; set; } = "Un gruppo di nemici balza fuori dall'ombra!";

    public override void _Ready()
    {
        base._Ready();

        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (_collisionShape is null)
        {
            _collisionShape = new CollisionShape2D { Name = "CollisionShape2D" };
            AddChild(_collisionShape);
        }

        UpdateCollisionRadius();
        BodyEntered += HandleBodyEntered;
    }

    public override void _ExitTree()
    {
        BodyEntered -= HandleBodyEntered;
        base._ExitTree();
    }

    /// <summary>
    /// Configures the encounter with the provided enemy definitions and optional detection radius.
    /// </summary>
    /// <param name="definitions">The enemy templates that should be instantiated for combat.</param>
    /// <param name="description">Narrative text displayed when combat starts.</param>
    /// <param name="detectionRadius">Optional detection radius override.</param>
    public void ConfigureEncounter(IEnumerable<EnemyDefinition> definitions, string description, float detectionRadius)
    {
        Enemies.Clear();

        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                continue;
            }

            Enemies.Add(definition);
        }

        EncounterDescription = description;

        if (detectionRadius > 0f)
        {
            DetectionRadius = detectionRadius;
            UpdateCollisionRadius();
        }

        _combatTriggered = false;
    }

    private void HandleBodyEntered(Node body)
    {
        if (_combatTriggered || body is not PlayerController)
        {
            return;
        }

        StartCombat();
    }

    private void StartCombat()
    {
        if (Enemies.Count == 0)
        {
            GD.PushWarning($"L'incontro {Name} non contiene nemici configurati.");
            _combatTriggered = false;
            return;
        }

        var characters = new List<Character>();
        foreach (var definition in Enemies)
        {
            if (definition is null)
            {
                continue;
            }

            characters.Add(definition.CreateCharacter());
        }

        if (characters.Count == 0)
        {
            GD.PushWarning($"L'incontro {Name} non è riuscito a generare nemici validi.");
            _combatTriggered = false;
            return;
        }

        var game = Game.Instance;
        if (game is null)
        {
            GD.PushWarning("Game.Instance non è disponibile: impossibile avviare il combattimento.");
            _combatTriggered = false;
            return;
        }

        if (game.TryStartExplorationEncounter(characters, EncounterDescription))
        {
            _combatTriggered = true;
            QueueFree();
        }
        else
        {
            _combatTriggered = false;
        }
    }

    private void UpdateCollisionRadius()
    {
        if (_collisionShape is null)
        {
            return;
        }

        if (_collisionShape.Shape is not CircleShape2D circle)
        {
            circle = new CircleShape2D();
            _collisionShape.Shape = circle;
        }

        circle.Radius = DetectionRadius;
    }
}
