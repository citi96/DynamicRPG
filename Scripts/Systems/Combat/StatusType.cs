using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace DynamicRPG.Systems.Combat;

/// <summary>
/// Tipi di status alterati che possono influenzare un personaggio durante il combattimento.
/// Alcuni effetti (es. <see cref="StatusType.Invisible"/> o <see cref="StatusType.Charmed"/>)
/// richiederanno logiche dedicate dell'IA in futuro e per ora sono solamente tracciati.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1028:Enum Storage should be Int32",
    Justification = "Il tipo int predefinito è adeguato per gli status di gameplay.")]
public enum StatusType
{
    /// <summary>Stordito - perde completamente il turno corrente.</summary>
    Stunned,

    /// <summary>A terra - richiede tempo per rialzarsi, movimento ridotto all'inizio del turno.</summary>
    Prone,

    /// <summary>Sanguinante - infligge danni periodici nel tempo.</summary>
    Bleeding,

    /// <summary>Avvelenato - infligge danni periodici e potenziali malus ai tiri (da implementare).</summary>
    Poisoned,

    /// <summary>In fiamme - infligge danni da fuoco a fine turno.</summary>
    Burning,

    /// <summary>Congelato - blocca completamente le azioni del personaggio.</summary>
    Frozen,

    /// <summary>Rallentato - riduce la capacità di movimento per il turno corrente.</summary>
    Slowed,

    /// <summary>Accelerato - fornisce movimento aggiuntivo per il turno corrente.</summary>
    Hasted,

    /// <summary>Invisibile - i nemici dovrebbero ignorare il bersaglio (logica IA futura).</summary>
    Invisible,

    /// <summary>Silenziato - impedisce l'uso di incantesimi (gestione prevista con il sistema magia).</summary>
    Silenced,

    /// <summary>Affascinato - modifica la fazione temporaneamente (da implementare).</summary>
    Charmed,

    /// <summary>In panico - il bersaglio tende a fuggire (logica IA futura).</summary>
    Panicked,

    /// <summary>Esausto - riduce l'efficacia generale (verrà gestito in sistemi successivi).</summary>
    Exhausted,
}
