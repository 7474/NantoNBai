using Codeuctivity.ImageSharpCompare;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;

namespace E2ETest
{
    [TestClass]
    public class TestProduction : PageTest
    {
        private const string BaseUrl = "https://nantonbaifunctionw.azurewebsites.net";

        [TestMethod]
        public async Task GenerateOnFunctionApp()
        {
            // https://n-bai.koudenpa.dev/api/Viewer?name=ポート番号&from=80&to=443
            var res = await Page.GotoAsync($"{BaseUrl}/api/Generate.png?name=ポート番号&from=80&to=443");

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

        /// <summary>
        /// OpenAPI ドキュメントは README と Index から公開リンクとして案内している。
        /// 分離ワーカー移行で最も壊れやすい箇所なので E2E でも監視する。
        /// </summary>
        [TestMethod]
        public async Task SwaggerDocumentOnFunctionApp()
        {
            var res = await Page.GotoAsync($"{BaseUrl}/api/swagger.json");

            Assert.IsNotNull(res);
            Assert.AreEqual(200, res.Status);

            var document = JObject.Parse(await res.TextAsync());
            var paths = document["paths"] as JObject;

            Assert.IsNotNull(paths, "swagger.json に paths がない");
            var pathNames = paths.Properties().Select(x => x.Name).ToList();
            CollectionAssert.Contains(pathNames, "/Generate.{format}");
            CollectionAssert.Contains(pathNames, "/Viewer");
        }

        [TestMethod]
        public async Task InvalidQueryReturnsBadRequest()
        {
            var res = await Page.GotoAsync($"{BaseUrl}/api/Viewer?name=test&from=invalid&to=443");

            Assert.IsNotNull(res);
            Assert.AreEqual(400, res.Status);
        }

        [TestMethod]
        public async Task InvalidEnumReturnsBadRequest()
        {
            var res = await Page.GotoAsync($"{BaseUrl}/api/Generate.invalid?name=test&from=80&to=443");

            Assert.IsNotNull(res);
            Assert.AreEqual(400, res.Status);
        }
    }
}
