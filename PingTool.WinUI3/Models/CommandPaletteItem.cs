namespace PingTool.Models;

/// <summary>An entry in the global command palette (Ctrl+K) — navigates to a page when invoked.</summary>
public class CommandPaletteItem
{
    public string Title { get; }
    public string Subtitle { get; }
    public string Glyph { get; }
    public Type PageType { get; }

    public CommandPaletteItem(string title, string subtitle, string glyph, Type pageType)
    {
        Title = title;
        Subtitle = subtitle;
        Glyph = glyph;
        PageType = pageType;
    }
}
