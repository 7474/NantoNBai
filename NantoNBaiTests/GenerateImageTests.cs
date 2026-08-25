using Microsoft.VisualStudio.TestTools.UnitTesting;
using NantoNBai;
using System.IO;
using System.Threading.Tasks;

namespace NantoNBaiTests
{
    /// <summary>
    /// テンプレートを編集して描画するところまでを通しで確認する。
    /// 同梱フォントで描くので、この比較は実行環境に依存しない。
    /// </summary>
    [TestClass()]
    public class GenerateImageTests
    {
        [TestMethod()]
        public async Task GenerateImageTest()
        {
            using var actual = Generate(ConvertFormat.Png);
            using var buffer = new MemoryStream();
            await actual.CopyToAsync(buffer);

            NantoNBai.Tests.PixelComparison.AssertSame("expect.png", buffer.ToArray(), "actual.png");
        }

        /// <summary>
        /// SVG は PNG と同じ描画から出しているので、こちらも通ることを確かめる。
        /// 評価版の透かしのような余計な描画が混ざっていないことも見る。
        /// </summary>
        [TestMethod()]
        public async Task GenerateSvgTest()
        {
            using var stream = Generate(ConvertFormat.Svg);
            using var reader = new StreamReader(stream);
            var svg = await reader.ReadToEndAsync();

            StringAssert.StartsWith(svg, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1280\" height=\"720\"");
            StringAssert.Contains(svg, "#4472C4", "from の棒がない");
            StringAssert.Contains(svg, "#ED7D31", "to の棒がない");
            StringAssert.Contains(svg, "#FF0000", "矢印がない");
            Assert.IsFalse(svg.Contains("Evaluation"), "評価版の透かしのような描画が混ざっている");
        }

        private static Stream Generate(ConvertFormat format)
        {
            var service = new NantoNBaiShapeCrawler();
            var converter = new Converter();

            using var pptx = service.Generate(
                "./",
                "ポート番号",
                80d,
                443d,
                Nan.Bai,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation");

            return converter.ConvertFromPptx(pptx, format);
        }
    }
}
