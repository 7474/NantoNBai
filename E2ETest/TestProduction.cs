using SixLabors.ImageSharp.Processing;
using Codeuctivity.ImageSharpCompare;

namespace E2ETest
{
    [TestClass]
    public class TestProduction : PageTest
    {
        [TestMethod]
        public async Task HomepageHasPlaywrightInTitleAndGetStartedLinkLinkingtoTheIntroPage()
        {
            // https://n-bai.koudenpa.dev/api/Viewer?name=ポート番号&from=80&to=443
            var res = await Page.GotoAsync("https://n-bai.koudenpa.dev/api/Generate.png?name=ポート番号&from=80&to=443");

            var screenshot = await Page.ScreenshotAsync();
            using var expectedImage = SixLabors.ImageSharp.Image.Load("expect.png");
            using var actualImage = SixLabors.ImageSharp.Image.Load(await res.BodyAsync());

            actualImage.Mutate(x => x.Resize(expectedImage.Width, expectedImage.Height));
            var calcDiff = ImageSharpCompare.CalcDiff(actualImage, expectedImage);

            Assert.IsTrue(calcDiff.PixelErrorCount < 10);
        }
    }
}
