using Polly;
using Polly.Timeout;
using Microsoft.Extensions.Logging;

namespace BooksService.Services
{
    public interface IResiliencePolicyService
    {
        IAsyncPolicy GetResiliencePolicy();
    }

    public class ResiliencePolicyService : IResiliencePolicyService
    {
        private readonly ILogger<ResiliencePolicyService> _logger;

        public ResiliencePolicyService(ILogger<ResiliencePolicyService> logger)
        {
            _logger = logger;
        }

        public IAsyncPolicy GetResiliencePolicy()
        {
            // Retry policy
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "Retry {RetryCount} after {TimeSpan} seconds",
                            retryCount, timeSpan.TotalSeconds);
                    });

            // Timeout policy
            var timeoutPolicy = Policy
                .TimeoutAsync(TimeSpan.FromSeconds(10),
                    TimeoutStrategy.Pessimistic,
                    onTimeoutAsync: (context, timeSpan, task, exception) =>
                    {
                        _logger.LogWarning("Timeout after {TimeSpan} seconds", timeSpan.TotalSeconds);
                        return Task.CompletedTask;
                    });

            // Circuit breaker policy (voliteľné)
            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    5,
                    TimeSpan.FromSeconds(30),
                    onBreak: (exception, breakDelay) =>
                    {
                        _logger.LogError(exception,
                            "Circuit broken for {BreakDelay} seconds",
                            breakDelay.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit reset");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit half-open");
                    });

            // Zloženie: Timeout -> Circuit Breaker -> Retry
            return Policy.WrapAsync(timeoutPolicy, circuitBreakerPolicy, retryPolicy);
        }
    }
}