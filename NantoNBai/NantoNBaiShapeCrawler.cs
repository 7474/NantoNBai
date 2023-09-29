using ShapeCrawler;
using System;
using System.IO;
using System.Linq;

namespace NantoNBai
{
    public class NantoNBaiShapeCrawler : INantoNBaiService
    {
        public Stream Generate(string baseDirectoryPath, string target, double from, double to, string contentType)
        {
            // XXX check contentType

            using var pres = SCPresentation.Open(Path.Combine(baseDirectoryPath, "nanto-n-bai-template.pptx"));
            var slide = pres.Slides.First();

            var title = (IAutoShape)slide.Shapes.First(sp => sp is IAutoShape);
            var chart = (IChart)slide.Shapes.First(sp => sp is IChart);

            //title.TextFrame.Paragraphs[0].Text = $"なんと{target}が{Math.Floor(to / from)}倍に！";
            title.TextFrame.AutofitType = SCAutofitType.None;
            title.TextFrame.Text = $"なんと{target}が{Math.Floor(to / from)}倍に！";

            chart.SeriesCollection[0].Points[0].Value = from;
            chart.SeriesCollection[1].Points[0].Value = to;
            chart.Categories[0].Name = target;

            var ms = new MemoryStream();
            pres.SaveAs(ms);
            ms.Position = 0;

            return ms;
        }
    }
}
