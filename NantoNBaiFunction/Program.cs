using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NantoNBai;

// 分離ワーカーのエントリポイント。
//
// ASP.NET Core 統合 (ConfigureFunctionsWebApplication / HttpRequest / IActionResult) は
// 意図的に採用していない。OpenAPI 拡張が提供する swagger エンドポイントは
// 1.6.0 でも 2.0.0-preview2 でも HttpRequestData ベースのままで、
// ASP.NET Core 統合と併用すると HttpRequestData の URL が上書きされ
// swagger UI / swagger.json が壊れることが報告されているため。
// https://github.com/Azure/azure-functions-dotnet-worker/issues/2071
// https://github.com/Azure/azure-functions-openapi-extension/issues/617
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    .ConfigureOpenApi()
    .ConfigureServices(services =>
    {
        services.AddSingleton<INantoNBaiService, NantoNBaiOpenXml>();
        services.AddSingleton<Converter>();
        services.AddSingleton<Formatter>();
    })
    .Build();

await host.RunAsync();
