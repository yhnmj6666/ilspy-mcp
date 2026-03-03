using ILSpyMcp.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const int stackSize = 16 * 1024 * 1024; // 16 MB

var tcs = new TaskCompletionSource();
var thread = new Thread(() =>
{
    try
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<DecompilerService>();
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        builder.Build().RunAsync().GetAwaiter().GetResult();
        tcs.SetResult();
    }
    catch (Exception ex)
    {
        tcs.SetException(ex);
    }
}, stackSize);
thread.Start();
await tcs.Task;
