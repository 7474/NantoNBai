using DocumentFormat.OpenXml.Packaging;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;
using X = DocumentFormat.OpenXml.Spreadsheet;

namespace NantoNBai
{
    /// <summary>
    /// テンプレートの pptx を Open XML SDK で編集する。
    /// </summary>
    /// <remarks>
    /// 触るのはタイトルの文字列と、グラフの系列値・項目名だけ。
    /// run はそのまま残して <c>a:t</c> の中身を入れ替えるので、
    /// テンプレートのマスターから継承している書式 (44pt / 見出しフォント) が壊れない。
    /// </remarks>
    public class NantoNBaiOpenXml : INantoNBaiService
    {
        public Stream Generate(string baseDirectoryPath, string target, double from, double to, Nan nan, string contentType)
        {
            // XXX check contentType

            // 増加と減少で矢印の向きが違うテンプレートを使い分ける
            var templatePath = Path.Combine(
                baseDirectoryPath,
                $"nanto-n-bai-template{(from > to ? "(1)" : "")}.pptx");

            // テンプレートそのものは書き換えず、複製を編集する
            var ms = new MemoryStream();
            using (var template = File.OpenRead(templatePath))
            {
                template.CopyTo(ms);
            }

            ms.Position = 0;

            using (var document = PresentationDocument.Open(ms, true))
            {
                var slidePart = document.PresentationPart?.SlideParts.FirstOrDefault()
                    ?? throw new InvalidOperationException("テンプレートにスライドがない");

                SetTitle(slidePart, $"なんと{target}が{new Formatter().Format(from, to, nan)}に！");

                foreach (var chartPart in slidePart.ChartParts)
                {
                    SetChartData(chartPart, target, from, to);
                }
            }

            ms.Position = 0;

            return ms;
        }

        private static void SetTitle(SlidePart slidePart, string text)
        {
            var title = slidePart.Slide.Descendants<P.Shape>().FirstOrDefault(shape =>
                shape.NonVisualShapeProperties?
                    .ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value
                        == P.PlaceholderValues.Title)
                ?? throw new InvalidOperationException("テンプレートにタイトルがない");

            var paragraph = title.TextBody?.GetFirstChild<A.Paragraph>()
                ?? throw new InvalidOperationException("タイトルに段落がない");

            var runs = paragraph.Elements<A.Run>().ToList();
            var run = runs.FirstOrDefault()
                ?? throw new InvalidOperationException("タイトルに文字列がない");

            // テンプレートのタイトルは 1 つの run にしてある。
            // 分かれていると差し替えが追記になってしまうので、余りは落とす。
            foreach (var extra in runs.Skip(1))
            {
                extra.Remove();
            }

            run.Text = new A.Text(text);
        }

        private static void SetChartData(ChartPart chartPart, string category, double from, double to)
        {
            var series = chartPart.ChartSpace.Descendants<C.BarChartSeries>().ToList();
            if (series.Count != 2)
            {
                throw new InvalidOperationException($"テンプレートの系列数が想定と違う: {series.Count}");
            }

            SetSeries(series[0], category, from);
            SetSeries(series[1], category, to);

            UpdateEmbeddedWorkbook(chartPart, category, from, to);
        }

        private static void SetSeries(C.BarChartSeries series, string category, double value)
        {
            var categoryPoint = series.GetFirstChild<C.CategoryAxisData>()?
                .StringReference?.StringCache?.Elements<C.StringPoint>().FirstOrDefault()
                ?? throw new InvalidOperationException("系列に項目名がない");
            categoryPoint.NumericValue = new C.NumericValue(category);

            var valuePoint = series.GetFirstChild<C.Values>()?
                .NumberReference?.NumberingCache?.Elements<C.NumericPoint>().FirstOrDefault()
                ?? throw new InvalidOperationException("系列に値がない");
            valuePoint.NumericValue = new C.NumericValue(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// グラフが持っているデータシートも合わせて書き換える。
        /// 描画はグラフのキャッシュ値だけを見るが、
        /// pptx を PowerPoint で開いて「データの編集」をしたときにここが古いと困る。
        /// </summary>
        private static void UpdateEmbeddedWorkbook(ChartPart chartPart, string category, double from, double to)
        {
            var package = chartPart.EmbeddedPackagePart;
            if (package is null)
            {
                return;
            }

            using var stream = package.GetStream(FileMode.Open, FileAccess.ReadWrite);
            using var workbook = SpreadsheetDocument.Open(stream, true);

            var worksheetPart = workbook.WorkbookPart?.WorksheetParts.FirstOrDefault();
            if (worksheetPart is null)
            {
                return;
            }

            SetCellText(worksheetPart, "A2", category);
            SetCellNumber(worksheetPart, "B2", from);
            SetCellNumber(worksheetPart, "C2", to);

            worksheetPart.Worksheet.Save();
        }

        // 共有文字列表を触らずに済むよう、文字列はインラインで持たせる
        private static void SetCellText(WorksheetPart worksheetPart, string reference, string value)
        {
            var cell = FindCell(worksheetPart, reference);
            if (cell is null)
            {
                return;
            }

            cell.DataType = X.CellValues.String;
            cell.CellValue = new X.CellValue(value);
        }

        private static void SetCellNumber(WorksheetPart worksheetPart, string reference, double value)
        {
            var cell = FindCell(worksheetPart, reference);
            if (cell is null)
            {
                return;
            }

            cell.DataType = X.CellValues.Number;
            cell.CellValue = new X.CellValue(value.ToString(CultureInfo.InvariantCulture));
        }

        private static X.Cell? FindCell(WorksheetPart worksheetPart, string reference)
            => worksheetPart.Worksheet.Descendants<X.Cell>()
                .FirstOrDefault(cell => cell.CellReference?.Value == reference);
    }
}
