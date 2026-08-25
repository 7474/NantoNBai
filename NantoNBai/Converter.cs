using NantoNBai.Rendering;
using Newtonsoft.Json.Converters;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace NantoNBai
{
    /// <summary>
    /// pptx を配信できる形式に変換する。
    /// </summary>
    /// <remarks>
    /// 変換は pptx -> SVG -> PNG の 1 本道で、描画の実装は <see cref="SvgSlideWriter"/> だけが持つ。
    /// PowerPoint の描画エンジンは使わないので、実行環境にオフィスソフトも商用ライブラリも要らない。
    /// </remarks>
    public class Converter
    {
        private readonly PptxSlideReader _reader = new();
        private readonly SvgSlideWriter _writer = new();
        private readonly SvgRasterizer _rasterizer = new();

        public Stream ConvertFromPptx(Stream pptx, ConvertFormat format)
        {
            if (format == ConvertFormat.Pptx) { return pptx; }

            var slide = _reader.Read(pptx);
            var svg = _writer.Write(slide);

            if (format == ConvertFormat.Svg)
            {
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
                stream.Position = 0;
                return stream;
            }

            return _rasterizer.ToPng(svg, (int)slide.Width, (int)slide.Height);
        }
    }

    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public enum ConvertFormat
    {
        [EnumMember(Value = "pptx")]
        Pptx,
        [EnumMember(Value = "svg")]
        Svg,
        [EnumMember(Value = "png")]
        Png
    }
}
