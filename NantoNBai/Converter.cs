using Spire.Presentation;
using System.IO;

namespace NantoNBai
{
    public class Converter
    {
        public Stream ConvertFromPptx(Stream pptx)
        {
            var presentation = new Presentation();
            presentation.LoadFromStream(pptx, FileFormat.Pptx2013);
            var slide = presentation.Slides[0];
            var svg = slide.SaveToSVG();

            var ms = new MemoryStream();
            ms.Write(svg);
            ms.Position = 0;

            return ms;
        }
    }
}
