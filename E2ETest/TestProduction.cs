using Codeuctivity.ImageSharpCompare;
using Newtonsoft.Json;
using SixLabors.ImageSharp;

namespace E2ETest
{
    [TestClass]
    public class TestProduction : PageTest
    {
        [TestMethod]
        public async Task GenerateOnFunctionApp()
        {
            // https://n-bai.koudenpa.dev/api/Viewer?name=ポート番号&from=80&to=443
            var res = await Page.GotoAsync("https://nantonbaifunctionw.azurewebsites.net/api/Generate.png?name=ポート番号&from=80&to=443");

            Console.WriteLine(JsonConvert.SerializeObject(res, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented,
            }));

            //var screenshot = await Page.ScreenshotAsync();
            using var expectedImage = SixLabors.ImageSharp.Image.Load("expect.png");
            using var actualImage = SixLabors.ImageSharp.Image.Load(await res.BodyAsync());
            actualImage.SaveAsPng("actual.png");

            //actualImage.Mutate(x => x.Resize(expectedImage.Width, expectedImage.Height));
            var calcDiff = ImageSharpCompare.CalcDiff(actualImage, expectedImage);

            Assert.AreEqual(0, calcDiff.PixelErrorCount);
        }
    }
}
