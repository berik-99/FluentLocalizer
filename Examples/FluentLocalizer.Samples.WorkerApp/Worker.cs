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
                    .WithArg("name", "Anita")
                    .Genderize(Gender.Male)
                    .Pluralize(0)
                    .WithCulture("it-IT")
                    .ResolveAsync(stoppingToken);
                Console.WriteLine(message);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
