using System.Text;
using Godot;

#nullable enable

namespace DynamicRPG.World.Exploration;

public partial class ExplorationController : Node2D
{
    [Export]
    public Label? LocationInfoLabel { get; set; }

    [Export]
    public Label? HelpLabel { get; set; }

    [Export]
    public Marker2D? PlayerSpawn { get; set; }

    [Export]
    public Characters.PlayerController? Player { get; set; }

    [Export]
    public Rect2 PlayableArea { get; set; } = new Rect2(new Vector2(-640, -360), new Vector2(1280, 720));

    public override void _Ready()
    {
        base._Ready();

        Player ??= GetNodeOrNull<Characters.PlayerController>("World/Player");
        LocationInfoLabel ??= GetNodeOrNull<Label>("InfoLayer/Info");
        HelpLabel ??= GetNodeOrNull<Label>("InfoLayer/Help");
        PlayerSpawn ??= GetNodeOrNull<Marker2D>("World/PlayerSpawn");

        if (Player is not null)
        {
            Player.PlayableArea = PlayableArea;

            if (PlayerSpawn is not null)
            {
                Player.GlobalPosition = PlayerSpawn.GlobalPosition;
            }
        }

        UpdateLocationInfo();
        UpdateHelpLabel();
    }

    public void RefreshFromGame()
    {
        UpdateLocationInfo();
    }

    private void UpdateLocationInfo()
    {
        if (LocationInfoLabel is null)
        {
            return;
        }

        var game = DynamicRPG.Game.Instance;
        if (game is null || game.WorldRegions.Count == 0 || game.CurrentRegion is null || game.CurrentLocation is null)
        {
            LocationInfoLabel.Text = "In attesa del caricamento del mondo...";
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Regione: {game.CurrentRegion.Name}");
        builder.AppendLine($"Bioma: {game.CurrentRegion.EnvironmentType}");
        builder.AppendLine($"Fazione dominante: {game.CurrentRegion.ControllingFaction}");
        builder.AppendLine($"Location attuale: {game.CurrentLocation.Name} ({game.CurrentLocation.Type})");
        builder.AppendLine($"Connessioni disponibili: {game.CurrentLocation.ConnectedLocations.Count}");

        LocationInfoLabel.Text = builder.ToString();
    }

    private void UpdateHelpLabel()
    {
        if (HelpLabel is null)
        {
            return;
        }

        HelpLabel.Text = "WASD o frecce per muoverti. Premi Esc per uscire.";
    }
}
