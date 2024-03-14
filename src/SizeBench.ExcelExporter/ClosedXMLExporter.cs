using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using ClosedXML.Excel;
using SizeBench.AnalysisEngine;

namespace SizeBench.ExcelExporter;

internal sealed class ClosedXMLExporter : IExcelExporter
{
    private const int MaxExcelCellLength = 32_767;

    private static string TruncateStringToExcelMaxLength(string str) =>
        str.Length > MaxExcelCellLength ? String.Concat(str.AsSpan(0, MaxExcelCellLength - "...".Length), "...") : str;

    [ExcludeFromCodeCoverage]
    public void ExportToExcel<T>(IReadOnlyList<T> items, ISessionWithProgress session, string currentDeeplink, string currentPageTitle, IProgress<SessionTaskProgress> progressReporter, CancellationToken token)
    {
        // If there's no items, there's nothing to export.
        if (items is null || items.Count == 0)
        {
            return;
        }

        var tempExportFilePath = Path.GetTempFileName() + ".xlsx";

        using (var workbook = new XLWorkbook())
        {
            var dataWorksheet = workbook.Worksheets.Add("Exported Data");
            var metadataWorksheet = workbook.Worksheets.Add("Metadata");

            if (session is ISession sess)
            {
                FillInMetadataWorksheet(sess, currentDeeplink, currentPageTitle, metadataWorksheet);
            }
            else if (session is IDiffSession diffSession)
            {
                FillInMetadataWorksheet(diffSession, currentDeeplink, currentPageTitle, metadataWorksheet);
            }

            FillInDataWorksheet(items, dataWorksheet, progressReporter, token);

            workbook.SaveAs(tempExportFilePath);
        }

        Process.Start(new ProcessStartInfo()
        {
            FileName = tempExportFilePath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Maximized
        });
    }

    [ExcludeFromCodeCoverage]
    public void ExportToExcelPreformatted(IList<string> columnHeaders, IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData, ISessionWithProgress session, string currentDeeplink, string currentPageTitle, IProgress<SessionTaskProgress> progressReporter, CancellationToken token)
    {
        // If there's no items, there's nothing to export.
        if (preformattedData is null || preformattedData.Count == 0)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(columnHeaders);

        var tempExportFilePath = Path.GetTempFileName() + ".xlsx";

        using (var workbook = new XLWorkbook())
        {
            var dataWorksheet = workbook.Worksheets.Add("Exported Data");
            var metadataWorksheet = workbook.Worksheets.Add("Metadata");

            var dataRows = new object[preformattedData.Count, columnHeaders.Count];

            for (var i = 0; i < preformattedData.Count; i++)
            {
                var item = preformattedData[i];
                for (var j = 0; j < columnHeaders.Count; j++)
                {
                    dataRows[i, j] = item[columnHeaders[j]];
                }
            }

            FillInDataWorksheet_Formatted(preformattedData.Count, dataWorksheet, columnHeaders.ToArray(), dataRows);
            if (session is ISession sess)
            {
                FillInMetadataWorksheet(sess, currentDeeplink, currentPageTitle, metadataWorksheet);
            }
            else if (session is IDiffSession diffSession)
            {
                FillInMetadataWorksheet(diffSession, currentDeeplink, currentPageTitle, metadataWorksheet);
            }

            workbook.SaveAs(tempExportFilePath);
        }

        Process.Start(new ProcessStartInfo()
        {
            FileName = tempExportFilePath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Maximized
        });
    }

    [ExcludeFromCodeCoverage]
    private static void FillInMetadataWorksheet(ISession session, string currentDeeplink, string currentPageTitle, IXLWorksheet metadataWorksheet)
    {
        metadataWorksheet.Cell(1, 1).Value = "Exported data is from...";

        metadataWorksheet.Cell(2, 1).Value = TruncateStringToExcelMaxLength(currentDeeplink);
        metadataWorksheet.Cell(2, 1).SetHyperlink(new XLHyperlink(currentDeeplink));

        metadataWorksheet.Cell(3, 1).Value = "Page Title:";
        metadataWorksheet.Cell(3, 2).Value = TruncateStringToExcelMaxLength(currentPageTitle);

        metadataWorksheet.Cell(4, 1).Value = "Binary Path:";
        metadataWorksheet.Cell(4, 2).Value = TruncateStringToExcelMaxLength(session.BinaryPath);

        metadataWorksheet.Cell(5, 1).Value = "PDB Path:";
        metadataWorksheet.Cell(5, 2).Value = TruncateStringToExcelMaxLength(session.PdbPath);

        metadataWorksheet.Column(1).Width = 12;
    }

    [ExcludeFromCodeCoverage]
    private static void FillInMetadataWorksheet(IDiffSession session, string currentDeeplink, string currentPageTitle, IXLWorksheet metadataWorksheet)
    {
        metadataWorksheet.Cell(1, 1).Value = "Exported data is from a diff between...";

        metadataWorksheet.Cell(2, 1).Value = TruncateStringToExcelMaxLength(currentDeeplink);
        metadataWorksheet.Cell(2, 1).SetHyperlink(new XLHyperlink(currentDeeplink));

        metadataWorksheet.Cell(3, 1).Value = "Page Title:";
        metadataWorksheet.Cell(3, 2).Value = TruncateStringToExcelMaxLength(currentPageTitle);

        metadataWorksheet.Cell(4, 1).Value = "Before Binary Path: ";
        metadataWorksheet.Cell(4, 2).Value = TruncateStringToExcelMaxLength(session.BeforeSession.BinaryPath);

        metadataWorksheet.Cell(5, 1).Value = "Before PDB Path: ";
        metadataWorksheet.Cell(5, 2).Value = TruncateStringToExcelMaxLength(session.BeforeSession.PdbPath);

        metadataWorksheet.Cell(6, 1).Value = "After Binary Path: ";
        metadataWorksheet.Cell(6, 2).Value = TruncateStringToExcelMaxLength(session.BeforeSession.BinaryPath);

        metadataWorksheet.Cell(7, 1).Value = "After PDB Path: ";
        metadataWorksheet.Cell(7, 2).Value = TruncateStringToExcelMaxLength(session.BeforeSession.PdbPath);

        metadataWorksheet.Column(1).Width = 25;
    }

