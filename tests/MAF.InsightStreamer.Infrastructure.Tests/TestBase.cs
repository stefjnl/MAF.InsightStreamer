using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.IO;

namespace MAF.InsightStreamer.Infrastructure.Tests;

public abstract class TestBase
{
    protected IServiceProvider ServiceProvider { get; private set; }
    protected IConfiguration Configuration { get; private set; }

    protected TestBase()
    {
        // Set up configuration
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddUserSecrets("MAF.InsightStreamer.Infrastructure.Tests")
            .Build();

        // Set up dependency injection
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole(options => options.FormatterName = "simple");
            builder.AddConfiguration(Configuration.GetSection("Logging"));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add other test services as needed
        services.AddOptions();

        ServiceProvider = services.BuildServiceProvider();
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    protected ILogger<T> GetLogger<T>()
    {
        return ServiceProvider.GetRequiredService<ILogger<T>>();
    }
}