using SizeBench.AnalysisEngine;

namespace SizeBench.ExcelExporter;

public interface IExcelExporter
{
    void ExportToExcel<T>(IReadOnlyList<T> items,
                          ISessionWithProgress session,
                          string currentDeeplink,
                          string currentPageTitle,
                          IProgress<SessionTaskProgress> progressReporter,
                          CancellationToken token);

    void ExportToExcelPreformatted(IList<string> columnHeaders,
                                   IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData,
                                   ISessionWithProgress session,
                                   string deeplink,
                                   string currentPageTitle,
                                   IProgress<SessionTaskProgress> progressReporter,
                                   CancellationToken token);
}
