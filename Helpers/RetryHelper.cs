using System.Threading.Tasks;
using System;

public static class RetryHelper
{
   public static async Task<T> RetryOnExceptionAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, bool> shouldRetryOnException,
        Func<Task> onRetryAsync,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        int retryCount = 0;
        delay ??= TimeSpan.FromSeconds(1);

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (shouldRetryOnException(ex))
            {
                retryCount++;

                if (retryCount > maxRetries)
                {
                    throw; // Exceeded max retries
                }

                if (onRetryAsync != null)
                {
                    await onRetryAsync();
                }

                await Task.Delay(delay.Value);
            }
        }
    }

    public static async Task RetryOnExceptionAsync(
        Func<Task> operation,
        Func<Exception, bool> shouldRetryOnException,
        Func<Task> onRetryAsync,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        int retryCount = 0;
        delay ??= TimeSpan.FromSeconds(1);

        while (true)
        {
            try
            {
                await operation();
                return; // Operation succeeded, exit the method
            }
            catch (Exception ex) when (shouldRetryOnException(ex))
            {
                retryCount++;

                if (retryCount > maxRetries)
                {
                    throw; // Exceeded max retries
                }

                if (onRetryAsync != null)
                {
                    await onRetryAsync();
                }

                await Task.Delay(delay.Value);
            }
        }
    }
}
