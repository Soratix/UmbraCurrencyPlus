using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Umbra.CurrencyPlus;

/// <summary>Un objet achetable avec une monnaie. Cost est le prix pour Quantity exemplaires.</summary>
public sealed record ShopEntry(
    uint   CurrencyId,
    uint   ItemId,
    string Name,
    uint   IconId,
    uint   Cost,
    uint   Quantity,
    bool   IsMarketable,
    string Category
)
{
    /// <summary>Coût en monnaie d'un exemplaire.</summary>
    public double CostPerUnit => Quantity > 0 ? (double)Cost / Quantity : Cost;

    /// <summary>Boutiques qui vendent l'objet à ce prix : RowId de SpecialShop, ou NpcTable.GrandCompanyShopKey.</summary>
    public IReadOnlyList<uint> ShopKeys { get; init; } = [];
}

/// <summary>
/// Table monnaie → objets, à partir de SpecialShop (mémoquartz, assignats, sceaux de chasse,
/// pierres bicolores, monnaies de tribus…) et de GCScripShopItem (sceaux de Grande Compagnie).
///
/// La conversion des identifiants de monnaie (UseCurrencyType) et la liste des boutiques
/// d'événement ou obsolètes sont reprises de CurrencySpender (Blackcatz1911, AGPL-3.0).
/// </summary>
public static class ShopTable
{
    private static readonly HashSet<uint> ExcludedShops = [
        // Boutiques d'événement (CurrencySpender : EventShops / EventShopBlacklist).
        1770595, 1770645, 1770729,
        1769474, 1769747, 1769875, 1769881, 1769882, 1769952, 1769953, 1769954, 1769955,
        1770283, 1770284, 1770336, 1770341,
        1770596, 1770646, 1770730,
        1769951,
        1769802, 1769983,
        // Anciennes boutiques (CurrencySpender : OldShops).
        1770434, 1770435, 1770436, 1770437, 1770438, 1770446, 1770267,
        1770767, 1770768, 1770769, 1770770,
    ];

    /// <summary>Monnaies « spéciales » désignées par un index (UseCurrencyType 16).</summary>
    private static readonly Dictionary<uint, uint> SpecialCurrencies = new() {
        [1]  = 10309, [2]  = 33913, [3]  = 10311, [4]  = 33914, [5]  = 10307, [6]  = 41784, [7]  = 41785,
        [8]  = 21072, [9]  = 21073, [10] = 21074, [11] = 21075, [12] = 21076, [13] = 21077, [14] = 21078,
        [15] = 21079, [16] = 21080, [17] = 21081, [18] = 21172, [19] = 21173, [20] = 21935, [21] = 22525,
        [22] = 26533, [23] = 26807, [24] = 28063, [25] = 28186, [26] = 28187, [27] = 28188, [28] = 30341,
    };

    private static uint ConvertCurrencyId(uint shopId, uint itemId, byte useCurrencyType, Dictionary<uint, uint> tomestones)
    {
        switch (shopId) {
            case 1770637:
                return SpecialCurrencies.GetValueOrDefault(itemId, itemId);
            case 1770638:
                return 33914; // assignat violet de récolteur
            case 1770446 or 1770699 when itemId < 10:
                if (SpecialCurrencies.TryGetValue(itemId, out var c) || tomestones.TryGetValue(itemId, out c)) return c;
                return itemId;
        }

        if (itemId < 10) {
            switch (useCurrencyType) {
                case 2 or 4: return tomestones.GetValueOrDefault(itemId, itemId);
                case 16:     return SpecialCurrencies.GetValueOrDefault(itemId, itemId);
            }
        }

        return itemId;
    }

    /// <summary>Construit la table. N'utilise que Lumina : peut tourner hors du jeu (tests).</summary>
    public static Dictionary<uint, List<ShopEntry>> Build(ExcelModule data)
    {
        var items = data.GetSheet<Item>();

        // Index de mémoquartz utilisés par UseCurrencyType 2 et 4 : 1 = Poétique, 2 = non limité, 3 = limité.
        Dictionary<uint, uint> tomestones = new() { [1] = 28 };
        foreach (var row in data.GetSheet<TomestonesItem>()) {
            if (row.Tomestones.RowId is 2 or 3) tomestones[row.Tomestones.RowId] = row.Item.RowId;
        }

        // (monnaie, objet) → entrée la moins chère par exemplaire.
        Dictionary<(uint, uint), ShopEntry>     best  = [];
        Dictionary<(uint, uint), HashSet<uint>> shops = [];

        void Add(uint currencyId, uint itemId, uint cost, uint quantity, uint shopKey)
        {
            if (currencyId == 0 || itemId == 0 || cost == 0 || currencyId == itemId) return;

            var item = items.GetRowOrDefault(itemId);
            if (item == null) return;

            string name = item.Value.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) return;

            var entry = new ShopEntry(
                currencyId,
                itemId,
                name,
                item.Value.Icon,
                cost,
                Math.Max(1, quantity),
                !item.Value.IsUntradable && item.Value.ItemSearchCategory.RowId != 0,
                item.Value.ItemUICategory.ValueNullable?.Name.ExtractText() ?? string.Empty
            );

            var key = (currencyId, itemId);
            if (!best.TryGetValue(key, out var existing) || entry.CostPerUnit < existing.CostPerUnit) {
                best[key]  = entry;
                shops[key] = [shopKey];
            } else if (Math.Abs(entry.CostPerUnit - existing.CostPerUnit) < 0.0001) {
                shops[key].Add(shopKey);
            }
        }

        foreach (var shop in data.GetSheet<SpecialShop>()) {
            if (ExcludedShops.Contains(shop.RowId)) continue;
            if (shop.RequiredFestival.RowId != 0) continue; // boutique d'événement saisonnier

            foreach (var entry in shop.Item) {
                int n = Math.Min(entry.ReceiveItems.Count, entry.ItemCosts.Count);

                for (int i = 0; i < n; i++) {
                    var receive = entry.ReceiveItems[i];
                    var cost    = entry.ItemCosts[i];
                    if (receive.Item.RowId == 0 || cost.ItemCost.RowId == 0) continue;

                    // Un échange qui demande plusieurs monnaies différentes ne se résume pas à un coût unique.
                    if (entry.ItemCosts.Count(c => c.ItemCost.RowId != 0) > 1) continue;

                    uint currency = ConvertCurrencyId(shop.RowId, cost.ItemCost.RowId, shop.UseCurrencyType, tomestones);
                    Add(currency, receive.Item.RowId, cost.CurrencyCost, receive.ReceiveCount, shop.RowId);
                }
            }
        }

        var categories = data.GetSheet<GCScripShopCategory>();
        foreach (var row in data.GetSubrowSheet<GCScripShopItem>()) {
            foreach (var sub in row) {
                var category = categories.GetRowOrDefault(sub.RowId);
                if (category == null) continue;

                uint gc       = category.Value.GrandCompany.RowId;
                uint currency = gc is >= 1 and <= 3 ? gc + 19 : 0; // sceaux : 20, 21, 22
                Add(currency, sub.Item.RowId, sub.CostGCSeals, 1, NpcTable.GrandCompanyShopKey(gc));
            }
        }

        return best
            .Select(kv => kv.Value with { ShopKeys = shops[kv.Key].ToList() })
            .GroupBy(e => e.CurrencyId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }
}
