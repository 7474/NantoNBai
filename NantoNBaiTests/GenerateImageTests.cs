using Codeuctivity.ImageSharpCompare;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NantoNBai;
using SixLabors.ImageSharp;

namespace NantoNBaiTests
{
    [TestClass()]
    public class GenerateImageTests
    {
        [TestMethod()]
        public async Task GenerateImageTest()
        {
            var _nantoNBaiService = new NantoNBaiShapeCrawler();
            var _converter = new Converter();

            string name = "ポート番号";
            var from = 80d;
            var to = 443d;
            var convertFormat = ConvertFormat.Png;
            var nan = Nan.Bai;

            using var ms = _nantoNBaiService.Generate(
                "./",
                name,
                from,
                to,
                nan,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation");
            using var imageFileStream = _converter.ConvertFromPptx(ms, convertFormat);
            using var ms2 = new MemoryStream();
            await imageFileStream.CopyToAsync(ms2);
            ms2.Position = 0;

            using var expectedImage = SixLabors.ImageSharp.Image.Load("expect.png");
            using var actualImage = SixLabors.ImageSharp.Image.Load(ms2);
            actualImage.SaveAsPng("actual.png");

            //actualImage.Mutate(x => x.Resize(expectedImage.Width, expectedImage.Height));
            var calcDiff = ImageSharpCompare.CalcDiff(actualImage, expectedImage);

            Assert.AreEqual(0, calcDiff.PixelErrorCount);
        }
    }
}
