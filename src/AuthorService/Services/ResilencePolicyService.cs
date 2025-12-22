using Polly;
using Polly.Timeout;
using Microsoft.Extensions.Logging;

namespace AuthorService.Services
{
    public interface IResiliencePolicyService
    {
        IAsyncPolicy<T> GetPolicy<T>();
    }

    public class ResiliencePolicyService : IResiliencePolicyService
    {
        private readonly ILogger<ResiliencePolicyService> _logger;

        public ResiliencePolicyService(ILogger<ResiliencePolicyService> logger)
        {
            _logger = logger;
        }

        public IAsyncPolicy<T> GetPolicy<T>()
        {
            // Build the non-generic policy
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

            var timeoutPolicy = Policy
                .TimeoutAsync(TimeSpan.FromSeconds(10),
                    TimeoutStrategy.Pessimistic,
                    onTimeoutAsync: (context, timeSpan, task, exception) =>
                    {
                        _logger.LogWarning("Timeout after {TimeSpan} seconds", timeSpan.TotalSeconds);
                        return Task.CompletedTask;
                    });

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

            // Wrap policies and convert to generic
            var wrappedPolicy = Policy.WrapAsync(timeoutPolicy, circuitBreakerPolicy, retryPolicy);
            return wrappedPolicy.AsAsyncPolicy<T>();
        }
    }
}