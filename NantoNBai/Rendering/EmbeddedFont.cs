using SkiaSharp;
using System;
using System.IO;
using System.Reflection;

namespace NantoNBai.Rendering
{
    /// <summary>
    /// 描画に使うフォント。リポジトリに同梱したものだけを使う。
    /// </summary>
    /// <remarks>
    /// テンプレートのテーマフォントは游ゴシックだが、これは再配布できない。
    /// 実行環境のフォントに任せると CI・Azure・手元で描画が変わり、
    /// 画像比較テストが環境依存になってしまう。
    /// そのため OFL の BIZ UDPGothic を同梱し、テーマフォントの指定に関わらずこれを使う。
    /// グリフはアウトライン (SVG の path) に変換して出力するので、
    /// 生成した SVG も閲覧側のフォントに依存しない。
    /// </remarks>
    public static class EmbeddedFont
    {
        private const string ResourceName = "NantoNBai.fonts.BIZUDPGothic-Regular.ttf";

        private static readonly Lazy<SKTypeface> Lazy = new(Load);

        public static SKTypeface Typeface => Lazy.Value;

        public static SKFont CreateFont(float sizePixels) => new(Typeface, sizePixels)
        {
            // ヒンティングは実行環境ごとにグリフを格子に合わせ込むため、
            // 字形と字送りが OS で変わってしまう。切って純粋なアウトラインを使う。
            Hinting = SKFontHinting.None,
            Subpixel = false,
            LinearMetrics = true,
            Edging = SKFontEdging.Antialias,
        };

        private static SKTypeface Load()
        {
            var assembly = typeof(EmbeddedFont).GetTypeInfo().Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"同梱フォントが見つからない: {ResourceName}");

            // SKTypeface.FromStream はストリームを読み切る必要があるので一度メモリに載せる
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;

            return SKTypeface.FromStream(buffer)
                ?? throw new InvalidOperationException($"同梱フォントを読み込めない: {ResourceName}");
        }
    }
}