    internal readonly struct ColumnMetadata
    {
        public readonly PropertyInfo propertyInfo;
        public readonly DisplayFormatAttribute? displayFormatAttribute;
        public readonly string columnHeader;
        public readonly int order;

        public ColumnMetadata(PropertyInfo pi, DisplayFormatAttribute? dfa, string header, int order)
        {
            this.propertyInfo = pi;
            this.displayFormatAttribute = dfa;
            this.columnHeader = header;
            this.order = order;
        }
    };

    internal static List<ColumnMetadata> DetermineColumnHeaders(Type type)
    {
        var properties = type.GetProperties();
        var columnMetadata = new List<ColumnMetadata>();
        for (var i = 0; i < properties.Length; i++)
        {
            // We don't care about pretty-printing collection types so just skip them
            // But special-case string since it is an IEnumerable of char
            if (properties[i].PropertyType != typeof(string) &&
                typeof(IEnumerable).IsAssignableFrom(properties[i].PropertyType))
            {
                continue;
            }

            var displayAttr = properties[i].GetCustomAttribute<DisplayAttribute>(true /* inherit */);

            // If AutoGenerateField == false, we'll skip this property and not output anything for it.
            if (displayAttr != null && displayAttr.GetAutoGenerateField() == false)
            {
                continue;
            }

            columnMetadata.Add(new ColumnMetadata(properties[i],
                                                  properties[i].GetCustomAttribute<DisplayFormatAttribute>(true /* inherit */),
                                                  displayAttr?.GetName() ?? properties[i].Name /* columnHeader */,
                                                  displayAttr?.GetOrder() ?? Int32.MaxValue));
        }

        columnMetadata.Sort((cm1, cm2) => cm1.order.CompareTo(cm2.order));
        return columnMetadata;
    }

    internal static object[,] GetTable<T>(IReadOnlyList<T> items,
                                          List<ColumnMetadata> columnMetadata,
                                          IProgress<SessionTaskProgress>? progressReporter,
                                          CancellationToken token)
    {
        var dataRows = new object[items.Count, columnMetadata.Count];

        for (var j = 0; j < items.Count; j++)
        {
            token.ThrowIfCancellationRequested();
            progressReporter?.Report(new SessionTaskProgress($"Exported {j}/{items.Count} items", (uint)j, (uint)items.Count));
            var item = items[j];

            for (var i = 0; i < columnMetadata.Count; i++)
            {
                var column = columnMetadata[i];
                var value = column.propertyInfo.GetValue(item);
                if (value is null &&
                    column.displayFormatAttribute != null &&
                    column.displayFormatAttribute!.NullDisplayText != null)
                {
                    dataRows[j, i] = column.displayFormatAttribute.NullDisplayText;
                }
                else if (value is null)
                {
                    dataRows[j, i] = String.Empty;
                }
                else if (column.displayFormatAttribute != null &&
                         column.displayFormatAttribute!.DataFormatString != null)
                {
                    dataRows[j, i] = String.Format(CultureInfo.InvariantCulture, column.displayFormatAttribute.DataFormatString, value);
                }
                else
                {
                    var valAsString = value.ToString();
                    if (valAsString?.Length > 500)
                    {
                        valAsString = String.Concat(valAsString.AsSpan(0, 500), "...");
                    }

                    dataRows[j, i] = valAsString ?? String.Empty;
                }
            }
        }

        return dataRows;
    }

    private static void FillInDataWorksheet<T>(IReadOnlyList<T> items,
                                               IXLWorksheet dataWorksheet,
                                               IProgress<SessionTaskProgress> progressReporter,
                                               CancellationToken token)
    {
        var headerMetadata = DetermineColumnHeaders(typeof(T));

        var dataRows = GetTable(items, headerMetadata, progressReporter, token);
        var columnHeaders = (from header in headerMetadata select header.columnHeader).ToArray();
        FillInDataWorksheet_Formatted(items.Count, dataWorksheet, columnHeaders, dataRows);
    }

    private static void FillInDataWorksheet_Formatted(int itemsCount,
                                                      IXLWorksheet dataWorksheet,
                                                      string[] columnHeaders,
                                                      object[,] dataRows)
    {
        // Write out the headers
        for (var i = 0; i < columnHeaders.Length; i++)
        {
            dataWorksheet.Cell(1, i + 1).Value = columnHeaders[i];
        }

        for (var rowIndex = 0; rowIndex < itemsCount; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < columnHeaders.Length; columnIndex++)
            {
                if (dataRows[rowIndex, columnIndex] is string str)
                {
                    dataWorksheet.Cell(2 + rowIndex, 1 + columnIndex).Value = TruncateStringToExcelMaxLength(str);
                }
                else
                {
                    dataWorksheet.Cell(2 + rowIndex, 1 + columnIndex).Value = XLCellValue.FromObject(dataRows[rowIndex, columnIndex], CultureInfo.InvariantCulture);
                }
            }
        }

        dataWorksheet.Range(dataWorksheet.Cell(1, 1), dataWorksheet.Cell(1 + itemsCount, columnHeaders.Length))
                     .CreateTable();

        dataWorksheet.Columns().AdjustToContents();
    }
}
