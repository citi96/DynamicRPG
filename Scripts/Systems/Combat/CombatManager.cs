using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using DynamicRPG.Characters;

namespace DynamicRPG.Systems.Combat;

#nullable enable

/// <summary>
/// Coordinates the flow of a turn-based combat encounter.
/// </summary>
public sealed partial class CombatManager : Node
{
    private readonly RandomNumberGenerator _rng = new();

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

        if (!IsCombatActive)
        {
            GD.Print("Impossibile avviare il combattimento: servono almeno un alleato e un nemico vivi.");
            EndCombat();
            return;
        }

        _rng.Randomize();
        RollInitiative();

        if (TurnOrder.Count == 0)
        {
            GD.Print("Nessun partecipante valido per il combattimento.");
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
            GD.Print($"Iniziativa di {combatant.Name}: tiro {roll} + DEX {dexterityModifier} + bonus {combatant.InitiativeBonus} = {total}");
        }

        var ordered = initiativeEntries
            .OrderByDescending(entry => entry.Total)
            .ThenByDescending(entry => entry.DexterityScore)
            .ThenByDescending(entry => entry.TieBreaker)
            .Select(entry => entry.Combatant)
            .ToList();

        TurnOrder.AddRange(ordered);

        GD.Print("Ordine di turno: " + string.Join(", ", TurnOrder.Select(character => character.Name)));
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

        if (currentCombatant.CurrentHealth <= 0)
        {
            GD.Print($"{currentCombatant.Name} è stato sconfitto prima del suo turno, si passa oltre.");
            EndTurn();
            return;
        }

        GD.Print($"--- Round {RoundNumber}, turno di {currentCombatant.Name} ---");

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

        GD.Print($"{currentCombatant.Name} non appartiene più a nessuna fazione, turno saltato.");
        EndTurn();
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
        GD.Print($"{player.Name} (giocatore) esegue un'azione automatica di test.");

        var target = Enemies.FirstOrDefault(enemy => enemy.CurrentHealth > 0);
        if (target is null)
        {
            GD.Print("Nessun nemico valido da attaccare.");
            EndTurn();
            return;
        }

        ResolveBasicAttack(player, target);

        if (IsCombatActive)
        {
            EndTurn();
        }
    }

    private void ExecuteEnemyTurn(Character enemy)
    {
        GD.Print($"{enemy.Name} (nemico) agisce con l'IA di base.");

        var target = Players.FirstOrDefault(player => player.CurrentHealth > 0);
        if (target is null)
        {
            GD.Print("Nessun giocatore valido da attaccare.");
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

        GD.Print($"{attacker.Name} attacca {defender.Name} infliggendo {damage} danni. " +
                 $"HP rimanenti: {defender.CurrentHealth}/{defender.MaxHealth}");

        if (defenderWasDefeated)
        {
            GD.Print($"{defender.Name} è stato sconfitto!");
        }

        RemoveDefeatedCombatants();
        CheckBattleEnd();
    }

    private void RemoveDefeatedCombatants()
    {
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
            GD.Print("Il combattimento termina senza vincitori.");
        }
        else if (!anyEnemyAlive)
        {
            GD.Print("I giocatori vincono il combattimento!");
        }
        else
        {
            GD.Print("I giocatori sono stati sconfitti.");
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
    }

    private static int CalculateAbilityModifier(int abilityScore) => (int)Math.Floor((abilityScore - 10) / 2.0);

    private readonly record struct InitiativeEntry(Character Combatant, int Total, int DexterityScore, float TieBreaker);
}
