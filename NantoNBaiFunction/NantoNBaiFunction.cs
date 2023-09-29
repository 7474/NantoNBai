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

        [FunctionName("Generate")]
        [OpenApiOperation(operationId: "Generate", tags: new[] { "name" })]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiParameter(name: "from", In = ParameterLocation.Query, Required = true, Type = typeof(double), Description = "The **From** parameter")]
        [OpenApiParameter(name: "to", In = ParameterLocation.Query, Required = true, Type = typeof(double), Description = "The **To** parameter")]
        [OpenApiParameter(name: "format", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Format** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/octet-stream", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Generate(
            ExecutionContext executionContext,
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Generate.{format:alpha}")] HttpRequest req,
            string format)
        {
            _logger.LogInformation($"C# HTTP trigger function processed a request. path: {req.Path}, query: {req.QueryString}");

            string name = req.Query["name"];
            var from = double.Parse(req.Query["from"]);
            var to = double.Parse(req.Query["to"]);

            var ms = _nantoNBaiService.Generate(executionContext.FunctionAppDirectory, name, from, to, "application/vnd.openxmlformats-officedocument.presentationml.presentation");

            if (format == "svg")
            {
                var svg = _converter.ConvertFromPptx(ms);
                ms.Dispose();

                return new FileStreamResult(svg, "image/svg+xml")
                {
                    FileDownloadName = $"{name}.svg"
                };
            }

            return new FileStreamResult(ms, "application/octet-stream")
            {
                FileDownloadName = $"{name}.pptx"
            };
        }
    }
}

