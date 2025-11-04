using System;

#nullable enable

namespace DynamicRPG.Systems.Combat;

/// <summary>
/// Descrive un effetto di stato persistente applicato a un personaggio.
/// </summary>
public sealed class StatusEffect
{
    public StatusEffect(StatusType type, int duration, int potency = 0)
    {
        if (duration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "La durata deve essere positiva.");
        }

        if (potency < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(potency), potency, "La potenza non può essere negativa.");
        }

        Type = type;
        RemainingDuration = duration;
        Potency = potency;
    }

    /// <summary>
    /// Tipo di status applicato.
    /// </summary>
    public StatusType Type { get; }

    /// <summary>
    /// Numero di turni residui prima che l'effetto svanisca.
    /// </summary>
    public int RemainingDuration { get; set; }

    /// <summary>
    /// Intensità dell'effetto (ad esempio danni per turno).
    /// </summary>
    public int Potency { get; set; }

    /// <summary>
    /// Decrementa la durata residua di un turno.
    /// </summary>
    /// <returns><c>true</c> se l'effetto è terminato.</returns>
    public bool DecrementDuration()
    {
        if (RemainingDuration > 0)
        {
            RemainingDuration--;
        }

        return RemainingDuration <= 0;
    }

    public override string ToString() => $"{Type}({RemainingDuration})";
}
