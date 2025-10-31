namespace DynamicRPG.Characters;

using System;

#nullable enable

/// <summary>
/// Minimal placeholder representation for a non-player character.
/// </summary>
[Serializable]
public class NPC
{
    /// <summary>
    /// Display name of the NPC.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
