using System.Runtime.CompilerServices;

[assembly: CLSCompliant(false)]
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

[assembly: InternalsVisibleTo("SizeBench.GUI.Tests")]
[assembly: InternalsVisibleTo("SizeBench.AnalysisEngine.Tests")]
