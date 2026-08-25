using DocumentFormat.OpenXml.Packaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace NantoNBai.Rendering
{
    /// <summary>グラフのパーツを描画用の中間表現に読み替える。縦棒グラフだけを扱う。</summary>
    internal sealed class ChartReader
    {
        private readonly SlideTheme _theme;

        public ChartReader(SlideTheme theme) => _theme = theme;

        public BarChart Read(ChartPart chartPart)
        {
            var chart = chartPart.ChartSpace.GetFirstChild<C.Chart>()
                ?? throw new InvalidOperationException("グラフの本体がない");
            var plotArea = chart.PlotArea
                ?? throw new InvalidOperationException("グラフにプロット領域がない");

            var barChart = plotArea.GetFirstChild<C.BarChart>()
                ?? throw new NotSupportedException("縦棒グラフ以外は未対応");

            var categories = new List<string>();
            var series = new List<BarSeries>();

            foreach (var item in barChart.Elements<C.BarChartSeries>())
            {
                var name = item.SeriesText?.StringReference?.StringCache?
                    .Elements<C.StringPoint>().FirstOrDefault()?.NumericValue?.Text ?? string.Empty;

                var color = _theme.ResolveFill(item.ChartShapeProperties?.GetFirstChild<A.SolidFill>())
                    ?? SKColors.Gray;

                var values = item.GetFirstChild<C.Values>()?.NumberReference?.NumberingCache?
                    .Elements<C.NumericPoint>()
                    .Select(point => double.Parse(point.NumericValue?.Text ?? "0", CultureInfo.InvariantCulture))
                    .ToList() ?? new List<double>();

                series.Add(new BarSeries(name, color, values));

                if (categories.Count == 0)
                {
                    categories.AddRange(item.GetFirstChild<C.CategoryAxisData>()?.StringReference?.StringCache?
                        .Elements<C.StringPoint>()
                        .Select(point => point.NumericValue?.Text ?? string.Empty)
                        ?? Enumerable.Empty<string>());
                }
            }

            if (series.Count == 0)
            {
                throw new InvalidOperationException("グラフに系列がない");
            }

            var valueAxis = plotArea.GetFirstChild<C.ValueAxis>();
            var categoryAxis = plotArea.GetFirstChild<C.CategoryAxis>();

            var gridlineColor = _theme.ResolveFill(valueAxis?.MajorGridlines?.ChartShapeProperties?
                .GetFirstChild<A.Outline>()?.GetFirstChild<A.SolidFill>()) ?? new SKColor(0xD9, 0xD9, 0xD9);
            var axisLineColor = _theme.ResolveFill(categoryAxis?.ChartShapeProperties?
                .GetFirstChild<A.Outline>()?.GetFirstChild<A.SolidFill>()) ?? gridlineColor;

            var axisTextStyle = ReadTextStyle(valueAxis?.TextProperties);

            return new BarChart(
                series,
                categories,
                barChart.GetFirstChild<C.GapWidth>()?.Val?.Value ?? 150,
                barChart.GetFirstChild<C.Overlap>()?.Val?.Value ?? 0,
                axisTextStyle,
                gridlineColor,
                axisLineColor,
                ReadPlotAreaLayout(plotArea.Layout),
                chart.Legend is not null);
        }

        private TextStyle ReadTextStyle(C.TextProperties? properties)
        {
            var defaultRun = properties?.GetFirstChild<A.Paragraph>()?
                .ParagraphProperties?.GetFirstChild<A.DefaultRunProperties>();

            var sizePoints = (defaultRun?.FontSize?.Value ?? 1000) / 100f;
            var color = _theme.ResolveFill(defaultRun?.GetFirstChild<A.SolidFill>())
                ?? new SKColor(0x59, 0x59, 0x59);

            return new TextStyle(sizePoints, color, defaultRun?.Bold?.Value ?? false);
        }

        // プロット領域の位置はテンプレート側の c:manualLayout に持たせている。
        // 自動レイアウトの再現に頼らないことで、描画器に座標のマジックナンバーを置かずに済む。
        private static PlotAreaLayout? ReadPlotAreaLayout(C.Layout? layout)
        {
            var manual = layout?.ManualLayout;
            if (manual is null)
            {
                return null;
            }

            var x = manual.GetFirstChild<C.Left>()?.Val?.Value;
            var y = manual.GetFirstChild<C.Top>()?.Val?.Value;
            var width = manual.GetFirstChild<C.Width>()?.Val?.Value;
            var height = manual.GetFirstChild<C.Height>()?.Val?.Value;

            if (x is null || y is null || width is null || height is null)
            {
                return null;
            }

            return new PlotAreaLayout((float)x, (float)y, (float)width, (float)height);
        }
    }
}
