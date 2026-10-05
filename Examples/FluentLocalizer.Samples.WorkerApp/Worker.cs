namespace FluentLocalizer.Samples.WorkerApp;

public class Worker(ILogger<Worker> logger, ITranslator translator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Basic file-store lookup: the JSON files are copied beside the application.
        // No WithCulture: requests use the CurrentUICulture set in Program.cs.
        logger.LogInformation("Basic: {Message}", translator.Get("Service:Ready").Resolve());
        logger.LogInformation("Fallback: {Message}", translator.Get("Service:FallbackOnly").Resolve());

        var count = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                // Advanced: namespaced JSON, ICU message arguments, and live file reload.
                var message = await translator.Get("common:Notifications:MessageCount")
                    .WithArg("name", "Anita")
                    .Genderize(Gender.Female)
                    .Pluralize(count++ % 3)
                    .ResolveAsync(stoppingToken);
                logger.LogInformation("Advanced: {Message}", message);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
