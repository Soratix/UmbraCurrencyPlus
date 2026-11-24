using System.Threading;
using System.Threading.Tasks;

namespace Umbra.CurrencyPlus;

/// <summary>
/// Tables calculées une seule fois en tâche de fond et gardées en mémoire :
/// monnaie → objets (ShopTable) et boutique → PNJ → positions (NpcTable).
/// </summary>
public static class ShopData
{
    private sealed record Tables(Dictionary<uint, List<ShopEntry>> Shops, NpcTable.Result Npcs);

    private static Task<Tables>? _task;
    private static readonly Lock Gate = new();

    /// <summary>Vrai quand les tables sont prêtes.</summary>
    public static bool IsReady => _task is { IsCompletedSuccessfully: true };

    /// <summary>Message d'erreur si la construction a échoué.</summary>
    public static string? Error => _task is { IsFaulted: true } t ? t.Exception?.GetBaseException().Message : null;

    /// <summary>Lance la construction si elle n'a pas encore eu lieu. Ne bloque pas.</summary>
    public static void EnsureStarted()
    {
        lock (Gate) {
            _task ??= Task.Run(Build);
        }
    }

    /// <summary>Les objets achetables avec cette monnaie, ou null tant que la table n'est pas prête.</summary>
    public static IReadOnlyList<ShopEntry>? For(uint currencyId)
    {
        EnsureStarted();
        if (!IsReady) return null;

        return _task!.Result.Shops.TryGetValue(currencyId, out var list) ? list : [];
    }

    /// <summary>Positions connues des PNJ qui vendent cet objet à ce prix.</summary>
    public static List<NpcLocation> VendorsOf(ShopEntry entry)
    {
        if (!IsReady) return [];

        var npcs = _task!.Result.Npcs;
        List<NpcLocation> result = [];
        HashSet<uint> seen = [];

        foreach (uint key in entry.ShopKeys) {
            if (!npcs.ShopNpcs.TryGetValue(key, out var ids)) continue;

            foreach (uint npcId in ids) {
                if (!seen.Add(npcId) || !npcs.NpcLocations.TryGetValue(npcId, out var locations)) continue;
                result.AddRange(locations);
            }
        }

        return result;
    }

    private static Tables Build()
    {
        var started = DateTime.UtcNow;

        try {
            var excel  = Framework.Service<IDataManager>().Excel;
            var shops  = ShopTable.Build(excel);
            var npcs   = NpcTable.Build(excel);

            Logger.Info($"[CurrencyPlus] Tables : {shops.Sum(r => r.Value.Count)} objets pour {shops.Count} monnaies, "
                      + $"{npcs.ShopNpcs.Count} boutiques reliées à un PNJ, en {(DateTime.UtcNow - started).TotalMilliseconds:0} ms.");

            return new(shops, npcs);
        } catch (Exception e) {
            Logger.Error($"[CurrencyPlus] Construction des tables impossible : {e}");
            throw;
        }
    }
}
