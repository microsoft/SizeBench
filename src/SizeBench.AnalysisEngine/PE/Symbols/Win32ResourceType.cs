namespace SizeBench.AnalysisEngine.Symbols;

// The official names all start with "RT_" but that looks pretty ugly in the UI and since it's the standard convention, skipping it here
// just makes the names a bit more readable - they're already ugly enough to modern sensibilities, being all caps...
#pragma warning disable CA1707 // Identifiers should not contain underscores - these are the names in the Win32 rsrc spec, so keeping them as they are there despite .NET naming conventions
public enum Win32ResourceType
{
    Unknown = 0,
    CURSOR = 1,
    BITMAP = 2,
    ICON = 3,
    MENU = 4,
    DIALOG = 5,
    STRINGTABLE = 6,
    FONTDIR = 7,
    FONT = 8,
    ACCELERATOR = 9,
    RCDATA = 10,
    MESSAGETABLE = 11,
    GROUP_CURSOR = 12,
    NEWBITMAP = 13,
    GROUP_ICON = 14,
    MENUEX = 15,
    VERSION = 16,
    DLGINCLUDE = 17,
    DIALOGEX = 18,
    PLUGPLAY = 19,
    VXD = 20,
    ANICURSOR = 21,
    ANIICON = 22,
    HTML = 23,
    MANIFEST = 24,
    RIBBON_XML = 28,
    DLGINIT = 240,
    TOOLBAR = 241,

    UserNamedResource = System.Int32.MaxValue,
}
#pragma warning restore CA1707
