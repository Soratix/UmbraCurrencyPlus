using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace Umbra.CurrencyPlus;

/// <summary>Une monnaie proposée dans les réglages. Key sert d'identifiant de config, jamais traduit.</summary>
public sealed record CurrencyDef(string Key, bool DefaultTracked, Func<uint> ResolveItemId);

/// <summary>État courant d'une monnaie, lu dans le jeu.</summary>
public sealed record CurrencyState(uint ItemId, string Name, uint IconId, int Count, int Cap)
{
    public float Ratio => Cap > 0 ? (float)Count / Cap : 0f;
}

public static class Currencies
{
    public const uint Poetics          = 28;
    public const uint AlliedSeals      = 27;
    public const uint CenturioSeals    = 10307;
    public const uint SackOfNuts       = 26533;
    public const uint WolfMarks        = 25;
    public const uint TrophyCrystals   = 36656;
    public const uint PurpleCrafter    = 33913;
    public const uint PurpleGatherer   = 33914;
    public const uint OrangeCrafter    = 41784;
    public const uint OrangeGatherer   = 41785;
    public const uint SkyBuilders      = 28063;
    public const uint BiColorGemstones = 26807;
    public const uint Mgp              = 29;

    /// <summary>Sceaux de Grande Compagnie : Maelstrom (1) → 20, Deux Vipères (2) → 21, Immortels (3) → 22.</summary>
    public static uint GrandCompanySeal(uint grandCompanyId) => grandCompanyId is >= 1 and <= 3 ? grandCompanyId + 19 : 0;

    public static bool IsGrandCompanySeal(uint itemId) => itemId is 20 or 21 or 22;

    /// <summary>Monnaies proposées par défaut, dans l'ordre de la config.</summary>
    public static readonly IReadOnlyList<CurrencyDef> Defaults = [
        new("Poetics", true, () => Poetics),
        new("NonLimitedTome", false, () => NonLimitedTomestone),
        new("LimitedTome", true, () => LimitedTomestone),
        new("GCSeals", true, () => GrandCompanySeal(Framework.Service<IPlayer>().GrandCompanyId)),
        new("AlliedSeals", false, () => AlliedSeals),
        new("CenturioSeals", false, () => CenturioSeals),
        new("SackOfNuts", false, () => SackOfNuts),
        new("WolfMarks", false, () => WolfMarks),
        new("TrophyCrystals", false, () => TrophyCrystals),
        new("PurpleCrafter", false, () => PurpleCrafter),
        new("PurpleGatherer", false, () => PurpleGatherer),
        new("OrangeCrafter", false, () => OrangeCrafter),
        new("OrangeGatherer", false, () => OrangeGatherer),
        new("SkyBuilders", false, () => SkyBuilders),
        new("BiColorGemstones", true, () => BiColorGemstones),
        new("Mgp", false, () => Mgp),
    ];

    private static uint? _limitedTome;
    private static uint? _nonLimitedTome;

    /// <summary>Mémoquartz limité de la saison (Tomestones = 3 dans TomestonesItem).</summary>
    public static uint LimitedTomestone => _limitedTome ??= FindTomestone(3);

    /// <summary>Mémoquartz non limité de la saison (Tomestones = 2 dans TomestonesItem).</summary>
    public static uint NonLimitedTomestone => _nonLimitedTome ??= FindTomestone(2);

    private static uint FindTomestone(uint tomestonesId)
    {
        foreach (var row in Framework.Service<IDataManager>().GetExcelSheet<TomestonesItem>()) {
            if (row.Tomestones.RowId == tomestonesId) return row.Item.RowId;
        }

        return 0;
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
