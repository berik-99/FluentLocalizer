using FluentLocalizer.Core;

namespace FluentLocalizer.Samples.WorkerApp;

public class Worker(ILogger<Worker> logger, ITranslator translator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var message = await translator.Get("Welcome")
                    .WithArg("name", "Anna")
                    .Genderize(Gender.Female)
                    .Pluralize(5)
                    .ResolveAsync(stoppingToken);
                Console.WriteLine(message);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
