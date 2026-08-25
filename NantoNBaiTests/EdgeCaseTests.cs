using Microsoft.VisualStudio.TestTools.UnitTesting;
using NantoNBai;
using NantoNBai.Rendering;
using SkiaSharp;
using System.Linq;

namespace NantoNBai.Tests
{
    /// <summary>
    /// 代表値以外の入力。減少方向・負の値・長い name・極端な倍率を確認する。
    /// </summary>
    [TestClass]
    public class EdgeCaseTests
    {
        private static SlideDocument Read(string target, double from, double to)
        {
            var service = new NantoNBaiOpenXml();
            using var pptx = service.Generate(
                "./",
                target,
                from,
                to,
                Nan.Bai,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation");

            return new PptxSlideReader().Read(pptx);
        }

        /// <summary>
        /// 減少方向は別のテンプレートを使い、矢印を 180 度回転して左右反転している。
        /// これを無視すると矢印が逆向きに描かれる。
        /// </summary>
        [TestMethod]
        public void DecreaseFlipsTheArrow()
        {
            var arrow = Read("ポート番号", 443d, 80d).Elements.OfType<ShapeElement>().Single();

            Assert.AreEqual(ShapeGeometryKind.BentUpArrow, arrow.Geometry.Kind);
            Assert.AreEqual(180f, arrow.RotationDegrees);
            Assert.IsTrue(arrow.FlipHorizontal, "矢印が左右反転されていない");
        }

        [TestMethod]
        public void IncreaseDoesNotRotateTheArrow()
        {
            var arrow = Read("ポート番号", 80d, 443d).Elements.OfType<ShapeElement>().Single();

            Assert.AreEqual(0f, arrow.RotationDegrees);
            Assert.IsFalse(arrow.FlipHorizontal);
        }

        [TestMethod]
        public void NegativeValuesExtendTheAxisBelowZero()
        {
            var scale = Scale(-10d, 20d);

            Assert.AreEqual(-15d, scale.Min);
            Assert.AreEqual(25d, scale.Max);
            Assert.AreEqual(5d, scale.Unit);
        }

        [TestMethod]
        public void LargeValuesKeepReadableTicks()
        {
            var scale = Scale(1d, 1000000d);

            Assert.AreEqual(0d, scale.Min);
            Assert.AreEqual(1200000d, scale.Max);
            Assert.AreEqual(200000d, scale.Unit);
        }

        [TestMethod]
        public void SmallValuesGetFractionalTicks()
        {
            var scale = Scale(0.5d, 1.25d);

            Assert.AreEqual(0d, scale.Min);
            Assert.AreEqual(1.4d, scale.Max, 1e-9);
            Assert.AreEqual(0.2d, scale.Unit, 1e-9);
            Assert.AreEqual("1.4", scale.Format(scale.Max));
        }

        [TestMethod]
        public void ZeroValuesStillGetAnAxis()
        {
            var scale = Scale(0d, 0d);

            Assert.IsTrue(scale.Max > scale.Min, "目盛りが潰れている");
            Assert.IsTrue(scale.TickCount > 0);
        }

        /// <summary>長い name はタイトルの箱の幅で折り返す。</summary>
        [TestMethod]
        public void LongNameWrapsTheTitle()
        {
            var title = Read("とても長い名前のポート番号とその周辺", 80d, 443d)
                .Elements.OfType<TextElement>().Single();

            using var font = EmbeddedFont.CreateFont(title.Style.SizePoints * 96f / 72f);
            var lines = SvgSlideWriter.WrapText(font, title.Text, title.Bounds.Width - 20f);

            Assert.IsTrue(lines.Count > 1, "折り返されていない");
            Assert.AreEqual(title.Text, string.Concat(lines));
        }

        [TestMethod]
        public void ShortNameStaysOnOneLine()
        {
            var title = Read("ポート番号", 80d, 443d).Elements.OfType<TextElement>().Single();

            using var font = EmbeddedFont.CreateFont(title.Style.SizePoints * 96f / 72f);
            var lines = SvgSlideWriter.WrapText(font, title.Text, title.Bounds.Width - 20f);

            Assert.AreEqual(1, lines.Count);
        }

        /// <summary>英数字は語の途中で折り返さない。</summary>
        [TestMethod]
        public void WrappingKeepsWordsTogether()
        {
            using var font = EmbeddedFont.CreateFont(40f);
            var lines = SvgSlideWriter.WrapText(font, "なんとHTTPSが5倍に！", 200f);

            Assert.IsTrue(lines.Count > 1, "折り返されていない");
            Assert.IsTrue(lines.All(line => line.Length > 0), "空行ができている");
            Assert.IsTrue(lines.Any(line => line.Contains("HTTPS")), $"語が分割された: {string.Join(" / ", lines)}");
            Assert.AreEqual("なんとHTTPSが5倍に！", string.Concat(lines));
        }

        /// <summary>1 行に収まらない語は、それでも分割して描く (無限ループにしない)。</summary>
        [TestMethod]
        public void WrappingBreaksWordsThatCannotFit()
        {
            using var font = EmbeddedFont.CreateFont(40f);
            var lines = SvgSlideWriter.WrapText(font, "なんとHTTPSが5倍に！", 120f);

            Assert.IsTrue(lines.All(line => line.Length > 0), "空行ができている");
            Assert.AreEqual("なんとHTTPSが5倍に！", string.Concat(lines), "文字が落ちている");
        }

        private static SvgSlideWriter.ValueAxisScale Scale(double from, double to)
            => SvgSlideWriter.ValueAxisScale.For(new[]
            {
                new BarSeries("from", SKColors.Blue, new[] { from }),
                new BarSeries("to", SKColors.Orange, new[] { to }),
            });
    }
}
