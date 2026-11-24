using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Umbra.CurrencyPlus;

/// <summary>Position d'un PNJ marchand dans le monde (feuille Level).</summary>
public sealed record NpcLocation(uint NpcId, uint TerritoryId, uint MapId, Vector3 Position);

/// <summary>
/// Boutique → PNJ qui la tiennent → positions.
///
/// Les boutiques sont rattachées aux PNJ en suivant ENpcBase.ENpcData, à travers CustomTalk,
/// TopicSelect et PreHandler, comme CurrencySpender (Blackcatz1911, AGPL-3.0), dont viennent aussi
/// les associations codées en dur pour les boutiques qu'aucune donnée ne relie à un PNJ
/// (échange d'assignats, etc.). Les positions viennent de la feuille Level (Type 8 = PNJ).
/// </summary>
public static class NpcTable
{
    /// <summary>Clé de boutique pour les sceaux de Grande Compagnie (les SpecialShop gardent leur RowId).</summary>
    public static uint GrandCompanyShopKey(uint grandCompanyId) => 0xFF000000u + grandCompanyId;

    public sealed record Result(
        Dictionary<uint, HashSet<uint>>     ShopNpcs,
        Dictionary<uint, List<NpcLocation>> NpcLocations
    );

    private static IEnumerable<uint> Range(uint start, uint end)
    {
        for (uint i = start; i <= end; i++) yield return i;
    }

    /// <summary>Boutiques sans lien dans les données, rattachées à la main (CurrencySpender : npcMapping).</summary>
    private static IEnumerable<(uint Shop, uint Npc)> ManualMappings()
    {
        // Échange d'assignats (Limsa ; variantes à Gridania et Ul'dah).
        uint[] scripNpcs = [1003633, 1001617, 1003077];
        IEnumerable<uint> scripShops = [
            ..Range(1770112, 1770114), ..Range(1770183, 1770196), ..Range(1770264, 1770268), ..Range(1770272, 1770278),
            ..Range(1770420, 1770431), ..Range(1770477, 1770510), ..Range(1770518, 1770535), ..Range(1770625, 1770631),
            ..Range(1770706, 1770707), ..Range(1770781, 1770794), ..Range(1770868, 1770882), 1770907, 1770943, 1770944,
            1771000, 1771001,
        ];
        foreach (uint shop in scripShops) {
            foreach (uint npc in scripNpcs) yield return (shop, npc);
        }

        (uint[] Shops, uint Npc)[] others = [
            ([1769577, 1769578], 1012225),                                                      // Ardolain
            ([1769790, 1769791, 1769819, 1769814, 1769854, 1769883, 1769873, 1769940, 1769807], 1019451), // Eschina
            ([1769743, 1769744, 1770537], 1018655),                                             // Prêtre peu recommandable
            ([..Range(1770551, 1770589), 1770888, 1770889], 1005244),                           // Intendant des marques
            ([1770637, 1770638], 1045069),                                                      // Quinnana
            ([1770601, 1770602, 1770659, 1770660], 1043463),
            ([1770604, 1770643, 1770662, 1770709], 1043465),
            ([1769957], 1027998), ([1769958], 1027538), ([1769959], 1027385), ([1769960], 1027497),
            ([1769961], 1027892), ([1769962], 1027665), ([1769963], 1027709), ([1769964], 1027766),
            ([1770904], 1044839),
            ([1770282], 1033921),
            ([1770764, 1770765], 1048387),
            ([1770766, 1770767], 1049079),
        ];
        foreach (var (shops, npc) in others) {
            foreach (uint shop in shops) yield return (shop, npc);
        }
    }

    public static Result Build(ExcelModule data)
    {
        Dictionary<uint, HashSet<uint>> shopNpcs = [];

        void Link(uint shopKey, uint npcId)
        {
            if (!shopNpcs.TryGetValue(shopKey, out var set)) shopNpcs[shopKey] = set = [];
            set.Add(npcId);
        }

        var customTalks  = data.GetSheet<CustomTalk>();
        var topicSelects = data.GetSheet<TopicSelect>();
        var preHandlers  = data.GetSheet<PreHandler>();

        Type[] scriptTypes = [typeof(SpecialShop), typeof(GCShop), typeof(CustomTalk), typeof(TopicSelect), typeof(PreHandler)];
        int    scriptHash  = RowRef.CreateTypeHash(scriptTypes);

        void Visit(uint npcId, RowRef rowRef, int depth)
        {
            if (depth > 4 || rowRef.RowId == 0) return;

            if (rowRef.Is<SpecialShop>()) {
                Link(rowRef.RowId, npcId);
            } else if (rowRef.Is<GCShop>()) {
                var gc = data.GetSheet<GCShop>().GetRowOrDefault(rowRef.RowId);
                if (gc != null) Link(GrandCompanyShopKey(gc.Value.GrandCompany.RowId), npcId);
            } else if (rowRef.Is<CustomTalk>()) {
                var talk = customTalks.GetRowOrDefault(rowRef.RowId);
                if (talk == null) return;

                Visit(npcId, talk.Value.SpecialLinks, depth + 1);
                foreach (var script in talk.Value.Script) {
                    if (script.ScriptArg == 0) continue;
                    // Le hash des types est calculé une fois pour toutes plus haut : CA1857 voudrait une constante.
#pragma warning disable CA1857
                    var target = RowRef.GetFirstValidRowOrUntyped(data, script.ScriptArg, scriptTypes, scriptHash, null);
#pragma warning restore CA1857
                    Visit(npcId, target, depth + 1);
                }
            } else if (rowRef.Is<TopicSelect>()) {
                var topic = topicSelects.GetRowOrDefault(rowRef.RowId);
                if (topic == null) return;
                foreach (var shop in topic.Value.Shop) Visit(npcId, shop, depth + 1);
            } else if (rowRef.Is<PreHandler>()) {
                var pre = preHandlers.GetRowOrDefault(rowRef.RowId);
                if (pre != null) Visit(npcId, pre.Value.Target, depth + 1);
            }
        }

        foreach (var npc in data.GetSheet<ENpcBase>()) {
            foreach (var rowRef in npc.ENpcData) Visit(npc.RowId, rowRef, 0);
        }

        // Marchands de F.A.T.E. : la ligne de FateShop porte l'ID du PNJ.
        foreach (var fate in data.GetSheet<FateShop>()) {
            foreach (var shop in fate.SpecialShop) {
                if (shop.RowId != 0) Link(shop.RowId, fate.RowId);
            }
        }

        foreach (var (shop, npc) in ManualMappings()) Link(shop, npc);

        // Positions des PNJ.
        Dictionary<uint, List<NpcLocation>> locations = [];
        foreach (var level in data.GetSheet<Level>()) {
            if (level.Type != 8 || level.Object.RowId == 0 || level.Territory.RowId == 0 || level.Map.RowId == 0) continue;

            uint npcId = level.Object.RowId;
            if (!locations.TryGetValue(npcId, out var list)) locations[npcId] = list = [];
            list.Add(new(npcId, level.Territory.RowId, level.Map.RowId, new(level.X, level.Y, level.Z)));
        }

        return new(shopNpcs, locations);
    }
}
