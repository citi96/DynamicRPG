using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using DynamicRPG.Characters;
using DynamicRPG.UI;

namespace DynamicRPG.Systems.Combat;

#nullable enable

/// <summary>
/// Coordinates the flow of a turn-based combat encounter.
/// </summary>
public sealed partial class CombatManager : Node
{
    private const int DefaultGridWidth = 10;
    private const int DefaultGridHeight = 10;

    private readonly RandomNumberGenerator _rng = new();
    private readonly GridPathfinder _pathfinder = new();

    private CombatGrid? _combatGrid;

    private Character? _activePlayerCharacter;

    public static CombatManager? Instance { get; private set; }

    /// <summary>
    /// Gets the collection of allied combatants.
    /// </summary>
    public List<Character> Players { get; } = new();

    /// <summary>
    /// Gets the collection of enemy combatants.
    /// </summary>
    public List<Character> Enemies { get; } = new();

    /// <summary>
    /// Gets the ordered list of combatants ready to act.
    /// </summary>
    public List<Character> TurnOrder { get; } = new();

    /// <summary>
    /// Gets the index within <see cref="TurnOrder"/> of the combatant whose turn is active.
    /// </summary>
    public int CurrentTurnIndex { get; private set; }

    /// <summary>
    /// Gets the current round number.
    /// </summary>
    public int RoundNumber { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a combat encounter is running.
    /// </summary>
    public bool IsCombatActive { get; private set; }

    /// <summary>
    /// Gets the active combat grid, when available.
    /// </summary>
    public CombatGrid? CurrentGrid => _combatGrid;

    /// <summary>
    /// Gets or sets the action menu associated with combat turns.
    /// </summary>
    public ActionMenu? ActionMenu { get; set; }

    /// <summary>
    /// Gets the combatant whose turn is currently active, when available.
    /// </summary>
    public Character? CurrentCharacter =>
        TurnOrder.Count == 0 || CurrentTurnIndex < 0 || CurrentTurnIndex >= TurnOrder.Count
            ? null
            : TurnOrder[CurrentTurnIndex];

    /// <summary>
    /// Gets a value indicating whether the combat manager is waiting for a movement target.
    /// </summary>
    public bool AwaitingMoveTarget { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the combat manager is waiting for an attack target.
    /// </summary>
    public bool AwaitingAttackTarget { get; private set; }

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        base._ExitTree();
    }

    /// <summary>
    /// Initializes and starts a combat encounter with the provided participants.
    /// </summary>
    /// <param name="players">Collection of allied combatants.</param>
    /// <param name="enemies">Collection of enemy combatants.</param>
    public void StartCombat(IEnumerable<Character> players, IEnumerable<Character> enemies)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(enemies);

        Players.Clear();
        Players.AddRange(players.Where(character => character is not null));

        Enemies.Clear();
        Enemies.AddRange(enemies.Where(character => character is not null));

        TurnOrder.Clear();
        RoundNumber = 1;
        CurrentTurnIndex = 0;
        IsCombatActive = Players.Any(character => character.CurrentHealth > 0) &&
            Enemies.Any(character => character.CurrentHealth > 0);

        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;
        _activePlayerCharacter = null;
        ActionMenu?.HideMenu();

        if (!IsCombatActive)
        {
            LogMessage("Impossibile avviare il combattimento: servono almeno un alleato e un nemico vivi.");
            EndCombat();
            return;
        }

        InitializeArenaLayout();
        PlaceInitialCombatants();
        ResetMovementAllowancesForParticipants();

        _rng.Randomize();
        RollInitiative();

        if (TurnOrder.Count == 0)
        {
            LogMessage("Nessun partecipante valido per il combattimento.");
            EndCombat();
            return;
        }

        CurrentTurnIndex = 0;
        BeginTurn();
    }

    /// <summary>
    /// Determines the initial turn order using initiative rolls.
    /// </summary>
    public void RollInitiative()
    {
        TurnOrder.Clear();

        var initiativeEntries = new List<InitiativeEntry>();
        foreach (var combatant in Players.Concat(Enemies))
        {
            if (combatant is null)
            {
                continue;
            }

            var roll = (int)_rng.RandiRange(1, 20);
            var dexterityModifier = CalculateAbilityModifier(combatant.Dexterity);
            var total = roll + dexterityModifier + combatant.InitiativeBonus;
            var tieBreaker = _rng.Randf();

            initiativeEntries.Add(new InitiativeEntry(combatant, total, combatant.Dexterity, tieBreaker));
            LogMessage($"Iniziativa di {combatant.Name}: tiro {roll} + DEX {dexterityModifier} + bonus {combatant.InitiativeBonus} = {total}");
        }

        var ordered = initiativeEntries
            .OrderByDescending(entry => entry.Total)
            .ThenByDescending(entry => entry.DexterityScore)
            .ThenByDescending(entry => entry.TieBreaker)
            .Select(entry => entry.Combatant)
            .ToList();

        TurnOrder.AddRange(ordered);

        LogMessage("Ordine di turno: " + string.Join(", ", TurnOrder.Select(character => character.Name)));
    }

