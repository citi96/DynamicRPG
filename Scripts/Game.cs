using Godot;
using System.ComponentModel;

namespace DynamicRPG;

public partial class Game : Node
{
    public enum GameState
    {
        Exploration,
        Combat,
        Dialog,
        Pause
    }

    [Export] public PackedScene ExplorationScene { get; set; } = default!;

    [Export] public PackedScene CombatScene { get; set; } = default;

    public GameState CurrentState { get; private set; } = GameState.Exploration;

    public override void _Ready()
    {
        GD.Print("Game inizializzato");
        StartExploration();
    }

    private void StartExploration()
    {
        CurrentState = GameState.Exploration;
        LoadScene(ExplorationScene);
    }

    public void LoadScene(PackedScene scene)
    {
        if (scene == null)
        {
            GD.PushWarning("LoadScene chiamato con un percorso vuoto.");
            return;
        }

        var error = GetTree().ChangeSceneToPacked(scene);
        if (error != Error.Ok)
        {
            GD.PushError($"Impossibile caricare la scena: {scene} (errore {error}).");
        }
    }

    public void EnterCombat()
    {
        if (CurrentState == GameState.Combat)
        {
            return;
        }

        CurrentState = GameState.Combat;
        LoadScene(CombatScene);
    }

    public void ExitCombat()
    {
        if (CurrentState != GameState.Combat)
        {
            return;
        }

        CurrentState = GameState.Exploration;
        LoadScene(ExplorationScene);
    }

    public void EnterDialog()
    {
        CurrentState = GameState.Dialog;
    }

    public void PauseGame()
    {
        CurrentState = GameState.Pause;
        GetTree().Paused = true;
    }

    public void ResumeGame()
    {
        if (CurrentState == GameState.Pause)
        {
            CurrentState = GameState.Exploration;
        }

        GetTree().Paused = false;
    }
}
