using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace Umbra.CurrencyPlus;

/// <summary>Groupes du sélecteur de monnaies (identifiants internes, jamais traduits).</summary>
public enum CurrencyGroup { Tomestones, GrandCompany, Battle, CraftingGathering, Tribes, Other }

/// <summary>Une monnaie proposée dans le sélecteur.</summary>
public sealed record CatalogEntry(uint ItemId, string Name, uint IconId, CurrencyGroup Group);

/// <summary>État courant d'une monnaie, lu dans le jeu.</summary>
public sealed record CurrencyState(uint ItemId, string Name, uint IconId, int Count, int Cap)
{
    public float Ratio => Cap > 0 ? (float)Count / Cap : 0f;
}

public static class Currencies
{
    public const uint Poetics          = 28;
    public const uint BiColorGemstones = 26807;

    /// <summary>Sceaux de Grande Compagnie : Maelstrom (1) → 20, Deux Vipères (2) → 21, Immortels (3) → 22.</summary>
    public static uint GrandCompanySeal(uint grandCompanyId) => grandCompanyId is >= 1 and <= 3 ? grandCompanyId + 19 : 0;

    public static bool IsGrandCompanySeal(uint itemId) => itemId is 20 or 21 or 22;

    private static readonly HashSet<uint> BattleIds = [
        25, 27, 10307, 26533, 26807, 36656, // marques de loup, insignes, gemmes bicolores, cristaux-trophées
    ];

    private static readonly HashSet<uint> CraftingGatheringIds = [
        33913, 33914, 41784, 41785, 28063, // assignats, assignats d'Azurée
    ];

    /// <summary>Valeur de config par défaut : poétique, mémoquartz limité, sceaux, gemmes bicolores.</summary>
    public const string DefaultTracked = "default";

    public static List<uint> DefaultIds() => [
        Poetics,
        LimitedTomestone,
        GrandCompanySeal(Framework.Service<IPlayer>().GrandCompanyId),
        BiColorGemstones,
    ];

    private static uint? _limitedTome;

    /// <summary>Mémoquartz limité de la saison (Tomestones = 3 dans TomestonesItem).</summary>
    public static uint LimitedTomestone => _limitedTome ??= Framework.Service<IDataManager>()
        .GetExcelSheet<TomestonesItem>()
        .FirstOrDefault(r => r.Tomestones.RowId == 3).Item.RowId;

    /// <summary>
    /// Toutes les monnaies de la fenêtre « Devises » du jeu : celles que CurrencyManager connaît
    /// (ses trois listes), plus les sceaux de toutes les Grandes Compagnies, les monnaies des
    /// tribus alliées (BeastTribe.CurrencyItem), les mémoquartz de la saison, les PGS, et les
    /// monnaies déjà suivies (pour pouvoir toujours les retirer). À appeler sur le thread du jeu.
    /// </summary>
    public static unsafe List<CatalogEntry> Catalog(IEnumerable<uint> tracked)
    {
        var data  = Framework.Service<IDataManager>();
        var items = data.GetExcelSheet<Item>();

        HashSet<uint> tomestones = data.GetExcelSheet<TomestonesItem>().Select(r => r.Item.RowId).Where(id => id != 0).ToHashSet();
        HashSet<uint> tribes     = data.GetExcelSheet<BeastTribe>().Select(r => r.CurrencyItem.RowId).Where(id => id != 0).ToHashSet();

        // Comme la fenêtre du jeu : seulement les sceaux de la Grande Compagnie du personnage.
        HashSet<uint> ids = [29, ..tribes];
        uint seal = GrandCompanySeal(Framework.Service<IPlayer>().GrandCompanyId);
        if (seal != 0) ids.Add(seal);

        var cm = CurrencyManager.Instance();
        if (cm != null) {
            foreach (uint id in cm->ItemBucket.Keys) ids.Add(id);
            foreach (uint id in cm->ContentItemBucket.Keys) ids.Add(id);
            foreach (var special in cm->SpecialItemBucket.Values) ids.Add(cm->GetItemIdBySpecialId(special.SpecialId));
        }

        // Mémoquartz de la saison : poétique (1), non limité (2), limité (3). Ils ne figurent pas
        // toujours dans les listes de CurrencyManager, on les ajoute donc explicitement.
        // Les anciens mémoquartz n'ont plus de boutique : ils sont écartés.
        HashSet<uint> currentTomes = data.GetExcelSheet<TomestonesItem>()
            .Where(r => r.Tomestones.RowId is 1 or 2 or 3)
            .Select(r => r.Item.RowId)
            .ToHashSet();
        ids.UnionWith(currentTomes);

        HashSet<uint> trackedIds = tracked.ToHashSet();
        ids.UnionWith(trackedIds);

        List<CatalogEntry> result = [];
        foreach (uint id in ids) {
            if (id <= 1) continue; // gils : aucune boutique couverte
            if (!trackedIds.Contains(id)) {
                if (IsGrandCompanySeal(id) && id != seal) continue;
                if (tomestones.Contains(id) && !currentTomes.Contains(id)) continue;
            }

            var item = items.GetRowOrDefault(id);
            if (item == null) continue;

            string name = item.Value.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var group = true switch {
                _ when tomestones.Contains(id)           => CurrencyGroup.Tomestones,
                _ when IsGrandCompanySeal(id)            => CurrencyGroup.GrandCompany,
                _ when tribes.Contains(id)               => CurrencyGroup.Tribes,
                _ when BattleIds.Contains(id)            => CurrencyGroup.Battle,
                _ when CraftingGatheringIds.Contains(id) => CurrencyGroup.CraftingGathering,
                _                                        => CurrencyGroup.Other,
            };

            result.Add(new(id, name, item.Value.Icon, group));
        }

        return result
            .OrderBy(e => e.Group)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Lit la quantité et le plafond d'une monnaie. À appeler sur le thread du jeu.</summary>
    public static unsafe CurrencyState? Read(uint itemId)
    {
        if (itemId == 0) return null;

        var data = Framework.Service<IDataManager>();
        var item = data.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        if (item == null) return null;

        int count = Framework.Service<IPlayer>().GetItemCount(itemId);
        int cap   = (int)item.Value.StackSize;

        var cm = CurrencyManager.Instance();
        if (cm != null && cm->HasItem(itemId)) {
            uint max = cm->GetItemMaxCount(itemId);
            if (max > 0) cap = (int)max;
        }

        if (IsGrandCompanySeal(itemId)) {
            var ps = PlayerState.Instance();
            if (ps != null) {
                var rank = data.GetExcelSheet<GrandCompanyRank>().GetRowOrDefault(ps->GetGrandCompanyRank());
                if (rank != null) cap = (int)rank.Value.MaxSeals;
            }
        }

        return new(itemId, item.Value.Name.ExtractText(), item.Value.Icon, count, cap);
    }
}
