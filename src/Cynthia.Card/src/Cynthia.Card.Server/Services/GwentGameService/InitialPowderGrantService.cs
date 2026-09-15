using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cynthia.Card.Server
{
    public sealed class InitialPowderGrantService : BackgroundService
    {
        private readonly GwentDatabaseService _database;
        private readonly ILogger<InitialPowderGrantService> _logger;
        public InitialPowderGrantService(GwentDatabaseService database, ILogger<InitialPowderGrantService> logger)
        { _database = database; _logger = logger; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var count = await _database.BackfillInitialPowder(stoppingToken,
                        (id, error) => _logger.LogError(error, "Initial powder failed for account {PlayerId}", id));
                    _logger.LogInformation("Initial powder backfill completed: {Granted} accounts granted.", count);
                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception e) { _logger.LogError(e, "Initial powder backfill will retry in 30 seconds."); }
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            }
        }
    }
}
