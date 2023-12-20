using ShapeCrawler;
using System.IO;
using System.Linq;

namespace NantoNBai
{
    public class NantoNBaiShapeCrawler : INantoNBaiService
    {
        public Stream Generate(string baseDirectoryPath, string target, double from, double to, Nan nan, string contentType)
        {
            // XXX check contentType

            var pres = new Presentation(Path.Combine(baseDirectoryPath, $"nanto-n-bai-template{(from > to ? "(1)" : "")}.pptx"));
            var slide = pres.Slides.First();

            var title = slide.Shapes.First(sp => !(sp is IChart));
            var chart = (IChart)slide.Shapes.First(sp => sp is IChart);
            var allow = slide.Shapes.Where(sp => !(sp is IChart)).Skip(1).First();

            //title.TextFrame.Paragraphs[0].Text = $"なんと{target}が{Math.Floor(to / from)}倍に！";
            title.TextFrame.AutofitType = AutofitType.None;
            // TextFrame.Text が内部で段落分割されていると、
            // 代入が段落の先頭への追記になっているのでテンプレートのプレースフォルダを1単語にしている
            // https://github.com/ShapeCrawler/ShapeCrawler/commit/60e0710a65370517227bcd13adb02e930822d3ed#diff-66e3a1ffec6e80c554965ba410f6ab9396eea8a9a7002bb66f1018b33d990ded
            title.TextFrame.Text = $"なんと{target}が{new Formatter().Format(from, to, nan)}に！";

            chart.SeriesList[0].Points[0].Value = from;
            chart.SeriesList[1].Points[0].Value = to;
            chart.Categories[0].Name = target;

            var ms = new MemoryStream();
            pres.SaveAs(ms);
            ms.Position = 0;

            return ms;
        }
    }
}
