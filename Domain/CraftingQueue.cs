using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public class CraftingQueue
{
    private readonly ConcurrentQueue<CraftingJob> _craftingQueue;
    private readonly ILogger<CraftingQueue> _logger;

    public CraftingQueue(ILogger<CraftingQueue> logger)
    {
        _logger = logger;
        _craftingQueue = new ConcurrentQueue<CraftingJob>();
    }

    /// <summary>
    /// Adds a crafting job to the queue.
    /// </summary>
    public void Add(CraftingJob job)
    {
        _logger.LogInformation($"Job added to queue ID: {job.ItemId}, Quantity: {job.Quantity}, Market: {job.MarketId}, Start: {job.CraftingStartTime}, End: {job.CraftingStartTime.Add(job.CraftingDuration)}");
        _craftingQueue.Enqueue(job);
    }

    /// <summary>
    /// Retrieves the next crafting job
    /// </summary>
    public CraftingJob Peek()
    {
        if (_craftingQueue.TryPeek(out CraftingJob result))
        {
            return result;
        }
        return null;
    }

    /// <summary>
    /// Dequeues the next crafting job from the queue.
    /// </summary>
    public CraftingJob Dequeue()
    {
        if (_craftingQueue.TryDequeue(out CraftingJob result))
        {
            return result;
        }
        return null;
    }

    /// <summary>
    /// Exposes the queue as an enumerable for iteration.
    /// </summary>
    public IEnumerable<CraftingJob> GetAllJobs()
    {
        return _craftingQueue;
    }

    public bool itemQueued(ulong itemId)
    {
        foreach (var job in GetAllJobs()) {
            if (job.ItemId == itemId) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the count of jobs currently in the queue.
    /// </summary>
    public int Count => _craftingQueue.Count;
}