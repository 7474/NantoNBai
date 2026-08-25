using DocumentFormat.OpenXml.Packaging;
using ShapeCrawler;
using System.IO;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace NantoNBai
{
    public class NantoNBaiShapeCrawler : INantoNBaiService
    {
        public Stream Generate(string baseDirectoryPath, string target, double from, double to, Nan nan, string contentType)
        {
            // XXX check contentType

            var pres = new Presentation(Path.Combine(baseDirectoryPath, $"nanto-n-bai-template{(from > to ? "(1)" : "")}.pptx"));
            var slide = pres.Slides.First();

            var title = slide.Shapes.First(sp => sp.ContentType != ShapeContentType.Chart);
            var chart = slide.Shapes.First(sp => sp.ContentType == ShapeContentType.Chart).ColumnChart;
            var allow = slide.Shapes.Where(sp => sp.ContentType != ShapeContentType.Chart).Skip(1).First();

            //title.TextFrame.Paragraphs[0].Text = $"なんと{target}が{Math.Floor(to / from)}倍に！";
            title.TextBox.AutofitType = AutofitType.None;
            // TextFrame.Text が内部で段落分割されていると、
            // 代入が段落の先頭への追記になっているのでテンプレートのプレースフォルダを1単語にしている
            // https://github.com/ShapeCrawler/ShapeCrawler/commit/60e0710a65370517227bcd13adb02e930822d3ed#diff-66e3a1ffec6e80c554965ba410f6ab9396eea8a9a7002bb66f1018b33d990ded
            title.SetText($"なんと{target}が{new Formatter().Format(from, to, nan)}に！");

            chart.SeriesCollection[0].Points[0].Value = from;
            chart.SeriesCollection[1].Points[0].Value = to;
            chart.Categories[0].Name = target;

            var ms = new MemoryStream();
            pres.Save(ms);

            ms.Position = 0;
            RestoreInheritedTitleFormat(ms);
            ms.Position = 0;

            return ms;
        }

        // ShapeCrawler 0.80 はテキストを差し替えるときに、解決した書式を run に明示的に書き込む。
        // その値がテンプレートの継承元 (マスターの titleStyle は 44pt、テーマの見出しフォント) と
        // 一致せず、タイトルが 14pt の別フォントで描画されてしまう。
        // ShapeCrawler の保存処理が書き込むため、保存後に明示書式を削って継承に戻す。
        private static void RestoreInheritedTitleFormat(Stream pptx)
        {
            using var document = PresentationDocument.Open(pptx, true);
            var slide = document.PresentationPart?.SlideParts.FirstOrDefault()?.Slide;
            if (slide == null)
            {
                return;
            }

            var title = slide.Descendants<P.Shape>().FirstOrDefault(sp =>
                sp.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value
                    == P.PlaceholderValues.Title);
            if (title == null)
            {
                return;
            }

            foreach (var properties in title.Descendants<A.RunProperties>().ToList())
            {
                properties.FontSize = null;
                foreach (var child in properties.ChildElements
                    .Where(c => c is A.LatinFont or A.EastAsianFont or A.ComplexScriptFont or A.SolidFill)
                    .ToList())
                {
                    child.Remove();
                }
            }
        }
    }
}
