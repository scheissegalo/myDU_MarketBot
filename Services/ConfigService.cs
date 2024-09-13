using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

public class ConfigService
{
    public MarketBotConfig Config { get; private set; }
    private const string MarketsFilePath = "Data/markets.json";

    public ConfigService(IOptions<ConfigOptions> options)
    {
        var confText = System.IO.File.ReadAllText(
            options.Value.ConfigPath
            );
        Config = JsonConvert.DeserializeObject<MarketBotConfig>(confText);

        // Load markets from the markets.json file
        var marketDataText = File.ReadAllText(MarketsFilePath);
        var availableMarkets = JsonConvert.DeserializeObject<List<Market>>(marketDataText);

        // Validate or populate OperationMarkets based on conditions
        ValidateOrPopulateOperationMarkets(availableMarkets, Config.Market.OperationMarkets, "Market");
    }

    private void ValidateOrPopulateOperationMarkets(List<Market> availableMarkets, List<ulong> operationMarkets, string configSection)
    {
        if (operationMarkets == null || operationMarkets.Count == 0 || operationMarkets.Contains(0))
        {
            // Populate OperationMarkets from markets.json if not set or contains 0
            operationMarkets = availableMarkets.Select(m => m.Id).ToList();
            Console.WriteLine($"Populated {configSection}.OperationMarkets from markets.json.");
        }
        else
        {
            // Verify if the provided markets exist in the markets.json file
            var availableMarketIds = availableMarkets.Select(m => m.Id).ToHashSet();
            var invalidMarkets = operationMarkets.Where(id => !availableMarketIds.Contains(id)).ToList();

            if (invalidMarkets.Any())
            {
                throw new InvalidOperationException($"The following markets are invalid in {configSection}: {string.Join(", ", invalidMarkets)}");
            }
        }
    }
}

public class ConfigOptions
{
    public string ConfigPath { get; set; }
}

public class Market
{
    public ulong Id { get; set; }
    public string Name { get; set; }
}