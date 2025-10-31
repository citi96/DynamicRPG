using Godot;

namespace DynamicRPG;

public partial class Game : Node2D
{
    private Node? _world;
    private CanvasLayer? _ui;

    // Placeholder references for global singletons to be initialized later.
    private Node? _digitalGameMaster;
    private Node? _questManager;

    public override void _Ready()
    {
        _world = GetNode<Node>("World");
        _ui = GetNode<CanvasLayer>("UI");

        // TODO: Initialize the digital game master singleton when available.
        _digitalGameMaster = null;

        // TODO: Initialize the quest manager singleton when available.
        _questManager = null;

        GD.Print("Game Started");
    }

    public override void _Process(double delta)
    {
        // TODO: Update global timers or world simulation systems here.

        // TODO: Update AI systems or other per-frame managers here.
    }
}
