# MarketBot

MarketBot is application designed to monitor and craft items based on buy orders in configured markets.

**Note:** This is the author's first project in C#, and feedback or suggestions for improvements are highly appreciated.

## Table of Contents

- [Features](#features)
- [How It Works](#how-it-works)
- [Configuration](#configuration)
- [Usage](#usage)
- [Limitations](#limitations)
- [Contributing](#contributing)
- [Feedback](#feedback)
- [License](#license)

## Features

- **Buy Order Monitoring:** Continuously scans configured markets for new buy orders.
- **Crafting Queue:** Queues items for crafting based on detected buy orders and available recipes.
- **Dynamic Crafting Times:** Utilizes crafting times from recipes to simulate item creation.
- **Market Operations:** Attempts to sell crafted items in the highest-paying market, not just the one where the buy order was detected.
- **Configurable Markets and Intervals:** Allows customization of market IDs and operation intervals through a configuration file.

## Future
- [ ] Smarter pricing system or ability to configure min max prices. Maybe based on tier or crafting time.
- [ ] Create buy/sell orders for ores and gasses.
- [ ] More complex crafting system. Instead directly crafting item, before that, craft necessary components till ore level. Overflow of components could be sold on market.
- [ ] More complex resource handling. Instead of crafting everything from thin air, maybe it could look into sell orders for components and buy them. This would add movement in smaller servers.
- [ ] Market monitoring and pricing based on real market situation. Monitor own bot sell and buy orders to adjust pricing.

## How It Works

1. **Buy Order Detection:** MarketBot monitors specified markets for new buy orders that meet certain criteria.
2. **Crafting Queue Management:**
   - When a qualifying buy order is detected, the corresponding item is added to the crafting queue.
   - Only one item per `typeId` is crafted at a time. If an item is already queued, additional buy orders for the same item are ignored until the first one is processed.
3. **Item Crafting:**
   - The bot simulates crafting using the crafting time specified in the item's recipe.
   - The crafting process respects the crafting durations and queues items accordingly.
4. **Selling Crafted Items:**
   - After crafting, the bot attempts to sell the item to the highest buy order available in source market.
   - If no buy orders are available in source market, it attempts to sell it in other configured markets.
   - If no buy orders are available, the item is discarded.
5. **Resource Buy Orders:**
   - Currently, MarketBot ignores buy orders for resources and focuses on crafted items only.

## Configuration

MarketBot uses a JSON configuration file to set up its operational parameters. Below is an example configuration:

```json
{
    "Market": {
        "MarketOperationsTickInSeconds": 60,
        "QueueProcessingTickInSeconds": 5,
        "OperationMarkets": [3, 4, 29]
    }
}
```

### Configuration Options

- **MarketOperationsTickInSeconds:** Interval in seconds between each market operation cycle (default is `60` seconds).
- **QueueProcessingTickInSeconds:** Interval in seconds for processing the crafting queue (default is `5` seconds).
- **OperationMarkets:** List of market IDs where the bot will operate. If left empty or omitted, the bot will use all markets specified in `Data/markets.json`.

### Customization

- **Blacklisting/Whitelisting Recipes:**
  - Currently, there is no straightforward way to blacklist or whitelist recipes through the configuration.
  - As a workaround, you can remove unwanted recipes directly from `Data/recipes.json`.

## Usage

1. **Set Up Configuration:**
   - Edit the configuration file with your desired settings.
   - Ensure that the market IDs correspond to valid markets in your environment.
2. **Run the Application:**
   - Building and running now is part of multistep docker operation. See Dockerfile.runtime
3. **Automated run with rest of myDU services:**
   - Add following block at the end of docker-compose.yaml in root myDU server directory:
   ```yaml
   marketbot:
      build:
        context: /path/to/MarketBot
        dockerfile: Dockerfile.runtime
      container_name: mod_MarketBot
      pull_policy: never
      command: /config/dual.yaml
      volumes:
        - ${CONFPATH}:/config
        - ${LOGPATH}:/logs
        - /path/to/MarketBot/config.json:/Mod/config.json
      environment:
        BOT_LOGIN: trader
        BOT_PASSWORD: secret
        QUEUEING: http://queueing:9630
      restart: always
      networks:
        vpcbr:
          ipv4_address: 10.5.0.50```
4. **Monitor Logs:**
   - The application provides logging information to help you monitor its activities and debug if necessary.

## Limitations

- **Single Item Crafting per Type ID:**
  - The bot crafts only one item per `typeId` at a time.
  - If a buy order is detected in one market, and another appears in a different market for the same item, only the first one is processed.
- **No Resource Buy Order Handling:**
  - The current version ignores buy orders for resources and focuses solely on crafted items.
- **Recipe Management:**
  - There is no built-in feature to blacklist or whitelist recipes through the configuration file.
- **Buy Price Handling:**
  - At the moment there is the only check that price needs to be higher than 100. So there is a way to abuse the system.

## Contributing

Contributions are welcome! Whether it's reporting bugs, suggesting new features, or improving the codebase, your input is valuable.

### How to Contribute

1. **Fork the Repository:** Create your own fork of the project.
2. **Create a Branch:** Make a new branch for your feature or bugfix.
3. **Make Changes:** Implement your changes or additions.
4. **Submit a Pull Request:** Provide a clear description of your changes for review.

## Feedback

As this is the author's first project in C#, feedback is highly encouraged. Please feel free to:

- Suggest improvements or optimizations.
- Request new features or functionality.
- Report issues or bugs you encounter.

You can submit feedback by opening an issue or participating in discussions in the project's repository.

## License

This project is licensed under the [MIT License](LICENSE).
