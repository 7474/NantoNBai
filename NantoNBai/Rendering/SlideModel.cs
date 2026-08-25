using SkiaSharp;
using System.Collections.Generic;

namespace NantoNBai.Rendering
{
    // pptx から読み取った「描画に必要なだけ」の中間表現。
    // pptx の構造をそのまま持つのではなく、描画器が理解できる形に落とす。
    // 対応していない構造に出会ったら読み取り側が例外を投げる。黙って違う絵を出さないため。

    public sealed record SlideDocument(
        float Width,
        float Height,
        SKColor Background,
        IReadOnlyList<SlideElement> Elements);

    public abstract record SlideElement(SKRect Bounds);

    /// <summary>テキストを持つ図形。テンプレートのタイトルがこれ。</summary>
    public sealed record TextElement(
        SKRect Bounds,
        string Text,
        TextStyle Style,
        TextAlignment Alignment,
        TextAnchor Anchor,
        SKRect Insets,
        float LineSpacing) : SlideElement(Bounds);

    /// <summary>塗りと線を持つ図形。テンプレートの折線矢印がこれ。</summary>
    /// <remarks>
    /// 減少方向のテンプレートは同じ矢印を 180 度回転して左右反転して使っている。
    /// </remarks>
    public sealed record ShapeElement(
        SKRect Bounds,
        ShapeGeometry Geometry,
        SKColor? Fill,
        SKColor? Stroke,
        float StrokeWidth,
        float RotationDegrees,
        bool FlipHorizontal,
        bool FlipVertical) : SlideElement(Bounds);

    /// <summary>グラフを埋め込んだフレーム。</summary>
    public sealed record ChartElement(
        SKRect Bounds,
        BarChart Chart) : SlideElement(Bounds);

    public sealed record TextStyle(float SizePoints, SKColor Color, bool Bold);

    public enum TextAlignment { Left, Center, Right }

    public enum TextAnchor { Top, Center, Bottom }

    public enum ShapeGeometryKind
    {
        /// <summary>prst="rect"</summary>
        Rectangle,

        /// <summary>prst="bentUpArrow"</summary>
        BentUpArrow,
    }

    public sealed record ShapeGeometry(
        ShapeGeometryKind Kind,
        IReadOnlyDictionary<string, int> Adjustments);

    public sealed record BarChart(
        IReadOnlyList<BarSeries> Series,
        IReadOnlyList<string> Categories,
        float GapWidthPercent,
        float OverlapPercent,
        TextStyle AxisTextStyle,
        SKColor GridlineColor,
        SKColor AxisLineColor,
        PlotAreaLayout? PlotArea,
        bool HasLegend);

    public sealed record BarSeries(string Name, SKColor Color, IReadOnlyList<double> Values);

    /// <summary>
    /// グラフのプロット領域。フレームに対する比率で、pptx の c:manualLayout に対応する。
    /// レイアウトをテンプレート側に持たせることで、描画器にマジックナンバーを置かずに済む。
    /// </summary>
    public sealed record PlotAreaLayout(float X, float Y, float Width, float Height);
}
