using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Resolvers;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NantoNBai;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NantoNBaiFunction
{
    public class NantoNBaiFunction
    {
        private const string CacheControl = "public, max-age=31536000";

        // in-process では ExecutionContext.FunctionAppDirectory (host.json のあるディレクトリ) を渡していた。
        // 分離ワーカーではワーカーアセンブリが発行ルートに直接置かれるため、
        // AppContext.BaseDirectory が同じディレクトリを指す。
        // テンプレート pptx は NantoNBai.csproj が CopyToOutputDirectory=Always で
        // このディレクトリに配置している。
        private static readonly string TemplateDirectory = AppContext.BaseDirectory;

        private readonly ILogger<NantoNBaiFunction> _logger;
        private readonly INantoNBaiService _nantoNBaiService;
        private readonly Converter _converter;
        private readonly Formatter _formatter;

        public NantoNBaiFunction(
            ILogger<NantoNBaiFunction> log,
            INantoNBaiService nantoNBaiService,
            Converter converter,
            Formatter formatter)
        {
            _logger = log;
            _nantoNBaiService = nantoNBaiService;
            _converter = converter;
            _formatter = formatter;
        }

        [Function(nameof(Generate))]
        [OpenApiOperation("Generate", "Gurafu")]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Example = typeof(NameExample), Description = "The **Name** parameter")]
        [OpenApiParameter(name: "from", In = ParameterLocation.Query, Required = true, Type = typeof(double), Example = typeof(FromExample), Description = "The **From** parameter")]
        [OpenApiParameter(name: "to", In = ParameterLocation.Query, Required = true, Type = typeof(double), Example = typeof(ToExample), Description = "The **To** parameter")]
        [OpenApiParameter(name: "nan", In = ParameterLocation.Query, Required = false, Type = typeof(Nan), Example = typeof(NanExample), Description = "The **Nan** parameter")]
        [OpenApiParameter(name: "format", In = ParameterLocation.Path, Required = true, Type = typeof(ConvertFormat), Example = typeof(FormatExample), Description = "The **Format** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/octet-stream", bodyType: typeof(byte[]))]
        public async Task<HttpResponseData> Generate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Generate.{format}")] HttpRequestData req,
            string format
        )
        {
            _logger.LogInformation("C# HTTP trigger function processed a request. path: {Path}, query: {Query}", req.Url.AbsolutePath, req.Url.Query);

            var query = HttpUtility.ParseQueryString(req.Url.Query);
            string name = query["name"];
            if (string.IsNullOrWhiteSpace(name) ||
                !double.TryParse(query["from"], out var from) ||
                !double.TryParse(query["to"], out var to) ||
                !Enum.TryParse(format, true, out ConvertFormat convertFormat) ||
                !Enum.IsDefined(convertFormat) ||
                !Enum.TryParse(query["nan"] ?? "bai", true, out Nan nan) ||
                !Enum.IsDefined(nan))
            {
                return await BadRequest(req);
            }

            var ms = _nantoNBaiService.Generate(
                TemplateDirectory,
                name,
                from,
                to,
                nan,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation");

            if (convertFormat != ConvertFormat.Pptx)
            {
                using var imageFileStream = _converter.ConvertFromPptx(ms, convertFormat);
                ms.Dispose();

                var imageResponse = req.CreateResponse(HttpStatusCode.OK);
                imageResponse.Headers.Add("Cache-Control", CacheControl);
                imageResponse.Headers.Add("Content-Type", convertFormat == ConvertFormat.Svg ? "image/svg+xml" : "image/png");
                await imageFileStream.CopyToAsync(imageResponse.Body);
                return imageResponse;
            }

            using (ms)
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Cache-Control", CacheControl);
                response.Headers.Add("Content-Type", "application/octet-stream");
                response.Headers.Add("Content-Disposition", ContentDisposition($"{name}.pptx"));
                await ms.CopyToAsync(response.Body);
                return response;
            }
        }

        [Function(nameof(Viewer))]
        [OpenApiOperation("Viewer", "Gurafu")]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Example = typeof(NameExample), Description = "The **Name** parameter")]
        [OpenApiParameter(name: "from", In = ParameterLocation.Query, Required = true, Type = typeof(double), Example = typeof(FromExample), Description = "The **From** parameter")]
        [OpenApiParameter(name: "to", In = ParameterLocation.Query, Required = true, Type = typeof(double), Example = typeof(ToExample), Description = "The **To** parameter")]
        [OpenApiParameter(name: "nan", In = ParameterLocation.Query, Required = false, Type = typeof(Nan), Example = typeof(NanExample), Description = "The **Nan** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/html", bodyType: typeof(string))]
        public async Task<HttpResponseData> Viewer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Viewer")] HttpRequestData req
        )
        {
            _logger.LogInformation("C# HTTP trigger function processed a request. path: {Path}, query: {Query}", req.Url.AbsolutePath, req.Url.Query);

            var query = HttpUtility.ParseQueryString(req.Url.Query);
            string name = query["name"];
            if (string.IsNullOrWhiteSpace(name) ||
                !double.TryParse(query["from"], out var from) ||
                !double.TryParse(query["to"], out var to) ||
                !Enum.TryParse(query["nan"] ?? "bai", true, out Nan nan) ||
                !Enum.IsDefined(nan))
            {
                return await BadRequest(req);
            }
            var bai = _formatter.Format(from, to, nan);

            // クエリ由来の値は HTML に直接埋め込まない (反射型 XSS 対策)
            var encodedName = HtmlEscape(name);
            var encodedBai = HtmlEscape(bai);
            var encodedQueryString = HtmlEscape(req.Url.Query);

            // {req.Host} Funcion AppsのホストなのでCDNのホストどっかから取りたい
            return await Html(req, $"<html lang=\"ja\"><head>" +
                $"<meta charset=\"UTF-8\">" +
                $"<meta property=\"og:title\" content=\"{encodedName}が{encodedBai}!!!\">" +
                $"<meta property=\"og:description\" content=\"{encodedName}が{encodedBai}!!!\">" +
                $"<meta property=\"og:image\" content=\"https://n-bai.koudenpa.dev/api/Generate.png{encodedQueryString}\">" +
                $"<meta name=\"twitter:image\" content=\"https://n-bai.koudenpa.dev/api/Generate.png{encodedQueryString}\">" +
                $"<meta name=\"twitter:card\" content=\"summary_large_image\">" +
                $"</head><body>" +
                $"<div><img src=\"https://n-bai.koudenpa.dev/api/Generate.png{encodedQueryString}\"></div>" +
                $"<div><a href=\"https://github.com/7474/NantoNBai\">https://github.com/7474/NantoNBai</a></div>" +
                $"</body></html>");
        }

        [Function(nameof(Index))]
        public async Task<HttpResponseData> Index(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Index")] HttpRequestData req
        )
        {
            _logger.LogInformation("C# HTTP trigger function processed a request. path: {Path}, query: {Query}", req.Url.AbsolutePath, req.Url.Query);

            return await Html(req, $"<html lang=\"ja\"><head>" +
                $"<meta charset=\"UTF-8\">" +
                $"<meta property=\"og:title\" content=\"NantoNBai\">" +
                $"<meta property=\"og:description\" content=\"なんと凄いグラフを作れます\">" +
                $"<meta property=\"og:image\" content=\"https://n-bai.koudenpa.dev/api/Generate.png?name=%E3%83%9D%E3%83%BC%E3%83%88%E7%95%AA%E5%8F%B7&from=80&to=443\">" +
                $"<meta name=\"twitter:image\" content=\"https://n-bai.koudenpa.dev/api/Generate.png?name=%E3%83%9D%E3%83%BC%E3%83%88%E7%95%AA%E5%8F%B7&from=80&to=443\">" +
                $"<meta name=\"twitter:card\" content=\"summary_large_image\">" +
                $"</head><body>" +
                $"<h1>NantoNBai</h1>" +
                $"<p>なんと凄いグラフを作れます</p>" +
                $"<div><img src=\"https://n-bai.koudenpa.dev/api/Generate.png?name=%E3%83%9D%E3%83%BC%E3%83%88%E7%95%AA%E5%8F%B7&from=80&to=443\"></div>" +
                $"<ul>" +
                $"<li><a href=\"https://n-bai.koudenpa.dev/api/swagger/ui\">https://n-bai.koudenpa.dev/api/swagger/ui</a></li>" +
                $"<li><a href=\"https://github.com/7474/NantoNBai\">https://github.com/7474/NantoNBai</a></li>" +
                $"</ul>" +
                $"</body></html>");
        }

        private static async Task<HttpResponseData> Html(HttpRequestData req, string html)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Cache-Control", CacheControl);
            response.Headers.Add("Content-Type", "text/html");
            await response.WriteBytesAsync(Encoding.UTF8.GetBytes(html));
            return response;
        }

        private static async Task<HttpResponseData> BadRequest(HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("Invalid query parameters.");
            return response;
        }

        // HTML の特殊文字だけをエスケープする。
        // WebUtility.HtmlEncode は非 ASCII も数値文字参照にしてしまい、
        // 日本語を含む既存の出力バイト列が変わってしまうため使わない。
        private static string HtmlEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        // ASP.NET Core の FileStreamResult 相当のヘッダを組み立てる。
        // ファイル名は日本語になりうるので RFC 6266 の filename* を併記する。
        private static string ContentDisposition(string fileName)
        {
            var ascii = new string(fileName.Select(c => c < 0x20 || c > 0x7e || c == '"' || c == '\\' ? '_' : c).ToArray());
            return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
        }

        public sealed class NameExample : OpenApiExample<string>
        {
            public override IOpenApiExample<string> Build(NamingStrategy namingStrategy = null)
            {
                Examples.Add(OpenApiExampleResolver.Resolve("default", "ポート番号", namingStrategy));
                return this;
            }
        }

        public sealed class FromExample : OpenApiExample<double>
        {
            public override IOpenApiExample<double> Build(NamingStrategy namingStrategy = null)
            {
                Examples.Add(OpenApiExampleResolver.Resolve("default", 80d, namingStrategy));
                return this;
            }
        }

        public sealed class ToExample : OpenApiExample<double>
        {
            public override IOpenApiExample<double> Build(NamingStrategy namingStrategy = null)
            {
                Examples.Add(OpenApiExampleResolver.Resolve("default", 443d, namingStrategy));
                return this;
            }
        }

        public sealed class NanExample : OpenApiExample<Nan>
        {
            public override IOpenApiExample<Nan> Build(NamingStrategy namingStrategy = null)
            {
                Examples.Add(OpenApiExampleResolver.Resolve("default", Nan.Bai, namingStrategy));
                return this;
            }
        }

        public sealed class FormatExample : OpenApiExample<ConvertFormat>
        {
            public override IOpenApiExample<ConvertFormat> Build(NamingStrategy namingStrategy = null)
            {
                Examples.Add(OpenApiExampleResolver.Resolve("default", ConvertFormat.Png, namingStrategy));
                return this;
            }
        }
    }
}
