using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Umbra.CurrencyPlus;

/// <summary>Statistiques de vente d'un objet sur les 7 derniers jours.</summary>
public sealed record MarketStats(uint ItemId, double MedianPrice, double Sales7Days);

public enum PriceStatus { Idle, Loading, Ready, Error }

/// <summary>Résultat d'une requête de prix pour une monnaie sur un marché.</summary>
public sealed class PriceSnapshot
{
    public PriceStatus                       Status    { get; init; }
    public IReadOnlyDictionary<uint, MarketStats> Stats { get; init; } = new Dictionary<uint, MarketStats>();
    public DateTime                          FetchedAt { get; init; }
    public string?                           Error     { get; init; }
    public string                            Market    { get; init; } = string.Empty;

    /// <summary>Change à chaque nouvel état : sert à savoir s'il faut redessiner.</summary>
    public int Version { get; init; }
}

/// <summary>
/// Prix Universalis, avec cache par (monnaie, marché).
///
/// Endpoint utilisé : GET /api/v2/history/{monde ou DC}/{ids}?entriesWithin=604800&amp;entriesToReturn=N
///  - au plus 100 ID par requête (le serveur ignore les suivants : .Take(100) dans HistoryController) ;
///  - entries[].pricePerUnit : ventes des 7 derniers jours (entriesWithin, en secondes) → médiane ;
///  - regularSaleVelocity : ventes par jour, calculées sur statsWithin (7 jours par défaut) → × 7.
/// Limites de l'API : 25 req/s (pointe à 50) et 8 connexions simultanées par IP. On en ouvre au plus 2.
/// L'endpoint « aggregated » n'est pas utilisé : ses moyennes portent sur 4 jours, pas 7.
/// </summary>
public static class PriceService
{
    private const string BaseUrl        = "https://universalis.app/api/v2/history/";
    private const int    BatchSize      = 100;
    private const int    EntriesPerItem = 100;
    private const int    SevenDaysSecs  = 7 * 24 * 3600;

    private static readonly HttpClient Http = CreateClient();
    private static readonly SemaphoreSlim Throttle = new(2);
    private static readonly ConcurrentDictionary<string, PriceSnapshot> Cache = new();
    private static int _version;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        string version = typeof(PriceService).Assembly.GetName().Version?.ToString(3) ?? "0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"UmbraCurrencyPlus/{version}");
        return client;
    }

    private static string Key(uint currencyId, string market) => $"{currencyId}|{market}";

    /// <summary>État actuel (sans déclencher de requête).</summary>
    public static PriceSnapshot Get(uint currencyId, string market)
        => Cache.TryGetValue(Key(currencyId, market), out var s) ? s : new PriceSnapshot { Status = PriceStatus.Idle, Market = market };

    /// <summary>
    /// Lance une requête si le cache est vide, périmé ou en erreur (ou si force = true).
    /// Ne bloque jamais : le travail se fait sur le pool de threads.
    /// </summary>
    public static void Request(uint currencyId, string market, IReadOnlyCollection<uint> itemIds, TimeSpan ttl, bool force = false)
    {
        if (string.IsNullOrEmpty(market)) return;

        string key     = Key(currencyId, market);
        var    current = Get(currencyId, market);

        if (current.Status == PriceStatus.Loading) return;
        if (!force && current.Status == PriceStatus.Ready && DateTime.UtcNow - current.FetchedAt < ttl) return;

        if (itemIds.Count == 0) {
            Cache[key] = new() { Status = PriceStatus.Ready, FetchedAt = DateTime.UtcNow, Market = market, Version = Interlocked.Increment(ref _version) };
            return;
        }

        Cache[key] = new() {
            Status    = PriceStatus.Loading,
            Stats     = current.Stats,
            FetchedAt = current.FetchedAt,
            Market    = market,
            Version   = Interlocked.Increment(ref _version),
        };

        var ids = itemIds.Distinct().ToList();
        _ = Task.Run(() => FetchAll(key, market, ids));
    }

    private static async Task FetchAll(string key, string market, List<uint> ids)
    {
        try {
            var batches = ids.Chunk(BatchSize).Select(batch => FetchBatch(market, batch));
            var results = await Task.WhenAll(batches);

            Dictionary<uint, MarketStats> stats = [];
            foreach (var r in results) {
                foreach (var (id, s) in r) stats[id] = s;
            }

            Cache[key] = new() {
                Status    = PriceStatus.Ready,
                Stats     = stats,
                FetchedAt = DateTime.UtcNow,
                Market    = market,
                Version   = Interlocked.Increment(ref _version),
            };
        } catch (Exception e) {
            Logger.Error($"[CurrencyPlus] Échec de la requête Universalis ({market}) : {e.Message}");
            Cache[key] = new() {
                Status  = PriceStatus.Error,
                Error   = e is TaskCanceledException ? "timeout" : e.Message,
                Market  = market,
                Version = Interlocked.Increment(ref _version),
            };
        }
    }

    private static async Task<Dictionary<uint, MarketStats>> FetchBatch(string market, uint[] ids)
    {
        string url = $"{BaseUrl}{Uri.EscapeDataString(market)}/{string.Join(",", ids)}"
                   + $"?entriesWithin={SevenDaysSecs}&entriesToReturn={EntriesPerItem}";

        await Throttle.WaitAsync();
        try {
            using var response = await Http.GetAsync(url);

            // Un lot sans aucun objet connu d'Universalis renvoie 404 : ce n'est pas une erreur pour nous.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var       doc    = await JsonDocument.ParseAsync(stream);

            return Parse(doc.RootElement);
        } finally {
            Throttle.Release();
        }
    }

    /// <summary>
    /// Avec plusieurs ID, la réponse est { "items": { "id": {...} } } ;
    /// avec un seul ID, c'est directement l'objet { "itemID": ..., "entries": [...] }.
    /// </summary>
    private static Dictionary<uint, MarketStats> Parse(JsonElement root)
    {
        Dictionary<uint, MarketStats> result = [];

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object) {
            foreach (var prop in items.EnumerateObject()) {
                var s = ParseItem(prop.Value);
                if (s != null) result[s.ItemId] = s;
            }
        } else if (root.TryGetProperty("itemID", out _)) {
            var s = ParseItem(root);
            if (s != null) result[s.ItemId] = s;
        }

        return result;
    }

    private static MarketStats? ParseItem(JsonElement item)
    {
        if (!item.TryGetProperty("itemID", out var idProp) || !idProp.TryGetUInt32(out uint id)) return null;

        List<double> prices = [];
        if (item.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array) {
            foreach (var e in entries.EnumerateArray()) {
                if (e.TryGetProperty("pricePerUnit", out var p) && p.TryGetDouble(out double price) && price > 0) {
                    prices.Add(price);
                }
            }
        }

        double velocity = item.TryGetProperty("regularSaleVelocity", out var v) && v.TryGetDouble(out double vel) ? vel : 0;

        return new(id, Median(prices), Math.Round(velocity * 7, 1));
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;

        values.Sort();
        int mid = values.Count / 2;
        return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
    }
}
