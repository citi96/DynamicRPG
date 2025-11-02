using System;

#nullable enable

namespace DynamicRPG.Systems.Combat;

/// <summary>
/// Tipi di status alterati che possono affliggere o potenziare un personaggio.
/// </summary>
public enum StatusType
{
    /// <summary>Stordito - perde il turno</summary>
    Stunned,

    /// <summary>A terra - dimezza movimento, malus difesa</summary>
    Prone,

    /// <summary>Sanguinante - danno periodico</summary>
    Bleeding,

    /// <summary>Avvelenato - danno periodico, malus tiri</summary>
    Poisoned,

    /// <summary>In fiamme - danno da fuoco periodico</summary>
    Burning,

    /// <summary>Congelato - impossibilitato a muoversi</summary>
    Frozen,

    /// <summary>Rallentato - movimento ridotto</summary>
    Slowed,

    /// <summary>Accelerato - movimento aumentato</summary>
    Hasted,

    /// <summary>Invisibile - nemici non possono bersagliare</summary>
    Invisible,

    /// <summary>Silenziato - non può lanciare incantesimi</summary>
    Silenced,

    /// <summary>Affascinato - cambia temporaneamente fazione</summary>
    Charmed,

    /// <summary>In panico - fugge dal combattimento</summary>
    Panicked,

    /// <summary>Esausto - penalità generali</summary>
    Exhausted,
}

/// <summary>
/// Rappresenta un effetto di stato temporaneo applicato a un personaggio.
/// </summary>
public sealed class StatusEffect
{
    public StatusEffect(StatusType type, int duration, int potency = 0)
    {
        if (duration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "La durata non può essere negativa.");
        }

        Type = type;
        RemainingDuration = duration;
        Potency = potency;
    }

    /// <summary>
    /// Tipo di status effect.
    /// </summary>
    public StatusType Type { get; }

    /// <summary>
    /// Turni rimanenti prima che l'effetto svanisca.
    /// </summary>
    public int RemainingDuration { get; set; }

    /// <summary>
    /// Intensità dell'effetto (es: danni per turno di Bleeding).
    /// </summary>
    public int Potency { get; set; }

    /// <summary>
    /// Decrementa la durata rimanente di 1 turno.
    /// </summary>
    /// <returns>True se l'effetto è scaduto.</returns>
    public bool DecrementDuration()
    {
        if (RemainingDuration > 0)
        {
            RemainingDuration--;
        }
        return RemainingDuration == 0;
    }

    /// <summary>
    /// Restituisce una descrizione leggibile dell'effetto.
    /// </summary>
    public override string ToString() => $"{Type}({RemainingDuration})";
}