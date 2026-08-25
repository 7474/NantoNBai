using Microsoft.VisualStudio.TestTools.UnitTesting;
using NantoNBai;
using NantoNBai.Rendering;
using SkiaSharp;
using System.Linq;

namespace NantoNBai.Tests
{
    /// <summary>
    /// テンプレートを編集した pptx から、描画に必要な情報を読み取れているかを確認する。
    /// 画像比較より先にこちらが落ちれば、原因が編集側か描画側かの切り分けが要らない。
    /// </summary>
    [TestClass]
    public class SlideReadingTests
    {
        private static SlideDocument Read()
        {
            var service = new NantoNBaiOpenXml();
            using var pptx = service.Generate(
                "./",
                "ポート番号",
                80d,
                443d,
                Nan.Bai,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation");

            return new PptxSlideReader().Read(pptx);
        }

        [TestMethod]
        public void SlideSizeIsFullHd()
        {
            var slide = Read();

            Assert.AreEqual(1280f, slide.Width);
            Assert.AreEqual(720f, slide.Height);
            Assert.AreEqual(SKColors.White, slide.Background);
        }

        /// <summary>
        /// タイトルの書式はテンプレートのマスター (44pt) から継承する。
        /// テキストを差し替えるライブラリが書式を上書きすると、ここが落ちる。
        /// </summary>
        [TestMethod]
        public void TitleInheritsTemplateFormat()
        {
            var title = Read().Elements.OfType<TextElement>().Single();

            Assert.AreEqual("なんとポート番号が5倍に！", title.Text);
            Assert.AreEqual(44f, title.Style.SizePoints, "タイトルの文字サイズがテンプレートと違う");
            Assert.AreEqual(SKColors.Black, title.Style.Color);
            Assert.AreEqual(TextAnchor.Center, title.Anchor);
            Assert.AreEqual(TextAlignment.Left, title.Alignment);
        }

        [TestMethod]
        public void ChartCarriesEditedValues()
        {
            var chart = Read().Elements.OfType<ChartElement>().Single().Chart;

            CollectionAssert.AreEqual(new[] { "ポート番号" }, chart.Categories.ToArray());
            Assert.AreEqual(2, chart.Series.Count);

            Assert.AreEqual("from", chart.Series[0].Name);
            Assert.AreEqual(80d, chart.Series[0].Values.Single());
            Assert.AreEqual(new SKColor(0x44, 0x72, 0xC4), chart.Series[0].Color, "テーマの accent1 で描かれていない");

            Assert.AreEqual("to", chart.Series[1].Name);
            Assert.AreEqual(443d, chart.Series[1].Values.Single());
            Assert.AreEqual(new SKColor(0xED, 0x7D, 0x31), chart.Series[1].Color, "テーマの accent2 で描かれていない");

            Assert.AreEqual(100f, chart.GapWidthPercent);
            Assert.AreEqual(-50f, chart.OverlapPercent);
            Assert.IsTrue(chart.HasLegend);
        }

        [TestMethod]
        public void ArrowUsesTemplateShape()
        {
            var arrow = Read().Elements.OfType<ShapeElement>().Single();

            Assert.AreEqual(ShapeGeometryKind.BentUpArrow, arrow.Geometry.Kind);
            Assert.AreEqual(SKColors.Red, arrow.Fill);
            Assert.AreEqual(18912, arrow.Geometry.Adjustments["adj1"]);
        }

        /// <summary>
        /// 値軸の目盛りは PowerPoint と同じ考え方で決めている。
        /// 443 に対しては 50 刻みで 500 まで。
        /// </summary>
        [TestMethod]
        public void ValueAxisMatchesPowerPointScaling()
        {
            var scale = SvgSlideWriter.ValueAxisScale.For(new[]
            {
                new BarSeries("from", SKColors.Blue, new[] { 80d }),
                new BarSeries("to", SKColors.Orange, new[] { 443d }),
            });

            Assert.AreEqual(500d, scale.Max);
            Assert.AreEqual(50d, scale.Unit);
            Assert.AreEqual(10, scale.TickCount);
        }
    }
}
