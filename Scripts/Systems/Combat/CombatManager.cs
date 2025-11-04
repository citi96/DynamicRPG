using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using DynamicRPG.Characters;
using DynamicRPG.Items;
using DynamicRPG.UI;

namespace DynamicRPG.Systems.Combat;

#nullable enable

/// <summary>
/// Coordinates the flow of a turn-based combat encounter.
/// Implements Prompts 4.7-4.13 complete specifications.
/// </summary>
public sealed partial class CombatManager : Node
{
    private const int DefaultGridWidth = 10;
    private const int DefaultGridHeight = 10;
    private const int EmptyTileSourceId = 0;
    private const int ObstacleTileSourceId = 1;
    private const int DifficultTileSourceId = 2;
    private const int DefaultRangedWeaponRange = 5;

    private static readonly Vector2I BattleTileSize = new(64, 64);
    private static readonly Color EmptyTileColor = new(0.247f, 0.514f, 0.298f);
    private static readonly Color ObstacleTileColor = new(0.745f, 0.224f, 0.224f);
    private static readonly Color DifficultTileColor = new(0.569f, 0.482f, 0.235f);

    private readonly RandomNumberGenerator _rng = new();
    private readonly GridPathfinder _pathfinder = new();
    private readonly Dictionary<Character, bool> _reactionsUsed = new();

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
        _reactionsUsed.Clear();
        RoundNumber = 1;
        CurrentTurnIndex = 0;
        IsCombatActive = Players.Any(c => c.CurrentHealth > 0) &&
                         Enemies.Any(c => c.CurrentHealth > 0);

        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;
        _activePlayerCharacter = null;
        ActionMenu?.HideMenu();

        if (Players.FirstOrDefault(player => player.CurrentHealth > 0) is { } primaryPlayer)
        {
            // Applicazione dimostrativa: il primo giocatore vivo inizia avvelenato per mostrare il ticking a fine turno.
            if (!primaryPlayer.HasStatus(StatusType.Poisoned))
            {
                primaryPlayer.ApplyStatus(StatusType.Poisoned, duration: 3, potency: 2);
            }
        }

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
            if (combatant is null) continue;

            var baseRoll = (int)_rng.RandiRange(1, 20);
            var dexBonus = combatant.Dexterity / 2;
            var traitBonus = combatant.Traits.Any(t =>
                string.Equals(t.Name, "Allerta", StringComparison.OrdinalIgnoreCase)) ? 5 : 0;

            var total = baseRoll + dexBonus + traitBonus + combatant.InitiativeBonus;
            var tieBreaker = _rng.Randf();

