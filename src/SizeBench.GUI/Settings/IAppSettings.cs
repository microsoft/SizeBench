namespace SizeBench.GUI.Settings;

public interface IAppSettings
{
    bool UseSymbolServer { get; set; }

    IList<string> SymbolServerPaths { get; }

    void SetSymbolServerPaths(IEnumerable<string> paths);
}
