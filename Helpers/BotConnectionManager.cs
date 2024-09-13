using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NQutils.Exceptions;

public class BotConnectionManager
{
    private Task _reconnectTask = null;
    private readonly object _reconnectLock = new object();
    private readonly ILogger<BotConnectionManager> _logger;

    public BotConnectionManager(ILogger<BotConnectionManager> logger)
    {
        _logger = logger;
    }

    public async Task ReconnectBotAsync(Exception ex)
    {
        Task reconnectTask;

        lock (_reconnectLock)
        {
            if (_reconnectTask == null || _reconnectTask.IsCompleted)
            {
                _logger.LogInformation("Attempting to reconnect the bot...");
                _reconnectTask = Mod.bot.Reconnect();
            }
            else
            {
                _logger.LogInformation("Reconnection already in progress. Waiting for it to complete.");
            }
            reconnectTask = _reconnectTask;
        }

        try
        {
            await reconnectTask;
        }
        catch (Exception reconnectEx)
        {
            // Clear the task so that future attempts can retry
            lock (_reconnectLock)
            {
                _reconnectTask = null;
            }
            _logger.LogError(reconnectEx, "Failed to reconnect the bot.");
            throw; // Re-throw to propagate the exception
        }
    }

    public bool IsDisconnectedException(Exception ex)
    {
        if (ex is BusinessException businessEx)
        {
            return businessEx.Message.Contains("disconnected") || businessEx.Message.Contains("InvalidSession");
        }
        return false;
    }
}
