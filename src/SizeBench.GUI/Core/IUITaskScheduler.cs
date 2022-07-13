using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;

namespace SizeBench.GUI.Core;

public interface IUITaskScheduler
{
    Task StartLongRunningUITask(string taskName, Func<CancellationToken, Task> task);

    Task StartExcelExport<T>(IExcelExporter excelExporter, IReadOnlyList<T>? items);

    Task StartExcelExportWithPreformattedData(IExcelExporter excelExporter,
                                              IList<string> columnHeaders,
                                              IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData);
}
