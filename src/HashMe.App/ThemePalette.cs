using System.Windows.Media;

namespace KaneO.HashMe.App;

public static class ThemePalette
{
    public const string HashMeDarkName = "HashMe Dark";
    public const string GraphiteName = "Graphite";
    public const string LightName = "Light";
    public const string HighContrastName = "High Contrast";

    public static string NormalizeName(string? name) => name switch
    {
        GraphiteName => GraphiteName,
        LightName => LightName,
        HighContrastName => HighContrastName,
        HashMeDarkName => HashMeDarkName,
        _ => HashMeDarkName
    };

    public static ThemeDefinition Get(string? name) => NormalizeName(name) switch
    {
        GraphiteName => new ThemeDefinition(GraphiteName, "#F22D2F33", "#FF3A3D42", "#FF6E737A", "#FFF7F7F7", "#FFC2C5C9", "#FF4B4F55"),
        LightName => new ThemeDefinition(LightName, "#F2F4F7FA", "#FFFFFFFF", "#FF477B92", "#FF0D1B24", "#FF526875", "#FFDCEAF0"),
        HighContrastName => new ThemeDefinition(HighContrastName, "#FF000000", "#FF101010", "#FFFFFF00", "#FFFFFFFF", "#FFFFFF00", "#FF252500"),
        _ => new ThemeDefinition(HashMeDarkName, "#F2071827", "#FF0D2334", "#FF147AA6", "#FFF4FBFF", "#FF8EBDD1", "#FF15374D")
    };

    public static SolidColorBrush Brush(string value)
    {
        var brush = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}

public sealed record ThemeDefinition(
    string Name,
    string Panel,
    string Field,
    string Border,
    string Text,
    string MutedText,
    string Hover);
