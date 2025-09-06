using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class ItemIdService
{
    private readonly ILogger<ItemIdService> _logger;
    private List<ulong> _allItemIds;
    private readonly string _itemsFilePath;

    public ItemIdService(ILogger<ItemIdService> logger)
    {
        _logger = logger;
        _itemsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "items.yaml");
        _allItemIds = new List<ulong>();
    }

    public async Task<List<ulong>> GetAllItemIdsAsync()
    {
        if (_allItemIds.Any())
        {
            return _allItemIds;
        }

        try
        {
            _logger.LogInformation($"Loading item IDs from {_itemsFilePath}");
            
            if (!File.Exists(_itemsFilePath))
            {
                _logger.LogWarning($"Items file not found at {_itemsFilePath}");
                return new List<ulong>();
            }

            var content = await File.ReadAllTextAsync(_itemsFilePath);
            _allItemIds = ExtractItemIds(content);
            
            _logger.LogInformation($"Loaded {_allItemIds.Count} item IDs from items.yaml");
            return _allItemIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading item IDs from items.yaml");
            return new List<ulong>();
        }
    }

    private List<ulong> ExtractItemIds(string yamlContent)
    {
        var itemIds = new List<ulong>();
        
        // Split content into sections (each item is separated by "---")
        var sections = yamlContent.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var section in sections)
        {
            var lines = section.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Look for uniqueId field in each section
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("uniqueId:"))
                {
                    // Extract the number after "uniqueId: "
                    var match = Regex.Match(trimmedLine, @"uniqueId:\s*(\d+)");
                    if (match.Success && ulong.TryParse(match.Groups[1].Value, out ulong itemId))
                    {
                        itemIds.Add(itemId);
                    }
                }
            }
        }
        
        return itemIds.Distinct().ToList();
    }

    public List<ulong> GetItemIds()
    {
        return _allItemIds.ToList();
    }
}
