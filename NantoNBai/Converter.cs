using Spire.Presentation;
using System.Drawing.Imaging;
using System.IO;

namespace NantoNBai
{
    public class Converter
    {
        public Stream ConvertFromPptx(Stream pptx, ConvertFormat format)
        {
            if (format == ConvertFormat.Pptx) { return pptx; }

            var presentation = new Presentation();
            presentation.LoadFromStream(pptx, FileFormat.Pptx2013);
            var slide = presentation.Slides[0];

            var ms = new MemoryStream();

            switch (format)
            {
                case ConvertFormat.Svg:
                    var bin = slide.SaveToSVG();
                    ms.Write(bin);
                    break;
                case ConvertFormat.Png:
                    {
                        using var image = slide.SaveAsImage();
                        image.Save(ms, ImageFormat.Png);
                    }
                    break;
                default:
                    throw new System.Exception("Invalid format");
            }

            ms.Position = 0;

            return ms;
        }
    }

    public enum ConvertFormat
    {
        Pptx,
        Svg,
        Png
    }
}