    /// <summary>
    /// Begins the turn of the current combatant.
    /// </summary>
    public void BeginTurn()
    {
        if (!IsCombatActive)
        {
            return;
        }

        RemoveDefeatedCombatants();

        if (CheckBattleEnd())
        {
            return;
        }

        if (TurnOrder.Count == 0)
        {
            EndCombat();
            return;
        }

        CurrentTurnIndex = Math.Clamp(CurrentTurnIndex, 0, TurnOrder.Count - 1);
        var currentCombatant = TurnOrder[CurrentTurnIndex];

        currentCombatant.ResetMovementForNewTurn();

        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;
        _activePlayerCharacter = null;
        ActionMenu?.HideMenu();

        if (currentCombatant.CurrentHealth <= 0)
        {
            LogMessage($"{currentCombatant.Name} è stato sconfitto prima del suo turno, si passa oltre.");
            EndTurn();
            return;
        }

        LogMessage($"--- Round {RoundNumber}, turno di {currentCombatant.Name} ---");

        if (Enemies.Contains(currentCombatant))
        {
            ExecuteEnemyTurn(currentCombatant);
            return;
        }

        if (Players.Contains(currentCombatant))
        {
            ExecutePlayerTurn(currentCombatant);
            return;
        }

        LogMessage($"{currentCombatant.Name} non appartiene più a nessuna fazione, turno saltato.");
        EndTurn();
    }

    public void PrepareMove()
    {
        if (!IsCombatActive || _activePlayerCharacter is null)
        {
            ActionMenu?.ShowMenu();
            return;
        }

        AwaitingMoveTarget = true;
        AwaitingAttackTarget = false;

        LogMessage($"Seleziona una destinazione di movimento per {_activePlayerCharacter.Name}.");
    }

    public void HandlePlayerAttackRequest()
    {
        if (!IsCombatActive || _activePlayerCharacter is null)
        {
            ActionMenu?.ShowMenu();
            return;
        }

        AwaitingMoveTarget = false;
        AwaitingAttackTarget = true;

        var target = Enemies.FirstOrDefault(enemy => enemy.CurrentHealth > 0);
        if (target is null)
        {
            LogMessage("Nessun nemico valido da attaccare.");
            AwaitingAttackTarget = false;
            ActionMenu?.ShowMenu();
            return;
        }

        LogMessage($"{_activePlayerCharacter.Name} sferra un attacco automatico contro {target.Name}.");
        ResolveBasicAttack(_activePlayerCharacter, target);
        AwaitingAttackTarget = false;

        if (IsCombatActive)
        {
            EndTurn();
        }
    }

    /// <summary>
    /// Ends the current turn and advances to the next combatant.
    /// </summary>
    public void EndTurn()
    {
        if (!IsCombatActive)
        {
            return;
        }

        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;
        _activePlayerCharacter = null;
        ActionMenu?.HideMenu();

        if (CheckBattleEnd())
        {
            return;
        }

        if (TurnOrder.Count == 0)
        {
            EndCombat();
            return;
        }

        CurrentTurnIndex++;

        if (CurrentTurnIndex >= TurnOrder.Count)
        {
            CurrentTurnIndex = 0;
            RoundNumber++;
        }

        BeginTurn();
    }

    private void ExecutePlayerTurn(Character player)
    {
        _activePlayerCharacter = player;
        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;

        if (ActionMenu is null)
        {
            LogMessage($"{player.Name} (giocatore) esegue un'azione automatica di test (menu azioni non disponibile).");

            var fallbackTarget = Enemies.FirstOrDefault(enemy => enemy.CurrentHealth > 0);
            if (fallbackTarget is null)
            {
                LogMessage("Nessun nemico valido da attaccare.");
                EndTurn();
                return;
            }

            ResolveBasicAttack(player, fallbackTarget);

            if (IsCombatActive)
            {
                EndTurn();
            }

            return;
        }

        ActionMenu.ShowMenu();
        LogMessage($"{player.Name} attende un'azione del giocatore.");
    }

