using SistemaFacturacionSRI.Application.Interfaces;

namespace SistemaFacturacionSRI.API.Workers
{
    public class SriRetryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SriRetryWorker> _logger;

        public SriRetryWorker(IServiceProvider serviceProvider, ILogger<SriRetryWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Iniciando servicio de reintentos SRI...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Creamos un scope porque FacturaService es "Scoped" y el Worker es "Singleton"
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var facturaService = scope.ServiceProvider.GetRequiredService<IFacturaService>();

                        // Llamamos a la lógica que creamos en el paso 2
                        await facturaService.ProcesarFacturasPendientesSriAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error crítico en el worker de SRI");
                }

                // Esperar 5 minutos antes de la siguiente ejecución
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}