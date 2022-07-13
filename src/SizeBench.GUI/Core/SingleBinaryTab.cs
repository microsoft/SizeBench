using System.IO;
using Castle.Windsor;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Core;

internal sealed class SingleBinaryTab : TabBase
{
    public ISession Session { get; }

    protected override Uri HomePage => new Uri(@"SingleBinaryOverview", UriKind.Relative);

    public override string CurrentDeeplink
    {
        get
        {
            var binaryPath = Uri.EscapeDataString(this.Session.BinaryPath);
            var pdbPath = Uri.EscapeDataString(this.Session.PdbPath);
            var originalCurrentSource = this.CurrentPage.OriginalString;
            var inAppPage = Uri.EscapeDataString(originalCurrentSource);

            var deeplinkUrl = $"sizebench://2.0/{inAppPage}?BinaryPath={binaryPath}&PDBPath={pdbPath}";
            return deeplinkUrl;
        }
    }

    public override string Header => Path.GetFileNameWithoutExtension(this.Session.BinaryPath);

    public override string ToolTip => this.Session.BinaryPath;

    public override string BinaryPathForWindowTitle => this.Session.BinaryPath;

    public SingleBinaryTab(ISession session, IWindsorContainer container)
        : base(session, container)
    {
        this.Session = session;
    }
}
