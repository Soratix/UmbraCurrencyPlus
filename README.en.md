# Umbra – Currencies+

[Français](README.md) · **English**

A plugin for [Umbra](https://github.com/una-xiv/umbra) (FINAL FANTASY XIV, via Dalamud) that adds the **Currencies+** widget (« Monnaies+ » in French): your currencies in the toolbar and, one click away, what to spend them on. It replaces the [CurrencySpender](https://github.com/Blackcatz1911/CurrencySpender) Dalamud plugin.

## Features

- **In the toolbar:** one or more currencies with their icon and amount, like Umbra's own Currencies widget. The amount changes color above a threshold of its cap (90% by default).
- **Tooltip:** every tracked currency with its amount and cap.
- **Click a currency:** a popup with two tabs. The currency you clicked is preselected, and a row at the top lets you switch to another one.
  - **Profit:** items you can resell on the market board, with their cost, sale price (median of the last 7 days of sales), number of sales over 7 days, gil per point and a score (gil per point × sales over 7 days). Items are sorted by score by default, so an expensive item nobody buys does not come first. Click a column header to sort by that column.
  - **Collection:** unlockable items your character does not have yet (mounts, minions, orchestrion rolls, emotes, hairstyles, Triple Triad cards, barding…), with their type, cost and whether you have enough currency.
- **Click an item:** its link is printed in your chat (visible only to you, clickable) and its name is copied to the clipboard, ready to paste into the market board search.
- **Refresh button:** fetches prices again without waiting for the cache to expire.

Available currencies: tomestones (poetics, plus the current uncapped and capped ones), your Grand Company seals, Allied, Centurio and Nuts hunt currencies, Wolf Marks, Trophy Crystals, purple and orange scrips (crafter and gatherer), Skybuilders' scrips, bicolor gemstones and MGP. You can add any other currency by its item ID (for example an allied society currency).

The widget's interface, name included, follows Umbra's language: French when Umbra is in French, English otherwise. Item and currency names follow your client's language.

## Prices and network

- Prices come from the [Universalis API](https://docs.universalis.app/), for your home world or, optionally, its whole data center.
- No request is made while the popup is closed. Prices for a currency are cached for 3 hours (configurable).
- Items are requested in batches of 100 (the Universalis limit), with at most 2 requests at a time.
- The Collection tab makes no network call: unlock state is read from the game.

## Installation

1. In game, open **Umbra settings → Plugins**.
2. Add a GitHub repository: owner `Soratix`, repository `UmbraCurrencyPlus`.
3. Switch the toolbar to edit mode and add the **Currencies+** widget (« Monnaies+ » when Umbra is in French). You can also search for `currency`, `tomestone` or `universalis`.
4. Pick your currencies in the **Tracked currencies** tab of the widget settings.

## Building it yourself

You need the .NET 10 SDK and XIVLauncher with Umbra installed: the references point to `%AppData%\XIVLauncher`.

```powershell
dotnet build -c Release CurrencyPlus\CurrencyPlus.csproj
```

## Known limitations

- **Currencies not covered:** gil (regular shops), Free Company credits and Orbitingway lottery credits (random items).
- **Shops:** items are read from every shop in the game (SpecialShop, Grand Company shops), not NPC by NPC. Seasonal event shops are excluded, but an item may show up even if its shop is not unlocked for you yet (Grand Company rank, allied society reputation, quest…).
- **Multi-currency trades:** an item that costs several different currencies at once is skipped.
- **Prices:** these are past sales, not current listings. Universalis data depends on players uploading it, so a rarely traded item may have a rough or missing price.

## Credits

The shop currency ID conversion and the list of event shops come from [CurrencySpender](https://github.com/Blackcatz1911/CurrencySpender) (Blackcatz1911, AGPL-3.0).

## License

AGPL-3.0, like Umbra.
