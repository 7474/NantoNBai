using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NantoNBai;
using System.IO;
using System.Net;
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
        [OpenApiOperation("Generate")]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiParameter(name: "from", In = ParameterLocation.Query, Required = true, Type = typeof(double), Description = "The **From** parameter")]
        [OpenApiParameter(name: "to", In = ParameterLocation.Query, Required = true, Type = typeof(double), Description = "The **To** parameter")]
        [OpenApiParameter(name: "format", In = ParameterLocation.Path, Required = true, Type = typeof(ConvertFormat), Description = "The **Format** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/octet-stream", bodyType: typeof(byte[]))]
        public async Task<IActionResult> Generate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Generate.{format:alpha}")] HttpRequest req,
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
    }
}

