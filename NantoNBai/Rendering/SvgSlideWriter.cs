using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NantoNBai.Rendering
{
    /// <summary>
    /// スライドの中間表現を SVG にする。描画の実装はここだけで、PNG は SVG をラスタライズして得る。
    /// </summary>
    public sealed class SvgSlideWriter
    {
        // pt -> px (96 DPI)
        private const float PixelsPerPoint = 96f / 72f;

        public string Write(SlideDocument document)
        {
            var svg = new SvgBuilder();
            svg.Open(document.Width, document.Height);
            svg.Rect(0, 0, document.Width, document.Height, document.Background, null, 0f);

            foreach (var element in document.Elements)
            {
                switch (element)
                {
                    case TextElement text:
                        WriteText(svg, text);
                        break;
                    case ShapeElement shape:
                        WriteShape(svg, shape);
                        break;
                    case ChartElement chart:
                        WriteChart(svg, chart);
                        break;
                }
            }

            svg.Close();
            return svg.ToString();
        }

        private static void WriteText(SvgBuilder svg, TextElement element)
        {
            var box = new SKRect(
                element.Bounds.Left + element.Insets.Left,
                element.Bounds.Top + element.Insets.Top,
                element.Bounds.Right - element.Insets.Right,
                element.Bounds.Bottom - element.Insets.Bottom);

            var sizePixels = element.Style.SizePoints * PixelsPerPoint;
            using var font = EmbeddedFont.CreateFont(sizePixels);
            var metrics = font.Metrics;
            var lineHeight = (-metrics.Ascent + metrics.Descent) * element.LineSpacing;

            var lines = WrapText(font, element.Text, box.Width);
            var blockHeight = lineHeight * lines.Count;

            var top = element.Anchor switch
            {
                TextAnchor.Top => box.Top,
                TextAnchor.Bottom => box.Bottom - blockHeight,
                _ => box.MidY - blockHeight / 2f,
            };

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var width = font.MeasureText(line, (SKPaint?)null);
                var x = element.Alignment switch
                {
                    TextAlignment.Center => box.MidX - width / 2f,
                    TextAlignment.Right => box.Right - width,
                    _ => box.Left,
                };

                svg.TextPath(font, line, x, top + lineHeight * index - metrics.Ascent, element.Style.Color);
            }
        }

        /// <summary>
        /// 箱の幅に収まるように行を分ける。
        /// 長い name を入れたときに PowerPoint が折り返すのと同じ見え方にするため。
        /// </summary>
        /// <remarks>
        /// 日本語はどこでも折り返せるが、英数字の語中では折り返さない。
        /// </remarks>
        public static IReadOnlyList<string> WrapText(SKFont font, string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
            {
                return new[] { text };
            }

            var lines = new List<string>();
            var line = new StringBuilder();

            foreach (var character in text)
            {
                line.Append(character);
                if (font.MeasureText(line.ToString(), (SKPaint?)null) <= maxWidth)
                {
                    continue;
                }

                // 入り切らなかった 1 文字を次の行に送る。
                // 英数字の途中なら、その語の先頭まで戻して送る。
                line.Length--;
                var carry = character.ToString();

                if (IsWordCharacter(character))
                {
                    var breakAt = line.Length;
                    while (breakAt > 0 && IsWordCharacter(line[breakAt - 1]))
                    {
                        breakAt--;
                    }

                    if (breakAt > 0 && breakAt < line.Length)
                    {
                        carry = line.ToString(breakAt, line.Length - breakAt) + carry;
                        line.Length = breakAt;
                    }
                }

                if (line.Length > 0)
                {
                    lines.Add(line.ToString());
                }

                line.Clear();
                line.Append(carry);
            }

            if (line.Length > 0)
            {
                lines.Add(line.ToString());
            }

            return lines.Count == 0 ? new[] { string.Empty } : lines;
        }

        private static bool IsWordCharacter(char character)
            => char.IsAsciiLetterOrDigit(character) || character is '.' or ',' or '%';

        private static void WriteShape(SvgBuilder svg, ShapeElement element)
        {
            switch (element.Geometry.Kind)
            {
                case ShapeGeometryKind.Rectangle:
                    svg.Rect(
                        element.Bounds.Left,
                        element.Bounds.Top,
                        element.Bounds.Width,
                        element.Bounds.Height,
                        element.Fill,
                        element.Stroke,
                        element.StrokeWidth);
                    break;

                case ShapeGeometryKind.BentUpArrow:
                    svg.Polygon(
                        BentUpArrow(element.Bounds, element.Geometry.Adjustments),
                        element.Fill,
                        element.Stroke,
                        element.StrokeWidth,
                        Transform(element));
                    break;

                default:
                    throw new NotSupportedException($"未対応の図形: {element.Geometry.Kind}");
            }
        }

        /// <summary>
        /// prst="bentUpArrow" (右へ伸びてから上を向く矢印) の輪郭。
        /// </summary>
        /// <remarks>
        /// 調整値は図形の短辺 (ss) に対する 1/100000 単位で、
        /// adj1 = 矢印の太さ、adj2 = 矢じりの半幅、adj3 = 矢じりの高さ。
        /// PowerPoint が描いた図形の実測と一致することを確認している。
        /// </remarks>
        private static IReadOnlyList<SKPoint> BentUpArrow(SKRect bounds, IReadOnlyDictionary<string, int> adjustments)
        {
            var shortSide = Math.Min(bounds.Width, bounds.Height);
            var thickness = shortSide * Adjust(adjustments, "adj1", 25000) / 100000f;
            var headHalfWidth = shortSide * Adjust(adjustments, "adj2", 25000) / 100000f;
            var headHeight = shortSide * Adjust(adjustments, "adj3", 25000) / 100000f;

            var centerX = bounds.Right - headHalfWidth;
            var headBottom = bounds.Top + headHeight;
            var barTop = bounds.Bottom - thickness;

            return new[]
            {
                new SKPoint(bounds.Left, bounds.Bottom),
                new SKPoint(bounds.Left, barTop),
                new SKPoint(centerX - thickness / 2f, barTop),
                new SKPoint(centerX - thickness / 2f, headBottom),
                new SKPoint(centerX - headHalfWidth, headBottom),
                new SKPoint(centerX, bounds.Top),
                new SKPoint(centerX + headHalfWidth, headBottom),
                new SKPoint(centerX + thickness / 2f, headBottom),
                new SKPoint(centerX + thickness / 2f, bounds.Bottom),
            };
        }

        private static float Adjust(IReadOnlyDictionary<string, int> adjustments, string name, int fallback)
            => adjustments.TryGetValue(name, out var value) ? value : fallback;

        /// <summary>図形の反転と回転。いずれも図形の中心が基準。</summary>
        private static string? Transform(ShapeElement element)
        {
            if (element.RotationDegrees == 0f && !element.FlipHorizontal && !element.FlipVertical)
            {
                return null;
            }

            var centerX = element.Bounds.MidX;
            var centerY = element.Bounds.MidY;
            var scaleX = element.FlipHorizontal ? -1 : 1;
            var scaleY = element.FlipVertical ? -1 : 1;

            return string.Create(CultureInfo.InvariantCulture,
                $"rotate({element.RotationDegrees:0.###} {centerX:0.###} {centerY:0.###}) " +
                $"translate({centerX:0.###} {centerY:0.###}) scale({scaleX} {scaleY}) " +
                $"translate({-centerX:0.###} {-centerY:0.###})");
        }

        private static void WriteChart(SvgBuilder svg, ChartElement element)
        {
            var chart = element.Chart;
            var axis = ValueAxisScale.For(chart.Series);

            var labelSizePixels = chart.AxisTextStyle.SizePoints * PixelsPerPoint;
            using var labelFont = EmbeddedFont.CreateFont(labelSizePixels);
            var labelMetrics = labelFont.Metrics;
            var labelGap = labelSizePixels * 0.9f;

            var plot = ResolvePlotArea(element.Bounds, chart, axis, labelFont, labelSizePixels);

            // 目盛り線と目盛りラベル
            for (var index = 0; index <= axis.TickCount; index++)
            {
                var value = axis.Min + axis.Unit * index;
                var y = axis.ToY(value, plot);

                svg.Line(plot.Left, y, plot.Right, y, chart.GridlineColor, 1f);

                var text = axis.Format(value);
                var width = labelFont.MeasureText(text, (SKPaint?)null);
                svg.TextPath(
                    labelFont,
                    text,
                    plot.Left - labelGap - width,
                    y + labelMetrics.CapHeight / 2f,
                    chart.AxisTextStyle.Color);
            }

            // 項目軸はゼロの位置に引く。負の値があるとプロット領域の途中に来る。
            var zeroY = axis.ToY(0d, plot);
            svg.Line(plot.Left, zeroY, plot.Right, zeroY, chart.AxisLineColor, 1f);

            // 棒。gapWidth は項目間の間隔、overlap は系列同士の重なりで、いずれも棒の幅に対する割合。
            var categoryCount = Math.Max(chart.Categories.Count, 1);
            var slot = plot.Width / categoryCount;
            var gap = chart.GapWidthPercent / 100f;
            var overlap = chart.OverlapPercent / 100f;
            var barWidth = slot / (chart.Series.Count + gap + (chart.Series.Count - 1) * -overlap);

            for (var category = 0; category < categoryCount; category++)
            {
                var slotLeft = plot.Left + slot * category;
                var firstLeft = slotLeft + gap / 2f * barWidth;

                for (var index = 0; index < chart.Series.Count; index++)
                {
                    var series = chart.Series[index];
                    if (category >= series.Values.Count)
                    {
                        continue;
                    }

                    var left = firstLeft + barWidth * (1f - overlap) * index;
                    var valueY = axis.ToY(series.Values[category], plot);
                    svg.Rect(
                        left,
                        Math.Min(valueY, zeroY),
                        barWidth,
                        Math.Abs(zeroY - valueY),
                        series.Color,
                        null,
                        0f);
                }

                if (category < chart.Categories.Count)
                {
                    var text = chart.Categories[category];
                    var width = labelFont.MeasureText(text, (SKPaint?)null);
                    svg.TextPath(
                        labelFont,
                        text,
                        slotLeft + slot / 2f - width / 2f,
                        zeroY + labelGap - labelMetrics.Ascent,
                        chart.AxisTextStyle.Color);
                }
            }

            if (chart.HasLegend)
            {
                WriteLegend(svg, element.Bounds, plot, chart, labelFont, labelSizePixels);
            }
        }

        private static void WriteLegend(
            SvgBuilder svg,
            SKRect frame,
            SKRect plot,
            BarChart chart,
            SKFont font,
            float fontSizePixels)
        {
            var swatch = fontSizePixels * 0.62f;
            var swatchGap = fontSizePixels * 0.28f;
            var entryGap = fontSizePixels * 1.1f;

            var widths = chart.Series
                .Select(series => swatch + swatchGap + font.MeasureText(series.Name, (SKPaint?)null))
                .ToList();
            var total = widths.Sum() + entryGap * (chart.Series.Count - 1);

            var x = plot.MidX - total / 2f;
            var baseline = frame.Bottom - fontSizePixels * 0.55f;
            var metrics = font.Metrics;

            for (var index = 0; index < chart.Series.Count; index++)
            {
                var series = chart.Series[index];
                svg.Rect(
                    x,
                    baseline - metrics.CapHeight / 2f - swatch / 2f,
                    swatch,
                    swatch,
                    series.Color,
                    null,
                    0f);

                svg.TextPath(font, series.Name, x + swatch + swatchGap, baseline, chart.AxisTextStyle.Color);
                x += widths[index] + entryGap;
            }
        }

        /// <summary>
        /// プロット領域 (目盛り線が引かれる矩形) を決める。
        /// </summary>
        /// <remarks>
        /// pptx が位置を明示 (c:manualLayout) していればそれに従う。
        /// 指定がなければ PowerPoint と同じように自動で配置する。
        /// 目盛りラベル・項目ラベル・凡例が占める分をフォントの実測から差し引いて求めるので、
        /// フォントや文字列が変わっても収まる。
        /// </remarks>
        private static SKRect ResolvePlotArea(
            SKRect frame,
            BarChart chart,
            ValueAxisScale axis,
            SKFont labelFont,
            float labelSizePixels)
        {
            if (chart.PlotArea is { } layout)
            {
                var manualLeft = frame.Left + frame.Width * layout.X;
                var manualTop = frame.Top + frame.Height * layout.Y;
                return new SKRect(
                    manualLeft,
                    manualTop,
                    manualLeft + frame.Width * layout.Width,
                    manualTop + frame.Height * layout.Height);
            }

            var metrics = labelFont.Metrics;
            var lineHeight = -metrics.Ascent + metrics.Descent;

            // 目盛りラベルの最大幅。左端はこれを置ける分だけ内側に入る。
            var tickLabelWidth = 0f;
            for (var index = 0; index <= axis.TickCount; index++)
            {
                var width = labelFont.MeasureText(axis.Format(axis.Min + axis.Unit * index), (SKPaint?)null);
                tickLabelWidth = Math.Max(tickLabelWidth, width);
            }

            var left = frame.Left + tickLabelWidth + labelSizePixels * 0.9f;
            var right = frame.Right - labelSizePixels * 0.5f;

            // 上端はいちばん上の目盛りラベルが切れない位置。
            var top = frame.Top + lineHeight;

            // 下端は項目ラベルと凡例の分を空ける。
            var bottom = frame.Bottom
                - labelSizePixels * 0.55f  // 凡例の下の余白
                - lineHeight               // 凡例
                - labelSizePixels * 1.5f   // 項目ラベルと凡例の間
                - lineHeight               // 項目ラベル
                - labelSizePixels * 0.9f;  // 項目軸と項目ラベルの間

            return new SKRect(left, top, right, bottom);
        }

        /// <summary>
        /// 値軸の目盛り。PowerPoint の自動目盛りと同じ考え方で、
        /// データの最大値に 5% の余裕を足し、5 本以上の目盛りになる最大の刻みを選ぶ。
        /// </summary>
        public readonly record struct ValueAxisScale(double Min, double Max, double Unit)
        {
            public int TickCount => (int)Math.Round((Max - Min) / Unit);

            public static ValueAxisScale For(IEnumerable<BarSeries> series)
            {
                var values = series.SelectMany(s => s.Values).ToList();
                var dataMax = values.Count == 0 ? 0d : values.Max();
                var dataMin = values.Count == 0 ? 0d : values.Min();

                // 棒グラフはゼロを含める
                dataMax = Math.Max(dataMax, 0d);
                dataMin = Math.Min(dataMin, 0d);

                var span = dataMax - dataMin;
                if (span <= 0d)
                {
                    return new ValueAxisScale(0d, 1d, 0.2d);
                }

                // 端に 5% の余裕を足してから、目盛りが 5 本以上になる最大の刻みを選ぶ
                var padding = span * 0.05d;
                var paddedMax = dataMax > 0d ? dataMax + padding : 0d;
                var paddedMin = dataMin < 0d ? dataMin - padding : 0d;
                var paddedSpan = paddedMax - paddedMin;

                var magnitude = Math.Pow(10d, Math.Floor(Math.Log10(paddedSpan)));
                var unit = magnitude;
                foreach (var candidate in new[] { magnitude, magnitude / 2d, magnitude / 5d, magnitude / 10d })
                {
                    unit = candidate;
                    if (paddedSpan / candidate >= 5d)
                    {
                        break;
                    }
                }

                return new ValueAxisScale(
                    Math.Floor(paddedMin / unit) * unit,
                    Math.Ceiling(paddedMax / unit) * unit,
                    unit);
            }

            public float ToY(double value, SKRect plot)
                => plot.Bottom - (float)((value - Min) / (Max - Min)) * plot.Height;

            public string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>SVG を組み立てる。座標と色の書式をここに閉じ込める。</summary>
        private sealed class SvgBuilder
        {
            private readonly StringBuilder _builder = new();

            public void Open(float width, float height)
            {
                _builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ")
                    .Append(CultureInfo.InvariantCulture, $"width=\"{Number(width)}\" height=\"{Number(height)}\" ")
                    .Append(CultureInfo.InvariantCulture, $"viewBox=\"0 0 {Number(width)} {Number(height)}\">")
                    .AppendLine();
            }

            public void Close() => _builder.AppendLine("</svg>");

            public void Rect(float x, float y, float width, float height, SKColor? fill, SKColor? stroke, float strokeWidth)
            {
                _builder.Append(CultureInfo.InvariantCulture,
                    $"<rect x=\"{Number(x)}\" y=\"{Number(y)}\" width=\"{Number(width)}\" height=\"{Number(height)}\"");
                AppendPaint(fill, stroke, strokeWidth);
            }

            public void Line(float x1, float y1, float x2, float y2, SKColor color, float width)
            {
                _builder.Append(CultureInfo.InvariantCulture,
                    $"<line x1=\"{Number(x1)}\" y1=\"{Number(y1)}\" x2=\"{Number(x2)}\" y2=\"{Number(y2)}\"");
                _builder.Append(CultureInfo.InvariantCulture,
                    $" stroke=\"{Hex(color)}\" stroke-width=\"{Number(width)}\"/>");
                _builder.AppendLine();
            }

            public void Polygon(
                IReadOnlyList<SKPoint> points,
                SKColor? fill,
                SKColor? stroke,
                float strokeWidth,
                string? transform = null)
            {
                _builder.Append("<polygon points=\"");
                for (var index = 0; index < points.Count; index++)
                {
                    if (index > 0)
                    {
                        _builder.Append(' ');
                    }

                    _builder.Append(CultureInfo.InvariantCulture, $"{Number(points[index].X)},{Number(points[index].Y)}");
                }

                _builder.Append('"');

                if (transform is not null)
                {
                    _builder.Append(CultureInfo.InvariantCulture, $" transform=\"{transform}\"");
                }

                AppendPaint(fill, stroke, strokeWidth);
            }

            /// <summary>
            /// テキストはグリフのアウトラインにして書き出す。
            /// 閲覧側やラスタライザのフォント解決に依存せず、どこでも同じ絵になる。
            /// </summary>
            public void TextPath(SKFont font, string text, float x, float baseline, SKColor color)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                using var path = font.GetTextPath(text, new SKPoint(x, baseline));
                var data = path.ToSvgPathData();
                if (string.IsNullOrEmpty(data))
                {
                    return;
                }

                _builder.Append(CultureInfo.InvariantCulture, $"<path d=\"{data}\" fill=\"{Hex(color)}\"/>");
                _builder.AppendLine();
            }

            private void AppendPaint(SKColor? fill, SKColor? stroke, float strokeWidth)
            {
                _builder.Append(CultureInfo.InvariantCulture, $" fill=\"{(fill is null ? "none" : Hex(fill.Value))}\"");

                if (stroke is not null && strokeWidth > 0f)
                {
                    _builder.Append(CultureInfo.InvariantCulture,
                        $" stroke=\"{Hex(stroke.Value)}\" stroke-width=\"{Number(strokeWidth)}\"");
                }

                _builder.AppendLine("/>");
            }

            private static string Hex(SKColor color) => $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

            private static string Number(float value) => MathF.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

            public override string ToString() => _builder.ToString();
        }
    }
}
