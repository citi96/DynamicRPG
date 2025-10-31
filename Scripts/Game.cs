using Godot;

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

    [Export]
    public string ExplorationScenePath { get; set; } = "res://scenes/Exploration.tscn";

    [Export]
    public string CombatScenePath { get; set; } = "res://scenes/Combat.tscn";

    public GameState CurrentState { get; private set; } = GameState.Exploration;

    public override void _Ready()
    {
        GD.Print("Game inizializzato");
        StartExploration();
    }

    private void StartExploration()
    {
        CurrentState = GameState.Exploration;
        LoadScene(ExplorationScenePath);
    }

    public void LoadScene(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            GD.PushWarning("LoadScene chiamato con un percorso vuoto.");
            return;
        }

        var error = GetTree().ChangeSceneToFile(path);
        if (error != Error.Ok)
        {
            GD.PushError($"Impossibile caricare la scena: {path} (errore {error}).");
        }
    }

    public void EnterCombat()
    {
        if (CurrentState == GameState.Combat)
        {
            return;
        }

        CurrentState = GameState.Combat;
        LoadScene(CombatScenePath);
    }

    public void ExitCombat()
    {
        if (CurrentState != GameState.Combat)
        {
            return;
        }

        CurrentState = GameState.Exploration;
        LoadScene(ExplorationScenePath);
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
