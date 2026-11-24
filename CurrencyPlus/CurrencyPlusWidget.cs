namespace Umbra.CurrencyPlus;

/// <summary>
/// Le nom et la description d'un [ToolbarWidget] sont des constantes. Umbra lit l'attribut
/// avec GetCustomAttribute au démarrage de son framework, ce qui accepte une sous-classe :
/// celle-ci choisit le nom et la description dans la langue d'Umbra à ce moment-là.
/// </summary>
internal sealed class LocalizedToolbarWidgetAttribute() : ToolbarWidgetAttribute(
    "CurrencyPlus",
    Strings.T("WidgetName"),
    Strings.T("WidgetDesc"),
    ["currency", "currencies", "monnaie", "monnaies", "devises", "tomestone", "mémoquartz", "scrip", "assignat", "seal", "sceau", "universalis", "spend", "collection"]
);

/// <summary>
/// Une icône dans la barre. Au clic, la popup montre les monnaies suivies et ce qu'on peut en faire.
/// L'icône prend la couleur d'alerte quand une monnaie suivie approche de son plafond.
/// </summary>
[LocalizedToolbarWidget]
public sealed class CurrencyPlusWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    /// <summary>Colonnes facultatives de l'onglet « Rentable » (identifiants internes).</summary>
    public static readonly string[] ProfitColumns = ["Cost", "Price", "Sales", "GilPerPoint", "Score"];

    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.CustomizableIcon;

    protected override string          DefaultIconType        => IconTypeFontAwesome;
    protected override FontAwesomeIcon DefaultFontAwesomeIcon => FontAwesomeIcon.Coins;

    private CurrencyPlusPopup? _popup;

    public override WidgetPopup? Popup => _popup ??= new CurrencyPlusPopup(this);

    private List<CurrencyState> _states   = [];
    private DateTime            _nextRead = DateTime.MinValue;

    /// <summary>Monnaies suivies, dans l'ordre choisi, lues au dernier rafraîchissement.</summary>
    public IReadOnlyList<CurrencyState> States => _states;

    // ------------------------------------------------------------------ Config

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        string cat     = Strings.T("WidgetName");
        string colsCat = Strings.T("Cfg.Columns");

        return [
            ..base.GetConfigVariables(),

            // Liste des monnaies suivies, modifiée depuis la popup (bouton « Gérer »), où elles s'affichent avec leur icône.
            new StringWidgetConfigVariable("TrackedIds", Strings.T("Cfg.TrackedIds"), Strings.T("Cfg.TrackedIds.Desc"), Currencies.DefaultTracked, 1024) {
                Category = cat,
                IsHidden = true,
            },
            new IntegerWidgetConfigVariable("AlertThreshold", Strings.T("Cfg.Threshold"), Strings.T("Cfg.Threshold.Desc"), 90, 1, 100) { Category = cat },
            new ColorWidgetConfigVariable("AlertColor", Strings.T("Cfg.AlertColor"), null, 0xFF40A0FF) { Category = cat },
            new BooleanWidgetConfigVariable("Teleport", Strings.T("Cfg.Teleport"), Strings.T("Cfg.Teleport.Desc"), true) { Category = cat },
            new SelectWidgetConfigVariable(
                "Market", Strings.T("Cfg.Market"), Strings.T("Cfg.Market.Desc"), "World",
                new() { ["World"] = Strings.T("Cfg.Market.World"), ["DataCenter"] = Strings.T("Cfg.Market.DC") }
            ) { Category = cat },
            new IntegerWidgetConfigVariable("CacheHours", Strings.T("Cfg.CacheHours"), Strings.T("Cfg.CacheHours.Desc"), 3, 1, 48) { Category = cat },
            new IntegerWidgetConfigVariable("MaxRows", Strings.T("Cfg.MaxRows"), null, 150, 10, 500) { Category = cat },
            ..ProfitColumns.Select(c => new BooleanWidgetConfigVariable($"Col_{c}", Strings.T($"Col.{c}"), Strings.T("Cfg.Column.Desc"), true) {
                Category = colsCat,
            }),
        ];
    }

    /// <summary>ID des monnaies suivies, dans l'ordre.</summary>
    public List<uint> TrackedIds()
    {
        string raw = GetConfigValue<string>("TrackedIds").Trim();
        if (raw == Currencies.DefaultTracked) return Currencies.DefaultIds().Where(id => id != 0).Distinct().ToList();

        List<uint> ids = [];
        foreach (string part in raw.Split(',', ';', ' ')) {
            if (uint.TryParse(part.Trim(), out uint id) && id != 0 && !ids.Contains(id)) ids.Add(id);
        }

        return ids;
    }

    /// <summary>Ajoute ou retire une monnaie (depuis le sélecteur de la popup).</summary>
    public void ToggleTracked(uint itemId)
    {
        var ids = TrackedIds();
        if (!ids.Remove(itemId)) ids.Add(itemId);

        SetConfigValue("TrackedIds", string.Join(",", ids));
        _nextRead = DateTime.MinValue;
    }

    public bool IsColumnVisible(string column) => GetConfigValue<bool>($"Col_{column}");

    // ------------------------------------------------------------- Lifecycle

    private bool _alert;

    protected override void OnLoad()
    {
        SetTooltip(Strings.T("WidgetName"));
    }

    protected override void OnDraw()
    {
        // Umbra réapplique la couleur d'icône du réglage à chaque image, juste avant OnDraw.
        if (_alert) SetIconColor(AlertColor);

        if (DateTime.UtcNow < _nextRead) return;
        _nextRead = DateTime.UtcNow.AddMilliseconds(500);

        try {
            _states = ReadTrackedCurrencies();
        } catch (Exception e) {
            Logger.Error($"[CurrencyPlus] Lecture des monnaies impossible : {e.Message}");
            return;
        }

        UpdateToolbar();
    }

    private List<CurrencyState> ReadTrackedCurrencies()
    {
        if (!Framework.Service<IClientState>().IsLoggedIn) return [];

        List<CurrencyState> states = [];
        foreach (uint id in TrackedIds()) {
            var s = Currencies.Read(id);
            if (s != null) states.Add(s);
        }

        return states;
    }

    public bool IsAlert(CurrencyState s) => s.Cap > 0 && s.Ratio * 100 >= GetConfigValue<int>("AlertThreshold");

    public Color AlertColor => new(GetConfigValue<uint>("AlertColor"));

    private void UpdateToolbar()
    {
        _alert = _states.Any(IsAlert);
        if (_alert) SetIconColor(AlertColor);

        List<string> lines = [Strings.T("WidgetName")];

        if (_states.Count == 0) {
            lines.Add(Strings.T("NoCurrency"));
        } else {
            lines.Add("");
            foreach (var s in _states) {
                string line = s.Cap > 0 ? $"{s.Name} : {s.Count:N0} / {s.Cap:N0}" : $"{s.Name} : {s.Count:N0}";
                lines.Add(IsAlert(s) ? $"⚠ {line}" : line);
            }
        }

        lines.Add("");
        lines.Add(Strings.T("TooltipHint"));

        SetTooltip(string.Join("\n", lines));
    }
}
