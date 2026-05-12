using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using ClosedXML.Excel;
using FopFinance.Managers;
using FopFinance.Models;

namespace FopFinance.Services
{
    /// <summary>
    /// Bridge service responsible for report generation and export.
    /// </summary>
    public class ReportBridgeService
    {
        private readonly ReportManager _manager;
        private readonly Form _mainForm;

        public ReportBridgeService(ReportManager manager, Form mainForm)
        {
            _manager = manager;
            _mainForm = mainForm;
        }

        public string GenerateReport(string startIso, string endIso)
        {
            try
            {
                if (!DateTime.TryParse(startIso, out DateTime start))
                    return BridgeHelpers.Error("Невірна дата початку.");
                if (!DateTime.TryParse(endIso, out DateTime end))
                    return BridgeHelpers.Error("Невірна дата кінця.");

                var report = _manager.Generate(start, end);
                return JsonSerializer.Serialize(report, BridgeHelpers.JsonOpts);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string ExportReport(string reportJson, string format)
        {
            try
            {
                var report = BridgeHelpers.Deserialize<Report>(reportJson);
                if (report == null) return BridgeHelpers.Error("Некоректний звіт.");

                string ext = format.ToLowerInvariant() switch
                {
                    "xml"  => "xml",
                    "csv"  => "csv",
                    "xlsx" => "xlsx",
                    _      => "json"
                };

                string? filePath = null;
                _mainForm.Invoke(() =>
                {
                    using var dlg = new SaveFileDialog
                    {
                        Filter   = $"{ext.ToUpper()} файл|*.{ext}",
                        FileName = $"report_{DateTime.Today:yyyyMMdd}.{ext}"
                    };
                    if (dlg.ShowDialog(_mainForm) == DialogResult.OK)
                        filePath = dlg.FileName;
                });

                if (filePath == null) return BridgeHelpers.Ok("cancelled");

                if (ext == "xlsx")
                    ExportToXlsx(report, filePath);
                else
                    File.WriteAllText(filePath, ext switch
                    {
                        "xml" => report.ExportToXML(),
                        "csv" => report.ExportToCSV(),
                        _     => report.ExportToJSON()
                    });

                return BridgeHelpers.Ok(filePath);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        private static void ExportToXlsx(Report report, string filePath)
        {
            using var wb = new XLWorkbook();

            var summary = wb.Worksheets.Add("Summary");
            summary.Cell(1, 1).Value = "StartDate";   summary.Cell(1, 2).Value = report.StartDate.ToString("yyyy-MM-dd");
            summary.Cell(2, 1).Value = "EndDate";     summary.Cell(2, 2).Value = report.EndDate.ToString("yyyy-MM-dd");
            summary.Cell(3, 1).Value = "TotalIncome"; summary.Cell(3, 2).Value = report.TotalIncome;
            summary.Cell(4, 1).Value = "TotalExpense";summary.Cell(4, 2).Value = report.TotalExpense;
            summary.Cell(5, 1).Value = "NetProfit";   summary.Cell(5, 2).Value = report.NetProfit;

            var incSheet = wb.Worksheets.Add("Incomes");
            incSheet.Cell(1, 1).Value = "Date";
            incSheet.Cell(1, 2).Value = "Source";
            incSheet.Cell(1, 3).Value = "Amount";
            incSheet.Cell(1, 4).Value = "Description";
            int row = 2;
            foreach (var i in report.Incomes)
            {
                incSheet.Cell(row, 1).Value = i.Date.ToString("yyyy-MM-dd");
                incSheet.Cell(row, 2).Value = i.Source;
                incSheet.Cell(row, 3).Value = i.Amount;
                incSheet.Cell(row, 4).Value = i.Description;
                row++;
            }

            var expSheet = wb.Worksheets.Add("Expenses");
            expSheet.Cell(1, 1).Value = "Date";
            expSheet.Cell(1, 2).Value = "Category";
            expSheet.Cell(1, 3).Value = "Amount";
            expSheet.Cell(1, 4).Value = "Description";
            row = 2;
            foreach (var e in report.Expenses)
            {
                expSheet.Cell(row, 1).Value = e.Date.ToString("yyyy-MM-dd");
                expSheet.Cell(row, 2).Value = e.CategoryName;
                expSheet.Cell(row, 3).Value = e.Amount;
                expSheet.Cell(row, 4).Value = e.Description;
                row++;
            }

            summary.Columns().AdjustToContents();
            incSheet.Columns().AdjustToContents();
            expSheet.Columns().AdjustToContents();
            wb.SaveAs(filePath);
        }
    }
}
