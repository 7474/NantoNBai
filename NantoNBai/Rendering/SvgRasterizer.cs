using SkiaSharp;
using System;
using System.IO;

namespace NantoNBai.Rendering
{
    /// <summary>
    /// SVG を PNG にする。ラスタライズは Svg.Skia (MIT) に任せる。
    /// </summary>
    /// <remarks>
    /// 描画の実装は <see cref="SvgSlideWriter"/> だけが持ち、PNG はその SVG から作る。
    /// 出力形式ごとに描画コードを持たないので、見た目がずれない。
    /// </remarks>
    public sealed class SvgRasterizer
    {
        public Stream ToPng(string svg, int width, int height)
        {
            using var document = new Svg.Skia.SKSvg();
            using var picture = document.FromSvg(svg)
                ?? throw new InvalidOperationException("SVG をラスタライズできない");

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.DrawPicture(picture);
            canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;
            return stream;
        }
    }
}
