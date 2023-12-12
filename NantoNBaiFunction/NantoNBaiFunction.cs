using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NantoNBai;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NantoNBaiFunction
{
    public class NantoNBaiFunction
    {
        private readonly ILogger<NantoNBaiFunction> _logger;
        private readonly INantoNBaiService _nantoNBaiService;
        private readonly Converter _converter;

        public NantoNBaiFunction(ILogger<NantoNBaiFunction> log)
        {
            _logger = log;
            _nantoNBaiService = new NantoNBaiShapeCrawler();
            _converter = new Converter();
        }

        [FunctionName(nameof(Generate))]
        [OpenApiOperation("Generate", "Gurafu")]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiParameter(name: "from", In = ParameterLocation.Query, Required = true, Type = typeof(double), Description = "The **From** parameter")]
        [OpenApiParameter(name: "to", In = ParameterLocation.Query, Required = true, Type = typeof(double), Description = "The **To** parameter")]
        [OpenApiParameter(name: "nan", In = ParameterLocation.Query, Required = false, Type = typeof(Nan), Description = "The **Nan** parameter")]
        [OpenApiParameter(name: "format", In = ParameterLocation.Path, Required = true, Type = typeof(ConvertFormat), Description = "The **Format** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/octet-stream", bodyType: typeof(byte[]))]
        public async Task<IActionResult> Generate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Generate.{format}")] HttpRequest req,
            string format,
            ExecutionContext executionContext
        )
        {
            _logger.LogInformation($"C# HTTP trigger function processed a request. path: {req.Path}, query: {req.QueryString}");

            string name = req.Query["name"];
            var from = double.Parse(req.Query["from"]);
            var to = double.Parse(req.Query["to"]);
            var convertFormat = (ConvertFormat)System.Enum.Parse(typeof(ConvertFormat), format, true);
            var nan = (Nan)System.Enum.Parse(typeof(Nan), req.Query["nan"].FirstOrDefault() ?? "bai", true);

            var ms = _nantoNBaiService.Generate(
                executionContext.FunctionAppDirectory,
                name,
                from, 
                to, 
                nan,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation");

            if (convertFormat != ConvertFormat.Pptx)
            {
                var imageFileStream = _converter.ConvertFromPptx(ms, convertFormat);
                ms.Dispose();
                // XXX MemoryStream が返ってくることは知ってるけれどなー。byte[]返却でいいかも。
                using var ms2 = new MemoryStream();
                await imageFileStream.CopyToAsync(ms2);

                return new FileContentResult(ms2.ToArray(), convertFormat == ConvertFormat.Svg ? "image/svg+xml" : "image/png");
            }

            return new FileStreamResult(ms, "application/octet-stream")
            {
                FileDownloadName = $"{name}.pptx"
            };
        }

        [FunctionName(nameof(Viewer))]
        [OpenApiOperation("Viewer", "Gurafu")]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiParameter(name: "from", In = ParameterLocation.Query, Required = true, Type = typeof(double), Description = "The **From** parameter")]
        [OpenApiParameter(name: "to", In = ParameterLocation.Query, Required = true, Type = typeof(double), Description = "The **To** parameter")]
        [OpenApiParameter(name: "nan", In = ParameterLocation.Query, Required = false, Type = typeof(Nan), Description = "The **Nan** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/html", bodyType: typeof(string))]
        public async Task<IActionResult> Viewer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Viewer")] HttpRequest req
        )
        {
            _logger.LogInformation($"C# HTTP trigger function processed a request. path: {req.Path}, query: {req.QueryString}");

            string name = req.Query["name"];
            var from = double.Parse(req.Query["from"]);
            var to = double.Parse(req.Query["to"]);
            var nan = (Nan)System.Enum.Parse(typeof(Nan), req.Query["nan"].FirstOrDefault() ?? "bai", true);
            var bai = new Formatter().Format(from, to, nan);

            return new FileContentResult(Encoding.UTF8.GetBytes($"<html lang=\"ja\"><head>" +
                $"<meta charset=\"UTF-8\">" +
                $"<meta property=\"og:title\" content=\"{name}が{bai}!!!\">" +
                $"<meta property=\"og:description\" content=\"{name}が{bai}!!!\">" +
                $"<meta property=\"og:image\" content=\"https://{req.Host}/api/Generate.png{req.QueryString}\">" +
                $"<meta name=\"twitter:image\" content=\"https://{req.Host}/api/Generate.png{req.QueryString}\">" +
                $"<meta name=\"twitter:card\" content=\"summary_large_image\">" +
                $"</head><body>" +
                $"<div><img src=\"https://{req.Host}/api/Generate.png{req.QueryString}\"></div>" +
                $"<div><a href=\"https://github.com/7474/NantoNBai\">https://github.com/7474/NantoNBai</a></div>" +
                $"</body></html>"), "text/html");
        }

        [FunctionName(nameof(Index))]
        public async Task<IActionResult> Index(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Index")] HttpRequest req
        )
        {
            _logger.LogInformation($"C# HTTP trigger function processed a request. path: {req.Path}, query: {req.QueryString}");

            return new FileContentResult(Encoding.UTF8.GetBytes($"<html lang=\"ja\"><head>" +
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
                $"</body></html>"), "text/html"); 
        }
    }
}

