namespace KaneO.HashMe.App;

public sealed class HashMeSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool AlwaysOnTop { get; set; } = true;
    public bool LockPosition { get; set; }
    public bool StartWithWindows { get; set; }
    public string Theme { get; set; } = ThemePalette.HashMeDarkName;
    public double? Left { get; set; }
    public double? Top { get; set; }
}
