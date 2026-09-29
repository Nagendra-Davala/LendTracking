namespace LendTracking.Services
{
    /// <summary>
    /// Render's free tier spins the instance down after ~15 minutes with no incoming HTTP
    /// request. This periodically makes a real request to our own public health endpoint so
    /// Render always sees recent traffic. Only runs in Production and only if a URL is configured.
    /// </summary>
    public class SelfPingBackgroundService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SelfPingBackgroundService> _logger;
        private readonly IHostEnvironment _env;
        private readonly string? _url;
        private readonly TimeSpan _interval;

        public SelfPingBackgroundService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            IHostEnvironment env,
            ILogger<SelfPingBackgroundService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _env = env;
            _logger = logger;
            _url = config["SelfPing:Url"];
            _interval = TimeSpan.FromMinutes(config.GetValue("SelfPing:IntervalMinutes", 13));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_env.IsProduction())
            {
                _logger.LogInformation("SelfPingBackgroundService disabled (not Production).");
                return;
            }

            if (string.IsNullOrWhiteSpace(_url))
            {
                _logger.LogWarning("SelfPingBackgroundService disabled: SelfPing:Url is not configured.");
                return;
            }

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Ping immediately (including right after a deploy/restart) instead of
                    // waiting a full interval first, so a cold Render instance or suspended
                    // Neon compute gets warmed before the next real user hits it.
                    var response = await client.GetAsync(_url, stoppingToken);
                    _logger.LogInformation("Self-ping {Url} -> {Status}", _url, (int)response.StatusCode);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Self-ping to {Url} failed.", _url);
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
