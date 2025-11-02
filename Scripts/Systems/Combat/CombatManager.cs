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
    private TileMapLayer? _battleTileLayer;
    private TileSet? _battleTileSet;

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

    private const int EmptyTileSourceId = 0;
    private const int ObstacleTileSourceId = 1;
    private static readonly Vector2I BattleTileSize = new(64, 64);
    private static readonly Color EmptyTileColor = new(0.247f, 0.514f, 0.298f);
    private static readonly Color ObstacleTileColor = new(0.745f, 0.224f, 0.224f);

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
    }

    public override void _Ready()
    {
        base._Ready();
        EnsureBattleTileLayerReady();
        HideBattleGrid();
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
        SyncTileLayerWithGrid();
        PlaceInitialCombatants();

        _rng.Randomize();
        RollInitiative();

        if (TurnOrder.Count == 0)
        {
            LogMessage("Nessun partecipante valido per il combattimento.");
            EndCombat();
            return;
        }

        HandleNewRoundStart();
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

            var baseRoll = (int)_rng.RandiRange(1, 20);
            var dexterityBonus = combatant.Dexterity / 2;
            var traitBonus = combatant.Traits.Any(trait =>
                string.Equals(trait.Name, "Allerta", StringComparison.OrdinalIgnoreCase))
                ? 5
                : 0;

            var total = baseRoll + dexterityBonus + traitBonus + combatant.InitiativeBonus;
            var tieBreaker = _rng.Randf();

            initiativeEntries.Add(new InitiativeEntry(
                combatant,
                total,
                combatant.Dexterity,
                tieBreaker));

            LogMessage(
                $"Iniziativa di {combatant.Name}: tiro {baseRoll} + DEX {dexterityBonus} + tratto {traitBonus} + bonus {combatant.InitiativeBonus} = {total}");
        }

        var ordered = initiativeEntries
            .OrderByDescending(entry => entry.Total)
            .ThenByDescending(entry => entry.DexterityScore)
            .ThenByDescending(entry => entry.TieBreaker)
            .Select(entry => entry.Combatant)
            .ToList();

        TurnOrder.AddRange(ordered);
        CurrentTurnIndex = 0;
        RoundNumber = 1;

        if (TurnOrder.Count > 0)
        {
            LogMessage("Ordine di turno: " + string.Join(", ", TurnOrder.Select(character => character.Name)));
        }
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

        if (TurnOrder.Count == 0)
        {
            EndCombat();
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

        if (currentCombatant.CurrentHealth <= 0)
        {
            LogMessage($"{currentCombatant.Name} è stato sconfitto prima del suo turno, si passa oltre.");
            AdvanceToNextTurnIndex();
            BeginTurn();
            return;
        }

        currentCombatant.ResetMovementForNewTurn();

        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;
        _activePlayerCharacter = null;

        LogMessage($"--- Round {RoundNumber}, turno di {currentCombatant.Name} ---");

        if (Enemies.Contains(currentCombatant))
        {
            ActionMenu?.HideMenu();
            ExecuteEnemyAI(currentCombatant);

            if (IsCombatActive)
            {
                EndTurn();
            }

            return;
        }

        if (Players.Contains(currentCombatant))
        {
            ExecutePlayerTurn(currentCombatant);
            return;
        }

        LogMessage($"{currentCombatant.Name} non appartiene più a nessuna fazione, turno saltato.");
        AdvanceToNextTurnIndex();
        BeginTurn();
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
        // TODO: evidenzia celle raggiungibili (raggio = MovementAllowance)
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
        LogMessage($"Seleziona un bersaglio da attaccare con {_activePlayerCharacter.Name}.");
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

        AdvanceToNextTurnIndex();
        BeginTurn();
    }

    private void ExecutePlayerTurn(Character player)
    {
        _activePlayerCharacter = player;
        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;

        if (ActionMenu is null)
        {
            LogMessage($"{player.Name} attende un'azione del giocatore, ma il menu azioni non è disponibile.");
            return;
        }

        ActionMenu.ShowMenu();
        LogMessage($"{player.Name} attende un'azione del giocatore.");
    }

    private void ExecuteEnemyAI(Character enemy)
    {
        LogMessage($"{enemy.Name} (nemico) valuta le opzioni e agisce.");

        var target = Players.FirstOrDefault(player => player.CurrentHealth > 0);
        if (target is null)
        {
            LogMessage("Nessun giocatore valido da attaccare.");
            return;
        }

        ResolveBasicAttack(enemy, target);
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

        if (!anyEnemyAlive && !anyPlayerAlive)
        {
            LogMessage("Combattimento terminato senza vincitori.");
        }
        else if (!anyEnemyAlive)
        {
            LogMessage("Combattimento terminato: Vittoria del giocatore!");
        }
        else
        {
            LogMessage("I giocatori sono stati sconfitti...");
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
        HideBattleGrid();
    }

    private void AdvanceToNextTurnIndex()
    {
        if (TurnOrder.Count == 0)
        {
            CurrentTurnIndex = 0;
            return;
        }

        CurrentTurnIndex++;

        if (CurrentTurnIndex >= TurnOrder.Count)
        {
            CurrentTurnIndex = 0;
            RoundNumber++;
            HandleNewRoundStart();
        }
    }

    private void HandleNewRoundStart()
    {
        ResetMovementAllowancesForParticipants();
        LogMessage($"Round {RoundNumber} inizia!");
    }

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
        ShowBattleGrid();
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

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        if (!IsCombatActive || _battleTileLayer is null)
        {
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        if (!AwaitingMoveTarget && !AwaitingAttackTarget)
        {
            return;
        }

        var viewport = GetViewport();
        if (viewport is null)
        {
            return;
        }

        var clickPosition = viewport.GetMousePosition();
        var localPosition = _battleTileLayer.ToLocal(clickPosition);
        var cell = _battleTileLayer.LocalToMap(localPosition);

        HandleTileSelection(cell);
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

    private void EnsureBattleTileLayerReady()
    {
        _battleTileLayer ??= GetNodeOrNull<TileMapLayer>("BattleGrid");

        if (_battleTileLayer is null)
        {
            GD.PushWarning("TileMapLayer BattleGrid non trovata nella scena di combattimento.");
            return;
        }

        if (_battleTileLayer.TileSet is null)
        {
            _battleTileSet = CreateBattleTileSet();
            _battleTileLayer.TileSet = _battleTileSet;
        }
        else if (!ReferenceEquals(_battleTileSet, _battleTileLayer.TileSet))
        {
            _battleTileSet = _battleTileLayer.TileSet;
        }

        if (_battleTileSet is not null)
        {
            if (!_battleTileSet.HasSource(EmptyTileSourceId))
            {
                _battleTileSet.AddSource(CreateSolidTileSource(EmptyTileColor), EmptyTileSourceId);
            }

            if (!_battleTileSet.HasSource(ObstacleTileSourceId))
            {
                _battleTileSet.AddSource(CreateSolidTileSource(ObstacleTileColor), ObstacleTileSourceId);
            }

            _battleTileSet.TileSize = BattleTileSize;
        }
    }

    private TileSet CreateBattleTileSet()
    {
        var tileSet = new TileSet
        {
            TileSize = BattleTileSize,
        };

        tileSet.AddSource(CreateSolidTileSource(EmptyTileColor), EmptyTileSourceId);
        tileSet.AddSource(CreateSolidTileSource(ObstacleTileColor), ObstacleTileSourceId);

        return tileSet;
    }

    private static TileSetAtlasSource CreateSolidTileSource(Color color)
    {
        var image = Image.CreateEmpty(BattleTileSize.X, BattleTileSize.Y, false, Image.Format.Rgba8);
        image.Fill(color);

        var texture = ImageTexture.CreateFromImage(image);

        return new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = BattleTileSize,
        };
    }

    private void SyncTileLayerWithGrid()
    {
        if (_combatGrid is null)
        {
            return;
        }

        EnsureBattleTileLayerReady();

        if (_battleTileLayer is null)
        {
            return;
        }

        _battleTileLayer.Clear();
        _battleTileLayer.Visible = true;

        for (var x = 0; x < _combatGrid.Width; x++)
        {
            for (var y = 0; y < _combatGrid.Height; y++)
            {
                var tileType = _combatGrid.GetTile(new GridPosition(x, y));
                var sourceId = tileType == TileType.Obstacle ? ObstacleTileSourceId : EmptyTileSourceId;
                _battleTileLayer.SetCell(new Vector2I(x, y), sourceId, Vector2I.Zero, 0);
            }
        }
    }

    private void HandleTileSelection(Vector2I cell)
    {
        if (_combatGrid is null)
        {
            return;
        }

        var gridPosition = new GridPosition(cell.X, cell.Y);

        if (!_combatGrid.IsWithinBounds(gridPosition))
        {
            LogMessage("La cella selezionata è fuori dalla griglia di combattimento.");
            return;
        }

        if (AwaitingMoveTarget && CurrentCharacter is { } movingCharacter && Players.Contains(movingCharacter))
        {
            HandleMoveSelection(movingCharacter, gridPosition);
            return;
        }

        if (AwaitingAttackTarget && CurrentCharacter is { } attackingCharacter && Players.Contains(attackingCharacter))
        {
            HandleAttackSelection(attackingCharacter, gridPosition);
        }
    }

    private void HandleMoveSelection(Character character, GridPosition destination)
    {
        if (!CanMoveTo(character, destination.X, destination.Y))
        {
            LogMessage($"{character.Name} non può raggiungere la cella {destination}.");
            return;
        }

        var reachedTarget = MoveCharacter(character, destination.X, destination.Y);

        if (reachedTarget)
        {
            AwaitingMoveTarget = false;
            LogMessage($"{character.Name} termina il movimento.");
            EndTurn();
            return;
        }

        if (character.RemainingMovement <= 0)
        {
            AwaitingMoveTarget = false;
            LogMessage($"{character.Name} non ha altro movimento disponibile.");
            EndTurn();
        }
    }

    private void HandleAttackSelection(Character attacker, GridPosition destination)
    {
        var target = FindCharacterAt(destination);

        if (target is null)
        {
            LogMessage("Nessun bersaglio presente su quella cella.");
            return;
        }

        if (!Enemies.Contains(target))
        {
            LogMessage($"{attacker.Name} non può attaccare {target.Name}: non è un nemico valido.");
            return;
        }

        AwaitingAttackTarget = false;
        ResolveBasicAttack(attacker, target);

        if (IsCombatActive)
        {
            EndTurn();
        }
    }

    private Character? FindCharacterAt(GridPosition position)
    {
        if (_combatGrid is null)
        {
            return null;
        }

        return _combatGrid.GetOccupant(position);
    }

    private void ShowBattleGrid()
    {
        EnsureBattleTileLayerReady();

        if (_battleTileLayer is not null)
        {
            _battleTileLayer.Visible = true;
        }
    }

    private void HideBattleGrid()
    {
        if (_battleTileLayer is null)
        {
            return;
        }

        _battleTileLayer.Visible = false;
        _battleTileLayer.Clear();
    }
}
