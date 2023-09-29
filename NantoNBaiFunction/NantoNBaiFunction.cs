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

            var ms = _nantoNBaiService.Generate(executionContext.FunctionAppDirectory, name, from, to, "application/vnd.openxmlformats-officedocument.presentationml.presentation");

            if (convertFormat != ConvertFormat.Pptx)
            {
                var imageFileStream = _converter.ConvertFromPptx(ms, convertFormat);
                ms.Dispose();
                // XXX MemoryStream ���Ԃ��Ă��邱�Ƃ͒m���Ă邯��ǂȁ[�Bbyte[]�ԋp�ł��������B
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
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/html", bodyType: typeof(string))]
        public async Task<IActionResult> Viewer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Viewer")] HttpRequest req
        )
        {
            _logger.LogInformation($"C# HTTP trigger function processed a request. path: {req.Path}, query: {req.QueryString}");

            string name = req.Query["name"];
            var from = double.Parse(req.Query["from"]);
            var to = double.Parse(req.Query["to"]);

            return new FileContentResult(Encoding.UTF8.GetBytes($"<html lang=\"ja\"><head>" +
                $"<meta charset=\"UTF-8\">" +
                $"<meta property=\"og:title\" content=\"{name}��{Math.Floor(to / from)}�{!!!\">" +
                $"<meta property=\"og:image\" content=\"https://{req.Host}/api/Generate.png{req.QueryString}\">" +
                $"</head><body>" +
                $"<img src=\"https://{req.Host}/api/Generate.png{req.QueryString}\">" +
                $"</body></html>"), "text/html");
        }
    }
}

