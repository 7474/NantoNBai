using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

            // 描画は同梱フォントで行うので、本番の出力もローカルの期待画像と一致する
            NantoNBai.Tests.PixelComparison.AssertSame("expect.png", await res.BodyAsync(), "actual.png");
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
    }
}
