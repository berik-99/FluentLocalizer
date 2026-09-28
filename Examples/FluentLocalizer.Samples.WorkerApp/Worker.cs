namespace FluentLocalizer.Samples.WorkerApp;

public class Worker(ILogger<Worker> logger, ITranslator translator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Basic file-store lookup: the JSON files are copied beside the application.
        logger.LogInformation("Basic: {Message}", translator.Get("Service:Ready").WithCulture("it-IT").Resolve());
        logger.LogInformation("Fallback: {Message}", translator.Get("Service:FallbackOnly").WithCulture("it-IT").Resolve());

        var count = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                // Advanced: namespaced JSON, MessageFormat arguments, and live file reload.
                var message = await translator.Get("common:Notifications:MessageCount")
                    .WithArg("name", "Anita")
                    .Genderize(Gender.Female)
                    .Pluralize(count++ % 3)
                    .WithCulture("it-IT")
                    .ResolveAsync(stoppingToken);
                logger.LogInformation("Advanced: {Message}", message);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
