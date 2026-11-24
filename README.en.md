# Umbra – Currencies+

[Français](README.md) · **English**

A plugin for [Umbra](https://github.com/una-xiv/umbra) (FINAL FANTASY XIV, via Dalamud) that adds the **Currencies+** widget (« Monnaies+ » in French): an icon in the toolbar that opens your currencies and what to spend them on. It replaces the [CurrencySpender](https://github.com/Blackcatz1911/CurrencySpender) Dalamud plugin.

## Features

- **In the toolbar:** a single icon (customizable). It turns to the alert color when a tracked currency goes above a threshold of its cap (90% by default). The tooltip lists your tracked currencies with their amount and cap.
- **Click the icon:** your tracked currencies at the top of the popup. Click one to see what to spend it on, in two tabs. A tab only shows up when it has something in it.
  - **Profit:** items you can resell on the market board, with their cost, sale price (median of the last 7 days of sales), number of sales over 7 days, gil per point and a score (gil per point × sales over 7 days). Items are sorted by score by default, so an expensive item nobody buys does not come first. Click a column header to sort by that column. The visible columns are set in the widget settings.
  - **Collection:** unlockable items (mounts, minions, orchestrion rolls, emotes, hairstyles, Triple Triad cards, barding…) that you have neither unlocked nor already got in your inventory or chocobo saddlebags, with their type, cost and whether you have enough currency.
- **Click an item:** a flag is placed on the vendor, the item and location links are printed in your chat, and you teleport to the unlocked aetheryte closest to the vendor. When several NPCs sell it, the one in your current zone is chosen, otherwise the one with the cheapest teleport. Teleporting can be turned off in the settings (flag only).
- **“⚙ Manage” button:** every currency from the game's Currency window, with its icon, grouped (tomestones, Grand Company, battle, crafting & gathering, allied societies, other). Click a currency to track or untrack it.
- **Refresh button:** fetches prices again without waiting for the cache to expire.

The widget's interface, name included, follows Umbra's language: French when Umbra is in French, English otherwise. Item, currency and NPC names follow your client's language.

## Prices and network

- Prices come from the [Universalis API](https://docs.universalis.app/), for your home world or, optionally, its whole data center.
- No request is made while the popup is closed. Prices for a currency are cached for 3 hours (configurable).
- Items are requested in batches of 100 (the Universalis limit), with at most 2 requests at a time.
- The Collection tab makes no network call: unlock state is read from the game.

## Installation

1. In game, open **Umbra settings → Plugins**.
2. Add a GitHub repository: owner `Soratix`, repository `UmbraCurrencyPlus`.
3. Switch the toolbar to edit mode and add the **Currencies+** widget (« Monnaies+ » when Umbra is in French). You can also search for `currency`, `tomestone` or `universalis`.
4. Click the icon, then **“⚙ Manage”** to pick your currencies.

## Building it yourself

You need the .NET 10 SDK and XIVLauncher with Umbra installed: the references point to `%AppData%\XIVLauncher`.

```powershell
dotnet build -c Release CurrencyPlus\CurrencyPlus.csproj
```

## Known limitations

- **Currencies not covered:** gil (regular shops), Free Company credits and Orbitingway lottery credits (random items).
- **Shops:** items are read from every shop in the game (SpecialShop, Grand Company shops). Seasonal event shops are excluded, but an item may show up even if its shop is not unlocked for you yet (Grand Company rank, allied society reputation, quest…).
- **Vendor locations:** they come from the game data. They are known for nearly every item of the common currencies, but not for a few old shops: in that case only the item link is printed.
- **Items you already own:** your inventory and chocobo saddlebags are checked, not your retainers.
- **Multi-currency trades:** an item that costs several different currencies at once is skipped.
- **Prices:** these are past sales, not current listings. Universalis data depends on players uploading it, so a rarely traded item may have a rough or missing price.

## Credits

The shop currency ID conversion, the list of event shops and the shop-to-NPC mapping are based on [CurrencySpender](https://github.com/Blackcatz1911/CurrencySpender) (Blackcatz1911, AGPL-3.0).

## License

AGPL-3.0, like Umbra.