    private void ExecuteEnemyTurn(Character enemy)
    {
        ActionMenu?.HideMenu();
        LogMessage($"{enemy.Name} (nemico) agisce con l'IA di base.");

        var target = Players.FirstOrDefault(player => player.CurrentHealth > 0);
        if (target is null)
        {
            LogMessage("Nessun giocatore valido da attaccare.");
            EndTurn();
            return;
        }

        ResolveBasicAttack(enemy, target);

        if (IsCombatActive)
        {
            EndTurn();
        }
    }

    private void ResolveBasicAttack(Character attacker, Character defender)
    {
        var damage = Math.Max(attacker.AttackRating, 1);
        var defenderWasDefeated = defender.TakeDamage(damage);

        LogMessage($"{attacker.Name} attacca {defender.Name} infliggendo {damage} danni. " +
                 $"HP rimanenti: {defender.CurrentHealth}/{defender.MaxHealth}");

        if (defenderWasDefeated)
        {
            LogMessage($"{defender.Name} è stato sconfitto!");
        }

        RemoveDefeatedCombatants();
        CheckBattleEnd();
    }

    private void RemoveDefeatedCombatants()
    {
        if (_combatGrid is not null)
        {
            foreach (var defeated in Players.Concat(Enemies).Where(character => character.CurrentHealth <= 0).ToList())
            {
                _combatGrid.Vacate(new GridPosition(defeated.PositionX, defeated.PositionY), defeated);
            }
        }

        var removedFromTurnOrder = TurnOrder.RemoveAll(character => character.CurrentHealth <= 0);

        if (removedFromTurnOrder > 0)
        {
            Players.RemoveAll(character => character.CurrentHealth <= 0);
            Enemies.RemoveAll(character => character.CurrentHealth <= 0);

            if (TurnOrder.Count == 0)
            {
                CurrentTurnIndex = 0;
            }
            else
            {
                CurrentTurnIndex %= TurnOrder.Count;
            }
        }
    }

    private bool CheckBattleEnd()
    {
        var anyPlayerAlive = Players.Any(character => character.CurrentHealth > 0);
        var anyEnemyAlive = Enemies.Any(character => character.CurrentHealth > 0);

        if (anyPlayerAlive && anyEnemyAlive)
        {
            return false;
        }

        if (!anyPlayerAlive && !anyEnemyAlive)
        {
            LogMessage("Il combattimento termina senza vincitori.");
        }
        else if (!anyEnemyAlive)
        {
            LogMessage("I giocatori vincono il combattimento!");
        }
        else
        {
            LogMessage("I giocatori sono stati sconfitti.");
        }

        EndCombat();
        return true;
    }

    private void EndCombat()
    {
        Players.Clear();
        Enemies.Clear();
        TurnOrder.Clear();
        CurrentTurnIndex = 0;
        RoundNumber = 0;
        IsCombatActive = false;
        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;
        _activePlayerCharacter = null;
        ActionMenu?.HideMenu();
        _combatGrid?.ClearOccupants();
        _combatGrid = null;
    }

    private static int CalculateAbilityModifier(int abilityScore) => 
        (int)Math.Floor((abilityScore - 10) / 2.0);

    private readonly record struct InitiativeEntry(Character Combatant, int Total, int DexterityScore, float TieBreaker);

    private void InitializeArenaLayout()
    {
        var grid = new CombatGrid(DefaultGridWidth, DefaultGridHeight, TileType.Empty);

        for (var x = 0; x < DefaultGridWidth; x++)
        {
            grid.SetTile(new GridPosition(x, 0), TileType.Obstacle);
            grid.SetTile(new GridPosition(x, DefaultGridHeight - 1), TileType.Obstacle);
        }

        for (var y = 0; y < DefaultGridHeight; y++)
        {
            grid.SetTile(new GridPosition(0, y), TileType.Obstacle);
            grid.SetTile(new GridPosition(DefaultGridWidth - 1, y), TileType.Obstacle);
        }

        grid.SetTile(new GridPosition(4, 5), TileType.Obstacle);
        grid.SetTile(new GridPosition(6, 7), TileType.Obstacle);

        _combatGrid = grid;

        LogMessage($"Griglia di combattimento inizializzata: {DefaultGridWidth}x{DefaultGridHeight}.");
    }

