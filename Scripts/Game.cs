using Godot;

namespace DynamicRPG;

public partial class Game : Node2D
{
    public override void _Ready()
    {
        GD.Print("Game Started");
    }

    public override void _Process(double delta)
    {
        // Gestisci input generali o aggiornamenti per frame qui in futuro.
    }
}
