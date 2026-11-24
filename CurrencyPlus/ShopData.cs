using System.Threading;
using System.Threading.Tasks;

namespace Umbra.CurrencyPlus;

/// <summary>Table monnaie → objets (voir ShopTable), calculée une seule fois en tâche de fond et gardée en mémoire.</summary>
public static class ShopData
{
    private static Task<Dictionary<uint, List<ShopEntry>>>? _task;
    private static readonly Lock                           Gate = new();

    /// <summary>Vrai quand la table est prête.</summary>
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

        return _task!.Result.TryGetValue(currencyId, out var list) ? list : [];
    }

    private static Dictionary<uint, List<ShopEntry>> Build()
    {
        var started = DateTime.UtcNow;

        try {
            var result = ShopTable.Build(Framework.Service<IDataManager>().Excel);
            Logger.Info($"[CurrencyPlus] Table des boutiques : {result.Sum(r => r.Value.Count)} objets pour {result.Count} monnaies en {(DateTime.UtcNow - started).TotalMilliseconds:0} ms.");
            return result;
        } catch (Exception e) {
            Logger.Error($"[CurrencyPlus] Construction de la table des boutiques impossible : {e}");
            throw;
        }
    }
}