    private void PlaceInitialCombatants()
    {
        if (_combatGrid is null)
        {
            LogMessage("Impossibile posizionare i combattenti: la griglia non è pronta.");
            return;
        }

        var playerSpawns = new List<GridPosition>
        {
            new(2, 2),
            new(3, 2),
            new(2, 3),
            new(3, 3),
            new(2, 4),
            new(3, 4),
        };

        var enemySpawns = new List<GridPosition>
        {
            new(7, 7),
            new(6, 7),
            new(7, 6),
            new(6, 6),
            new(7, 5),
            new(6, 5),
        };

        PlaceCombatantsAtPositions(Players, playerSpawns, "alleato");
        PlaceCombatantsAtPositions(Enemies, enemySpawns, "nemico");
    }

    private void PlaceCombatantsAtPositions(
        IEnumerable<Character> combatants,
        IReadOnlyList<GridPosition> spawnPositions,
        string factionLabel)
    {
        if (_combatGrid is null)
        {
            return;
        }

        var index = 0;

        foreach (var combatant in combatants)
        {
            if (combatant is null)
            {
                continue;
            }

            GridPosition? assignedPosition = null;

            while (index < spawnPositions.Count && assignedPosition is null)
            {
                var spawn = spawnPositions[index];
                index++;

                if (!_combatGrid.CanOccupy(spawn, combatant))
                {
                    continue;
                }

                if (_combatGrid.TryOccupy(spawn, combatant))
                {
                    assignedPosition = spawn;
                }
            }

            if (assignedPosition is GridPosition finalPosition)
            {
                combatant.SetPosition(finalPosition.X, finalPosition.Y);
                LogMessage($"{combatant.Name} ({factionLabel}) posizionato a {finalPosition}.");
            }
            else
            {
                LogMessage($"Nessuna cella libera disponibile per {combatant.Name} ({factionLabel}).");
            }
        }
    }

    private void ResetMovementAllowancesForParticipants()
    {
        foreach (var combatant in Players.Concat(Enemies))
        {
            combatant.ResetMovementForNewTurn();
        }
    }

    public bool CanMoveTo(Character character, int targetX, int targetY)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!IsCombatActive || _combatGrid is null)
        {
            return false;
        }

        if (character.RemainingMovement <= 0)
        {
            return false;
        }

        var target = new GridPosition(targetX, targetY);

        if (!_combatGrid.IsWithinBounds(target))
        {
            return false;
        }

        return _combatGrid.CanOccupy(target, character);
    }

    public IReadOnlyList<GridPosition>? GetPath(Character character, int targetX, int targetY)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!CanMoveTo(character, targetX, targetY))
        {
            return null;
        }

        var grid = _combatGrid!;
        var start = new GridPosition(character.PositionX, character.PositionY);
        var destination = new GridPosition(targetX, targetY);

        return _pathfinder.FindPath(grid, character, start, destination);
    }

    public bool MoveCharacter(Character character, int targetX, int targetY)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!IsCombatActive || _combatGrid is null)
        {
            return false;
        }

        if (targetX == character.PositionX && targetY == character.PositionY)
        {
            return false;
        }

        var path = GetPath(character, targetX, targetY);

        if (path is null || path.Count <= 1)
        {
            LogMessage($"{character.Name} non può raggiungere la destinazione ({targetX}, {targetY}).");
            return false;
        }

        var stepsAvailable = Math.Min(character.RemainingMovement, path.Count - 1);

        if (stepsAvailable <= 0)
        {
            LogMessage($"{character.Name} non ha movimento residuo.");
            return false;
        }

        var stepsTaken = 0;
        var currentPosition = new GridPosition(character.PositionX, character.PositionY);

        for (var i = 1; i < path.Count && stepsTaken < stepsAvailable; i++)
        {
            var nextPosition = path[i];

            if (!_combatGrid.TryTransitionOccupant(currentPosition, nextPosition, character))
            {
                LogMessage($"Il percorso di {character.Name} è stato bloccato in {nextPosition}.");
                break;
            }

            character.SetPosition(nextPosition.X, nextPosition.Y);
            stepsTaken++;
            currentPosition = nextPosition;
            LogMessage($"{character.Name} avanza a {currentPosition}.");
        }

        if (stepsTaken <= 0)
        {
            return false;
        }

        character.ConsumeMovement(stepsTaken);
        LogMessage($"{character.Name} termina il movimento a {currentPosition}. Passi usati: {stepsTaken}, residuo: {character.RemainingMovement}.");

        return currentPosition.X == targetX && currentPosition.Y == targetY;
    }

    private static void LogMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var game = Game.Instance;
        if (game?.HUD is { } hud)
        {
            hud.AddLogMessage(message);
            return;
        }

        GD.Print(message);
    }
}