            initiativeEntries.Add(new InitiativeEntry(combatant, total, combatant.Dexterity, tieBreaker));
            LogMessage($"Iniziativa di {combatant.Name}: {baseRoll}+{dexBonus}+{traitBonus}+{combatant.InitiativeBonus} = {total}");
        }

        var ordered = initiativeEntries
            .OrderByDescending(e => e.Total)
            .ThenByDescending(e => e.DexterityScore)
            .ThenByDescending(e => e.TieBreaker)
            .Select(e => e.Combatant)
            .ToList();

        TurnOrder.AddRange(ordered);
        CurrentTurnIndex = 0;
        RoundNumber = 1;

        if (TurnOrder.Count > 0)
        {
            LogMessage("Ordine di turno: " + string.Join(", ", TurnOrder.Select(c => c.Name)));
        }
    }

    /// <summary>
    /// Begins the turn of the current combatant.
    /// </summary>
    public void BeginTurn()
    {
        if (!IsCombatActive || TurnOrder.Count == 0)
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
            LogMessage($"{currentCombatant.Name} è stato sconfitto prima del suo turno.");
            AdvanceToNextTurnIndex();
            BeginTurn();
            return;
        }

        // Resetta movimento e processa status inizio turno
        currentCombatant.ResetMovementForNewTurn();

        // Processa status effects inizio turno (Prompt 4.9)
        var canAct = currentCombatant.ProcessBeginTurnStatusEffects();

        if (!canAct)
        {
            // Il personaggio è stordito/congelato e non può agire
            AdvanceToNextTurnIndex();
            BeginTurn();
            return;
        }

        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;
        _activePlayerCharacter = null;

        LogMessage($"--- Round {RoundNumber}, turno di {currentCombatant.Name} ---");

        // Aggiorna UI turno (Prompt 4.12)
        UpdateTurnDisplay();

        if (Enemies.Contains(currentCombatant))
        {
            ActionMenu?.HideMenu();
            ExecuteEnemyAI(currentCombatant);
            if (IsCombatActive) EndTurn();
            return;
        }

        if (Players.Contains(currentCombatant))
        {
            ExecutePlayerTurn(currentCombatant);
            return;
        }

        LogMessage($"{currentCombatant.Name} non appartiene più a nessuna fazione.");
        AdvanceToNextTurnIndex();
        BeginTurn();
    }

    public void EndTurn()
    {
        if (!IsCombatActive || CurrentCharacter is null)
        {
            return;
        }

        // Processa status effects fine turno (Prompt 4.9)
        CurrentCharacter.ProcessEndTurnStatusEffects();

        // Aggiorna UI player se necessario (Prompt 4.8)
        if (CurrentCharacter.IsPlayer)
        {
            UpdatePlayerStatsDisplay();
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
        LogMessage($"Seleziona un bersaglio da attaccare con {_activePlayerCharacter.Name}.");
    }

    /// <summary>
    /// Performs complete attack resolution (Prompt 4.7).
    /// </summary>
    public bool PerformAttack(Character attacker, Character defender)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);

        if (!IsCombatActive || _combatGrid is null)
        {
            return false;
        }

        var weapon = attacker.EquippedWeapon;
        var isRangedAttack = IsRangedWeapon(weapon);

        if (!IsWithinAttackRange(attacker, defender, weapon, isRangedAttack))
        {
            if (attacker.IsPlayer && ReferenceEquals(CurrentCharacter, attacker))
            {
                LogMessage("Bersaglio fuori portata!");
            }
            else
            {
                LogMessage($"{attacker.Name} non può colpire {defender.Name}: bersaglio fuori portata.");
            }

            return false;
        }

        var coverInfo = isRangedAttack ? EvaluateCover(attacker, defender) : CoverInfo.None;

        if (isRangedAttack && coverInfo.BlocksLineOfSight)
        {
            LogMessage("Linea di vista ostruita: attacco impossibile.");
            return false;
        }

        if (isRangedAttack && coverInfo.GrantsCover)
        {
            LogMessage($"Il bersaglio è in copertura: attacco penalizzato (+{coverInfo.ArmorClassBonus} CA).");
        }

        var attackRoll = _rng.RandiRange(1, 20);
        var attackBonus = CalculateAttackBonus(attacker, weapon, isRangedAttack);
        var totalAttack = attackRoll + attackBonus;

        var coverBonus = isRangedAttack ? coverInfo.ArmorClassBonus : 0;
        var defenderArmorClass = CalculateArmorClass(defender) + coverBonus;

        var isCriticalHit = attackRoll == 20;
        var isCriticalFailure = attackRoll == 1;

        if (isCriticalFailure)
        {
            LogMessage($"{attacker.Name} attacca {defender.Name} ma fallisce clamorosamente! (Fallimento critico!)");
            FinalizeAttack(attacker);
            return true;
        }

        if (!isCriticalHit && totalAttack < defenderArmorClass)
        {
            LogMessage($"{attacker.Name} attacca {defender.Name} ma manca il colpo.");
            FinalizeAttack(attacker);
            return true;
        }

        var damage = CalculateDamage(attacker, defender, weapon, isCriticalHit, isRangedAttack);
        var defenderDefeated = defender.TakeDamage(damage);

        var critSuffix = isCriticalHit ? " (Colpo Critico!)" : string.Empty;
        LogMessage($"{attacker.Name} colpisce {defender.Name} infliggendo {damage} danni.{critSuffix}");

        if (defender.IsPlayer)
        {
            UpdatePlayerStatsDisplay();
        }

        if (defenderDefeated)
        {
            LogMessage($"{defender.Name} è morto.");
        }

        RemoveDefeatedCombatants();
        CheckBattleEnd();

        FinalizeAttack(attacker);
        return true;
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

        // Verifica opportunità di attacco (Prompt 4.13)
        if (!ProcessOpportunityAttacks(character))
        {
            return false; // Personaggio ucciso da AoO
        }

        var path = GetPath(character, targetX, targetY);

        if (path is null || path.Count <= 1)
        {
            LogMessage($"{character.Name} non può raggiungere ({targetX}, {targetY}).");
            return false;
        }

        if (character.RemainingMovement <= 0)
        {
            LogMessage($"{character.Name} non ha movimento residuo.");
            return false;
        }

        var movementBudget = character.RemainingMovement;
        var movementSpent = 0;
        var currentPosition = new GridPosition(character.PositionX, character.PositionY);
        var moved = false;

        for (var i = 1; i < path.Count; i++)
        {
            var nextPosition = path[i];

            var tileCost = _combatGrid.GetMovementCost(nextPosition);

            if (movementSpent + tileCost > movementBudget)
            {
                break;
            }

            if (!_combatGrid.TryTransitionOccupant(currentPosition, nextPosition, character))
            {
                LogMessage($"Percorso bloccato in {nextPosition}.");
                break;
            }

            character.SetPosition(nextPosition.X, nextPosition.Y);
            movementSpent += tileCost;
            currentPosition = nextPosition;
            moved = true;

            if (tileCost > 1)
            {
                LogMessage($"Il terreno difficile rallenta {character.Name}.");
            }

            LogMessage($"{character.Name} → {currentPosition}");
        }

        if (!moved)
        {
            return false;
        }

        character.ConsumeMovement(movementSpent);
        LogMessage($"{character.Name} termina movimento. Punti spesi: {movementSpent}, residuo: {character.RemainingMovement}");

        return currentPosition.X == targetX && currentPosition.Y == targetY;
    }

    /// <summary>
    /// Processa attacchi di opportunità quando un personaggio si muove (Prompt 4.13).
    /// </summary>
    private bool ProcessOpportunityAttacks(Character mover)
    {
        if (_combatGrid is null)
        {
            return true;
        }

        var moverPos = new GridPosition(mover.PositionX, mover.PositionY);
        var enemies = mover.IsPlayer ? Enemies : Players;
        var adjacentEnemies = new List<Character>();

        // Trova nemici adiacenti
        foreach (var enemy in enemies)
        {
            if (enemy.CurrentHealth <= 0)
            {
                continue;
            }

            var enemyPos = new GridPosition(enemy.PositionX, enemy.PositionY);
            var distance = CalculateDistance(moverPos, enemyPos);

            if (distance <= 1.5f) // Adiacente (inclusi diagonali)
            {
                adjacentEnemies.Add(enemy);
            }
        }

        // Esegui attacchi di opportunità
        foreach (var enemy in adjacentEnemies)
        {
            if (_reactionsUsed.TryGetValue(enemy, out var used) && used)
            {
                continue; // Questo nemico ha già usato la reazione
            }

            LogMessage($"{enemy.Name} effettua un attacco di opportunità su {mover.Name}!");
            _reactionsUsed[enemy] = true;

            PerformAttack(enemy, mover);

            if (mover.CurrentHealth <= 0)
            {
                LogMessage($"{mover.Name} viene abbattuto durante il movimento!");
                return false; // Movimento interrotto
            }
        }

        return true;
    }

    private enum CoverType
    {
        None,
        Half,
        ThreeQuarters,
        Blocked,
    }

    private readonly record struct CoverInfo(CoverType Type)
    {
        public static CoverInfo None => new(CoverType.None);

        public bool GrantsCover => Type is CoverType.Half or CoverType.ThreeQuarters;

        public bool BlocksLineOfSight => Type == CoverType.Blocked;

        public int ArmorClassBonus => Type switch
        {
            CoverType.Half => 2,
            CoverType.ThreeQuarters => 5,
            _ => 0,
        };
    }

    /// <summary>
    /// Determines whether the defender benefits from cover relative to the attacker.
    /// </summary>
    private bool HasCover(Character attacker, Character defender) =>
        EvaluateCover(attacker, defender).GrantsCover;

    /// <summary>
    /// Evaluates cover state between two combatants using a Bresenham line trace.
    /// </summary>
    private CoverInfo EvaluateCover(Character attacker, Character defender)
    {
        if (_combatGrid is null)
        {
            return CoverInfo.None;
        }

        var attackerPos = new GridPosition(attacker.PositionX, attacker.PositionY);
        var defenderPos = new GridPosition(defender.PositionX, defender.PositionY);

        var linePoints = GetLineOfSight(attackerPos, defenderPos);
        var hasLightCover = false;
        var hasHeavyCover = false;
        var lineBlocked = false;

        foreach (var point in linePoints)
        {
            if (point == attackerPos || point == defenderPos)
            {
                continue;
            }

            if (!_combatGrid.IsWithinBounds(point))
            {
                continue;
            }

            if (_combatGrid.GetTile(point) != TileType.Obstacle)
            {
                continue;
            }

            hasLightCover = true;

            var distanceToDefender = CalculateGridDistance(point, defenderPos);

            // +5 AC when an obstacle is adjacent to the defender (three-quarter cover).
            if (distanceToDefender <= 1)
            {
                hasHeavyCover = true;
                continue;
            }

            // Any obstacle outside the defender's immediate space blocks the line outright.
            lineBlocked = true;
            break;
        }

        if (lineBlocked)
        {
            return new CoverInfo(CoverType.Blocked);
        }

        if (hasHeavyCover)
        {
            return new CoverInfo(CoverType.ThreeQuarters);
        }

        if (hasLightCover)
        {
            return new CoverInfo(CoverType.Half);
        }

        return CoverInfo.None;
    }

    /// <summary>
    /// Traccia una linea tra due punti usando algoritmo di Bresenham.
    /// </summary>
    private List<GridPosition> GetLineOfSight(GridPosition from, GridPosition to)
    {
        var points = new List<GridPosition>();
        var x0 = from.X;
        var y0 = from.Y;
        var x1 = to.X;
        var y1 = to.Y;

        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            points.Add(new GridPosition(x0, y0));

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }

    private bool IsWithinAttackRange(Character attacker, Character defender, Item? weapon, bool isRangedAttack)
    {
        var attackerPos = new GridPosition(attacker.PositionX, attacker.PositionY);
        var defenderPos = new GridPosition(defender.PositionX, defender.PositionY);
        var distance = CalculateGridDistance(attackerPos, defenderPos);
        var weaponRange = GetWeaponRange(weapon, isRangedAttack);

        return distance <= weaponRange;
    }

    private static float CalculateDistance(GridPosition from, GridPosition to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private int CalculateAttackBonus(Character attacker, Item? weapon, bool isRangedAttack)
    {
        var abilityBonus = CalculateAbilityModifier(isRangedAttack ? attacker.Dexterity : attacker.Strength);
        var weaponAccuracy = weapon?.AccuracyBonus ?? 0;

        var skillName = isRangedAttack ? "Archery" : "OneHandedWeapons";
        var skillBonus = attacker.Skills.TryGetValue(skillName, out var skill) ? skill / 5 : 0;

        return abilityBonus + weaponAccuracy + skillBonus;
    }

    private int CalculateArmorClass(Character defender)
    {
        const int baseArmorClass = 10;
        var dexterityBonus = CalculateAbilityModifier(defender.Dexterity);
        var armorBonus = defender.EquippedArmor?.DefenseBonus ?? 0;

        return baseArmorClass + dexterityBonus + armorBonus;
    }

    private int CalculateDamage(
        Character attacker,
        Character defender,
        Item? weapon,
        bool isCriticalHit,
        bool isRangedAttack)
    {
        int minDamage;
        int maxDamage;

        if (weapon?.MinDamage is { } weaponMin && weapon.MaxDamage is { } weaponMax)
        {
            minDamage = Math.Min(weaponMin, weaponMax);
            maxDamage = Math.Max(weaponMin, weaponMax);
        }
        else
        {
            minDamage = 1;
            maxDamage = 3;
        }

        var damageRoll = _rng.RandiRange(minDamage, maxDamage);

        if (isCriticalHit)
        {
            damageRoll *= 2;
        }

        var abilityBonus = CalculateAbilityModifier(isRangedAttack ? attacker.Dexterity : attacker.Strength);
        var baseDamage = Math.Max(0, damageRoll + abilityBonus);
        var damageReduction = defender.EquippedArmor?.DamageReduction ?? 0;

        return Math.Max(0, baseDamage - damageReduction);
    }

    private void FinalizeAttack(Character attacker)
    {
        if (!attacker.IsPlayer || !ReferenceEquals(CurrentCharacter, attacker) || !IsCombatActive)
        {
            return;
        }

        EndTurn();
    }

    private static bool IsRangedWeapon(Item? weapon) =>
        weapon?.Type?.Contains("Ranged", StringComparison.OrdinalIgnoreCase) == true;

    private static int GetWeaponRange(Item? weapon, bool isRangedAttack)
    {
        if (weapon is not null && weapon.Range > 0)
        {
            return weapon.Range;
        }

        return isRangedAttack ? DefaultRangedWeaponRange : 1;
    }

    private static int CalculateAbilityModifier(int score) =>
        (int)MathF.Floor((score - 10) / 2f);

    private static int CalculateGridDistance(GridPosition from, GridPosition to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = Math.Abs(to.Y - from.Y);
        return Math.Max(dx, dy);
    }

    private void UpdatePlayerStatsDisplay()
    {
        var game = Game.Instance;
        if (game?.Player is { } player && game.HUD is { } hud)
        {
            hud.UpdatePlayerStats(
                player.CurrentHealth,
                player.MaxHealth,
                player.CurrentMana,
                player.MaxMana);

            // Aggiorna status effects (Prompt 4.10)
            hud.UpdateStatusEffects(player.StatusEffects);
        }
    }

    private void UpdateTurnDisplay()
    {
        if (CurrentCharacter is null) return;

        var game = Game.Instance;
        game?.HUD?.UpdateTurnIndicator(CurrentCharacter.Name, RoundNumber);
    }

    private void ExecutePlayerTurn(Character player)
    {
        _activePlayerCharacter = player;
        AwaitingMoveTarget = false;
        AwaitingAttackTarget = false;

        // Aggiorna display player (Prompt 4.8)
        UpdatePlayerStatsDisplay();

        if (ActionMenu is null)
        {
            LogMessage($"{player.Name} attende azione, ma ActionMenu non disponibile.");
            return;
        }

        ActionMenu.ShowMenu();
        LogMessage($"{player.Name} attende un'azione.");
    }

    private void ExecuteEnemyAI(Character enemy)
    {
        LogMessage($"{enemy.Name} (nemico) agisce.");

        var target = Players.FirstOrDefault(p => p.CurrentHealth > 0);
        if (target is null)
        {
            LogMessage("Nessun player valido da attaccare.");
            return;
        }

        var attackResolved = PerformAttack(enemy, target);

        if (attackResolved)
        {
            return;
        }

        if (!TryAdvanceTowardsTarget(enemy, target))
        {
            LogMessage($"{enemy.Name} non riesce ad avvicinarsi a {target.Name}.");
        }
    }

    private bool TryAdvanceTowardsTarget(Character mover, Character target)
    {
        if (_combatGrid is null || mover.RemainingMovement <= 0)
        {
            return false;
        }

        var moverStart = new GridPosition(mover.PositionX, mover.PositionY);
        var targetPos = new GridPosition(target.PositionX, target.PositionY);

        var candidatePositions = new List<GridPosition>();
        var directions = new[]
        {
            new GridPosition(1, 0),
            new GridPosition(-1, 0),
            new GridPosition(0, 1),
            new GridPosition(0, -1),
        };

        foreach (var direction in directions)
        {
            var neighbor = targetPos.Offset(direction.X, direction.Y);

            if (!_combatGrid.IsWithinBounds(neighbor) || !_combatGrid.CanOccupy(neighbor, mover))
            {
                continue;
            }

            candidatePositions.Add(neighbor);
        }

        if (candidatePositions.Count == 0)
        {
            var stepX = Math.Sign(targetPos.X - moverStart.X);
            var stepY = Math.Sign(targetPos.Y - moverStart.Y);

            if (stepX != 0)
            {
                var forward = moverStart.Offset(stepX, 0);
                if (_combatGrid.IsWithinBounds(forward) && _combatGrid.CanOccupy(forward, mover))
                {
                    candidatePositions.Add(forward);
                }
            }

            if (stepY != 0)
            {
                var vertical = moverStart.Offset(0, stepY);
                if (_combatGrid.IsWithinBounds(vertical) && _combatGrid.CanOccupy(vertical, mover))
                {
                    candidatePositions.Add(vertical);
                }
            }
        }

        if (candidatePositions.Count == 0)
        {
            return false;
        }

        candidatePositions.Sort((a, b) =>
            CalculateGridDistance(moverStart, a).CompareTo(CalculateGridDistance(moverStart, b)));

        foreach (var candidate in candidatePositions)
        {
            if (!CanMoveTo(mover, candidate.X, candidate.Y))
            {
                continue;
            }

            var previousPosition = new GridPosition(mover.PositionX, mover.PositionY);
            var previousMovement = mover.RemainingMovement;

            MoveCharacter(mover, candidate.X, candidate.Y);

            if (mover.RemainingMovement < previousMovement ||
                mover.PositionX != previousPosition.X ||
                mover.PositionY != previousPosition.Y)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveDefeatedCombatants()
    {
        if (_combatGrid is not null)
        {
            foreach (var defeated in Players.Concat(Enemies).Where(c => c.CurrentHealth <= 0))
            {
                _combatGrid.Vacate(new GridPosition(defeated.PositionX, defeated.PositionY), defeated);
            }
        }

        var removed = TurnOrder.RemoveAll(c => c.CurrentHealth <= 0);

        if (removed > 0)
        {
            Players.RemoveAll(c => c.CurrentHealth <= 0);
            Enemies.RemoveAll(c => c.CurrentHealth <= 0);

            CurrentTurnIndex = TurnOrder.Count == 0 ? 0 : CurrentTurnIndex % TurnOrder.Count;
        }
    }

    private bool CheckBattleEnd()
    {
        var playersAlive = Players.Any(c => c.CurrentHealth > 0);
        var enemiesAlive = Enemies.Any(c => c.CurrentHealth > 0);

        if (playersAlive && enemiesAlive)
        {
            return false;
        }

        if (!enemiesAlive && !playersAlive)
        {
            LogMessage("Combattimento terminato senza vincitori.");
        }
        else if (!enemiesAlive)
        {
            LogMessage("Vittoria del giocatore!");
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
        _reactionsUsed.Clear();
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

        Game.Instance?.HUD?.ClearTurnIndicator();
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

        // Reset reactions (Prompt 4.13)
        _reactionsUsed.Clear();

        LogMessage($"Round {RoundNumber} inizia!");
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

        if (!IsCombatActive || _combatGrid is null || character.RemainingMovement <= 0)
        {
            return false;
        }

        var target = new GridPosition(targetX, targetY);
        return _combatGrid.IsWithinBounds(target) && _combatGrid.CanOccupy(target, character);
    }

    public IReadOnlyList<GridPosition>? GetPath(Character character, int targetX, int targetY)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!CanMoveTo(character, targetX, targetY))
        {
            return null;
        }

        var start = new GridPosition(character.PositionX, character.PositionY);
        var dest = new GridPosition(targetX, targetY);

        return _pathfinder.FindPath(_combatGrid!, character, start, dest);
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
        if (viewport is null) return;

        var clickPos = viewport.GetMousePosition();
        var localPos = _battleTileLayer.ToLocal(clickPos);
        var cell = _battleTileLayer.LocalToMap(localPos);

        HandleTileSelection(cell);
    }

    private void HandleTileSelection(Vector2I cell)
    {
        if (_combatGrid is null) return;

        var gridPos = new GridPosition(cell.X, cell.Y);

        if (!_combatGrid.IsWithinBounds(gridPos))
        {
            LogMessage("Cella fuori dalla griglia.");
            return;
        }

        if (AwaitingMoveTarget && CurrentCharacter is { } mover && Players.Contains(mover))
        {
            HandleMoveSelection(mover, gridPos);
            return;
        }

        if (AwaitingAttackTarget && CurrentCharacter is { } attacker && Players.Contains(attacker))
        {
            HandleAttackSelection(attacker, gridPos);
        }
    }

    private void HandleMoveSelection(Character character, GridPosition destination)
    {
        if (!CanMoveTo(character, destination.X, destination.Y))
        {
            LogMessage($"{character.Name} non può raggiungere {destination}.");
            return;
        }

        var reached = MoveCharacter(character, destination.X, destination.Y);

        if (reached || character.RemainingMovement <= 0)
        {
            AwaitingMoveTarget = false;
            LogMessage($"{character.Name} termina il movimento.");
            EndTurn();
        }
    }

    private void HandleAttackSelection(Character attacker, GridPosition destination)
    {
        var target = FindCharacterAt(destination);

        if (target is null)
        {
            LogMessage("Nessun bersaglio su quella cella.");
            return;
        }

        if (!Enemies.Contains(target))
        {
            LogMessage($"{attacker.Name} non può attaccare {target.Name}: non è un nemico.");
            return;
        }

        var attackResolved = PerformAttack(attacker, target);

        if (!attackResolved)
        {
            AwaitingAttackTarget = true;
            return;
        }

        AwaitingAttackTarget = false;
    }

    private Character? FindCharacterAt(GridPosition position)
    {
        return _combatGrid?.GetOccupant(position);
    }

    internal static void LogMessage(string message)
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

    // Grid initialization and rendering methods

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
        grid.SetTile(new GridPosition(5, 5), TileType.Difficult);
        grid.SetTile(new GridPosition(5, 6), TileType.Difficult);

        _combatGrid = grid;
        LogMessage($"Griglia di combattimento inizializzata: {DefaultGridWidth}x{DefaultGridHeight}.");
        ShowBattleGrid();
    }

    private void PlaceInitialCombatants()
    {
        if (_combatGrid is null)
        {
            LogMessage("Impossibile posizionare combattenti: griglia non pronta.");
            return;
        }

        var playerSpawns = new List<GridPosition>
        {
            new(2, 2), new(3, 2), new(2, 3), new(3, 3), new(2, 4), new(3, 4),
        };

        var enemySpawns = new List<GridPosition>
        {
            new(7, 7), new(6, 7), new(7, 6), new(6, 6), new(7, 5), new(6, 5),
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
                LogMessage($"Nessuna cella libera per {combatant.Name} ({factionLabel}).");
            }
        }
    }

    private void EnsureBattleTileLayerReady()
    {
        _battleTileLayer ??= GetNodeOrNull<TileMapLayer>("BattleGrid/GridLayer");

        if (_battleTileLayer is null)
        {
            GD.PushWarning("TileMapLayer BattleGrid/GridLayer non trovata.");
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

            if (!_battleTileSet.HasSource(DifficultTileSourceId))
            {
                _battleTileSet.AddSource(CreateSolidTileSource(DifficultTileColor), DifficultTileSourceId);
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
        tileSet.AddSource(CreateSolidTileSource(DifficultTileColor), DifficultTileSourceId);

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
                var sourceId = tileType switch
                {
                    TileType.Obstacle => ObstacleTileSourceId,
                    TileType.Difficult => DifficultTileSourceId,
                    _ => EmptyTileSourceId,
                };
                _battleTileLayer.SetCell(new Vector2I(x, y), sourceId, Vector2I.Zero, 0);
            }
        }
    }

    private void ShowBattleGrid()
    {
        EnsureBattleTileLayerReady();

        if (_battleTileLayer is not null)
        {
            _battleTileLayer.Visible = true;
            if (_battleTileLayer.GetParent() is CanvasItem canvasParent)
            {
                canvasParent.Visible = true;
            }
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
        if (_battleTileLayer.GetParent() is CanvasItem canvasParent)
        {
            canvasParent.Visible = false;
        }
    }

    private readonly record struct InitiativeEntry(Character Combatant, int Total, int DexterityScore, float TieBreaker);
}