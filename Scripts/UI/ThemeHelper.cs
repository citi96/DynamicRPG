using Godot;

#nullable enable

namespace DynamicRPG.UI;

/// <summary>
/// Provides helper methods to build consistent UI themes at runtime.
/// </summary>
public static class ThemeHelper
{
    private const string MoveIconPath = "res://assets/ui/icons/move.png";
    private const string AttackIconPath = "res://assets/ui/icons/attack.png";
    private const string EndTurnIconPath = "res://assets/ui/icons/end_turn.png";

    private static Theme? _cachedTheme;
    private static Texture2D? _moveIcon;
    private static Texture2D? _attackIcon;
    private static Texture2D? _endTurnIcon;

    /// <summary>
    /// Applies the shared theme to the provided control hierarchy.
    /// </summary>
    public static void ApplySharedTheme(Control control)
    {
        control.Theme = GetSharedTheme();
    }

    /// <summary>
    /// Applies the shared theme to the control and ascends the parent chain so that
    /// container panels share the same look.
    /// </summary>
    /// <param name="control">The control used as the starting point for theming.</param>
    public static void ApplySharedThemeHierarchy(Control control)
    {
        var theme = GetSharedTheme();
        var current = control;

        while (true)
        {
            current.Theme = theme;

            if (current.GetParent() is not Control parent)
            {
                break;
            }

            current = parent;
        }
    }

    /// <summary>
    /// Loads the move action icon.
    /// </summary>
    public static Texture2D? GetMoveIcon() => _moveIcon ??= LoadTexture(MoveIconPath);

    /// <summary>
    /// Loads the attack action icon.
    /// </summary>
    public static Texture2D? GetAttackIcon() => _attackIcon ??= LoadTexture(AttackIconPath);

    /// <summary>
    /// Loads the end turn action icon.
    /// </summary>
    public static Texture2D? GetEndTurnIcon() => _endTurnIcon ??= LoadTexture(EndTurnIconPath);

    private static Theme GetSharedTheme()
    {
        if (_cachedTheme is not null)
        {
            return _cachedTheme;
        }

        var theme = new Theme();
        ConfigurePanels(theme);
        ConfigureButtons(theme);
        ConfigureLabels(theme);
        ConfigureRichText(theme);

        _cachedTheme = theme;
        return theme;
    }

    private static void ConfigurePanels(Theme theme)
    {
        var panel = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.09f, 0.11f, 0.88f),
            BorderColor = new Color(0.24f, 0.59f, 0.47f, 0.9f),
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
        };

        theme.SetStylebox("panel", "Panel", panel);
        theme.SetStylebox("panel", "PanelContainer", panel);
    }

    private static void ConfigureButtons(Theme theme)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.18f, 0.23f, 0.96f),
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            BorderColor = new Color(0.27f, 0.54f, 0.42f, 0.9f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };

        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(0.2f, 0.26f, 0.33f, 0.96f);
        hover.BorderColor = new Color(0.39f, 0.76f, 0.59f, 0.95f);

        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = new Color(0.1f, 0.14f, 0.19f, 1f);
        pressed.BorderColor = new Color(0.31f, 0.65f, 0.5f, 0.95f);

        theme.SetStylebox("normal", "Button", normal);
        theme.SetStylebox("hover", "Button", hover);
        theme.SetStylebox("pressed", "Button", pressed);
        theme.SetStylebox("focus", "Button", hover);
        theme.SetConstant("h_separation", "Button", 12);
    }

    private static void ConfigureLabels(Theme theme)
    {
        theme.SetColor("font_color", "Label", new Color(0.92f, 0.96f, 0.97f));
        theme.SetColor("font_color", "Button", new Color(0.93f, 0.97f, 0.98f));
        theme.SetColor("font_color", "RichTextLabel", new Color(0.92f, 0.96f, 0.97f));
    }

    private static void ConfigureRichText(Theme theme)
    {
        theme.SetColor("default_color", "RichTextLabel", new Color(0.92f, 0.96f, 0.97f));
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (!ResourceLoader.Exists(path, "Texture2D"))
        {
            GD.PushWarning($"Impossibile caricare la texture: {path}. Sostituisci il placeholder .txt con il file PNG richiesto.");
            return null;
        }

        return ResourceLoader.Load<Texture2D>(path);
    }
}
