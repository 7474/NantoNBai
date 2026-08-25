using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace NantoNBai.Rendering
{
    /// <summary>
    /// pptx の 1 枚目のスライドを描画用の中間表現に読み替える。
    /// </summary>
    /// <remarks>
    /// PowerPoint の仕様を網羅する読み取りではなく、テンプレートが使っている構造だけを扱う。
    /// 想定外の構造に出会ったら例外を投げる。黙って違う絵を描くと画像比較テストでしか気付けないため。
    /// </remarks>
    public sealed class PptxSlideReader
    {
        // 96 DPI では 1 px = 9525 EMU
        private const float EmuPerPixel = 9525f;

        public SlideDocument Read(Stream pptx)
        {
            using var document = PresentationDocument.Open(pptx, false);
            var presentationPart = document.PresentationPart
                ?? throw new InvalidOperationException("pptx にプレゼンテーション本体がない");
            var slidePart = presentationPart.SlideParts.FirstOrDefault()
                ?? throw new InvalidOperationException("pptx にスライドがない");

            var size = presentationPart.Presentation.SlideSize
                ?? throw new InvalidOperationException("スライドサイズがない");
            var width = ToPixels(size.Cx?.Value ?? 0);
            var height = ToPixels(size.Cy?.Value ?? 0);

            var layoutPart = slidePart.SlideLayoutPart;
            var masterPart = layoutPart?.SlideMasterPart;
            var theme = new SlideTheme(masterPart);

            var elements = new List<SlideElement>();
            var tree = slidePart.Slide.CommonSlideData?.ShapeTree
                ?? throw new InvalidOperationException("スライドに図形ツリーがない");

            foreach (var child in tree.ChildElements)
            {
                switch (child)
                {
                    case P.Shape shape:
                        elements.Add(ReadShape(shape, layoutPart, masterPart, theme));
                        break;
                    case P.GraphicFrame frame:
                        elements.Add(ReadChartFrame(frame, slidePart, theme));
                        break;
                    case P.NonVisualGroupShapeProperties:
                    case P.GroupShapeProperties:
                        break;
                    case OpenXmlUnknownElement:
                    case A.ExtensionList:
                        break;
                    default:
                        // p:pic (画像) や p:grpSp (グループ) はテンプレートが使っていないので未対応
                        if (child is P.Picture or P.GroupShape)
                        {
                            throw new NotSupportedException($"未対応の図形: {child.LocalName}");
                        }
                        break;
                }
            }

            return new SlideDocument(width, height, theme.Background, elements);
        }

        private SlideElement ReadShape(P.Shape shape, SlideLayoutPart? layoutPart, SlideMasterPart? masterPart, SlideTheme theme)
        {
            var placeholder = shape.NonVisualShapeProperties?
                .ApplicationNonVisualDrawingProperties?.PlaceholderShape;
            var bounds = ResolveBounds(shape, placeholder, layoutPart, masterPart);

            var body = shape.TextBody;
            var runs = body?.Descendants<A.Run>().ToList() ?? new List<A.Run>();
            var text = string.Concat(runs.Select(r => r.Text?.Text ?? string.Empty));

            if (!string.IsNullOrEmpty(text))
            {
                var defaults = ResolveTextDefaults(placeholder, masterPart);
                var runProperties = runs.First().RunProperties;

                var sizePoints = (runProperties?.FontSize?.Value ?? defaults.FontSize) / 100f;
                var color = ResolveColor(runProperties?.GetFirstChild<A.SolidFill>(), theme)
                    ?? ResolveColor(defaults.Fill, theme)
                    ?? SKColors.Black;

                var bodyProperties = body?.BodyProperties;
                var anchorValue = bodyProperties?.Anchor?.Value ?? defaults.Anchor;
                var anchor = anchorValue == A.TextAnchoringTypeValues.Top ? TextAnchor.Top
                    : anchorValue == A.TextAnchoringTypeValues.Bottom ? TextAnchor.Bottom
                    : TextAnchor.Center;
                var insets = new SKRect(
                    ToPixels(bodyProperties?.LeftInset?.Value ?? defaults.LeftInset),
                    ToPixels(bodyProperties?.TopInset?.Value ?? defaults.TopInset),
                    ToPixels(bodyProperties?.RightInset?.Value ?? defaults.RightInset),
                    ToPixels(bodyProperties?.BottomInset?.Value ?? defaults.BottomInset));

                var paragraph = body?.GetFirstChild<A.Paragraph>();
                var alignmentValue = paragraph?.ParagraphProperties?.Alignment?.Value ?? defaults.Alignment;
                var alignment = alignmentValue == A.TextAlignmentTypeValues.Center ? TextAlignment.Center
                    : alignmentValue == A.TextAlignmentTypeValues.Right ? TextAlignment.Right
                    : TextAlignment.Left;

                return new TextElement(
                    bounds,
                    text,
                    new TextStyle(sizePoints, color, runProperties?.Bold?.Value ?? false),
                    alignment,
                    anchor,
                    insets,
                    defaults.LineSpacing);
            }

            var geometry = ReadGeometry(shape.ShapeProperties);
            var fill = ResolveColor(shape.ShapeProperties?.GetFirstChild<A.SolidFill>(), theme);
            var (stroke, strokeWidth) = ResolveOutline(shape, theme);

            // 回転は 1/60000 度単位
            var transform = shape.ShapeProperties?.Transform2D;
            var rotation = (transform?.Rotation?.Value ?? 0) / 60000f;

            return new ShapeElement(
                bounds,
                geometry,
                fill,
                stroke,
                strokeWidth,
                rotation,
                transform?.HorizontalFlip?.Value ?? false,
                transform?.VerticalFlip?.Value ?? false);
        }

        private static ShapeGeometry ReadGeometry(P.ShapeProperties? properties)
        {
            var preset = properties?.GetFirstChild<A.PresetGeometry>();
            var presetValue = preset?.Preset?.Value;
            ShapeGeometryKind kind;
            if (presetValue is null || presetValue == A.ShapeTypeValues.Rectangle)
            {
                kind = ShapeGeometryKind.Rectangle;
            }
            else if (presetValue == A.ShapeTypeValues.BentUpArrow)
            {
                kind = ShapeGeometryKind.BentUpArrow;
            }
            else
            {
                throw new NotSupportedException($"未対応の図形ジオメトリ: {presetValue}");
            }

            var adjustments = new Dictionary<string, int>();
            foreach (var guide in preset?.AdjustValueList?.Elements<A.ShapeGuide>() ?? Enumerable.Empty<A.ShapeGuide>())
            {
                var name = guide.Name?.Value;
                var formula = guide.Formula?.Value;
                if (name is null || formula is null || !formula.StartsWith("val ", StringComparison.Ordinal))
                {
                    continue;
                }

                if (int.TryParse(formula.AsSpan(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    adjustments[name] = value;
                }
            }

            return new ShapeGeometry(kind, adjustments);
        }

        private (SKColor? Color, float Width) ResolveOutline(P.Shape shape, SlideTheme theme)
        {
            var outline = shape.ShapeProperties?.GetFirstChild<A.Outline>();
            if (outline is not null)
            {
                if (outline.GetFirstChild<A.NoFill>() is not null)
                {
                    return (null, 0f);
                }

                var explicitColor = ResolveColor(outline.GetFirstChild<A.SolidFill>(), theme);
                if (explicitColor is not null)
                {
                    return (explicitColor, ToPixels(outline.Width?.Value ?? 9525));
                }
            }

            // 図形の線がテーマのスタイル参照 (a:lnRef) だけで決まる場合。
            // テンプレートの折線矢印がこれで、テーマの線スタイルと accent1 の陰影で描かれる。
            var lineReference = shape.ShapeStyle?.LineReference;
            if (lineReference is null)
            {
                return (null, 0f);
            }

            var color = theme.ResolveStyleColor(lineReference);
            var width = theme.ResolveLineWidth(lineReference.Index?.Value ?? 0);
            return (color, ToPixels(width));
        }

        private SlideElement ReadChartFrame(P.GraphicFrame frame, SlidePart slidePart, SlideTheme theme)
        {
            var transform = frame.Transform
                ?? throw new InvalidOperationException("グラフフレームに位置がない");
            var bounds = ToRect(transform.Offset, transform.Extents);

            var reference = frame.Graphic?.GraphicData?.GetFirstChild<C.ChartReference>()
                ?? throw new NotSupportedException("グラフ以外の graphicFrame は未対応");
            var chartPart = (ChartPart)slidePart.GetPartById(reference.Id!);

            return new ChartElement(bounds, new ChartReader(theme).Read(chartPart));
        }

        private SKRect ResolveBounds(
            P.Shape shape,
            P.PlaceholderShape? placeholder,
            SlideLayoutPart? layoutPart,
            SlideMasterPart? masterPart)
        {
            var own = shape.ShapeProperties?.Transform2D;
            if (own is not null)
            {
                return ToRect(own.Offset, own.Extents);
            }

            // プレースホルダーは位置をレイアウト → マスターから継承する
            foreach (var candidate in EnumeratePlaceholderSources(placeholder, layoutPart, masterPart))
            {
                var transform = candidate.ShapeProperties?.Transform2D;
                if (transform is not null)
                {
                    return ToRect(transform.Offset, transform.Extents);
                }
            }

            throw new InvalidOperationException("図形の位置を解決できない");
        }

        private static IEnumerable<P.Shape> EnumeratePlaceholderSources(
            P.PlaceholderShape? placeholder,
            SlideLayoutPart? layoutPart,
            SlideMasterPart? masterPart)
        {
            if (placeholder is null)
            {
                yield break;
            }

            var layoutShapes = layoutPart?.SlideLayout.CommonSlideData?.ShapeTree.Elements<P.Shape>()
                ?? Enumerable.Empty<P.Shape>();
            var masterShapes = masterPart?.SlideMaster.CommonSlideData?.ShapeTree.Elements<P.Shape>()
                ?? Enumerable.Empty<P.Shape>();

            foreach (var candidate in layoutShapes.Concat(masterShapes))
            {
                var other = candidate.NonVisualShapeProperties?
                    .ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                if (other is null)
                {
                    continue;
                }

                if (SamePlaceholder(placeholder, other))
                {
                    yield return candidate;
                }
            }
        }

        private static bool SamePlaceholder(P.PlaceholderShape one, P.PlaceholderShape other)
        {
            var oneType = one.Type?.Value ?? P.PlaceholderValues.Body;
            var otherType = other.Type?.Value ?? P.PlaceholderValues.Body;
            if (oneType != otherType)
            {
                return false;
            }

            return (one.Index?.Value ?? 0) == (other.Index?.Value ?? 0);
        }

        private TextDefaults ResolveTextDefaults(P.PlaceholderShape? placeholder, SlideMasterPart? masterPart)
        {
            var styles = masterPart?.SlideMaster.TextStyles;
            var type = placeholder?.Type?.Value;
            var isTitle = type is not null
                && (type == P.PlaceholderValues.Title || type == P.PlaceholderValues.CenteredTitle);
            OpenXmlCompositeElement? listStyle = isTitle
                ? styles?.TitleStyle
                : styles?.BodyStyle;

            var level = listStyle?.GetFirstChild<A.Level1ParagraphProperties>();
            var defaultRun = level?.GetFirstChild<A.DefaultRunProperties>();

            // マスターの同種プレースホルダーの bodyPr から縦位置と余白を引き継ぐ
            var masterShape = masterPart?.SlideMaster.CommonSlideData?.ShapeTree.Elements<P.Shape>()
                .FirstOrDefault(s => placeholder is not null
                    && s.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape is { } other
                    && SamePlaceholder(placeholder, other));
            var bodyProperties = masterShape?.TextBody?.BodyProperties;

            var lineSpacing = level?.GetFirstChild<A.LineSpacing>()?
                .GetFirstChild<A.SpacingPercent>()?.Val?.Value ?? 100000;

            return new TextDefaults(
                FontSize: defaultRun?.FontSize?.Value ?? 1800,
                Fill: defaultRun?.GetFirstChild<A.SolidFill>(),
                Alignment: level?.Alignment?.Value ?? A.TextAlignmentTypeValues.Left,
                Anchor: bodyProperties?.Anchor?.Value ?? A.TextAnchoringTypeValues.Top,
                LeftInset: bodyProperties?.LeftInset?.Value ?? 91440,
                TopInset: bodyProperties?.TopInset?.Value ?? 45720,
                RightInset: bodyProperties?.RightInset?.Value ?? 91440,
                BottomInset: bodyProperties?.BottomInset?.Value ?? 45720,
                LineSpacing: lineSpacing / 100000f);
        }

        internal static SKColor? ResolveColor(A.SolidFill? fill, SlideTheme theme) => theme.ResolveFill(fill);

        private static SKRect ToRect(A.Offset? offset, A.Extents? extents)
        {
            var x = ToPixels(offset?.X?.Value ?? 0);
            var y = ToPixels(offset?.Y?.Value ?? 0);
            var width = ToPixels(extents?.Cx?.Value ?? 0);
            var height = ToPixels(extents?.Cy?.Value ?? 0);
            return new SKRect(x, y, x + width, y + height);
        }

        private static float ToPixels(long emu) => emu / EmuPerPixel;

        private static float ToPixels(int emu) => emu / EmuPerPixel;

        private sealed record TextDefaults(
            int FontSize,
            A.SolidFill? Fill,
            A.TextAlignmentTypeValues Alignment,
            A.TextAnchoringTypeValues Anchor,
            int LeftInset,
            int TopInset,
            int RightInset,
            int BottomInset,
            float LineSpacing);
    }
}
