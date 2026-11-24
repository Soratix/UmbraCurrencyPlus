using Dalamud.Bindings.ImGui;

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
    ["currency", "currencies", "monnaie", "monnaies", "tomestone", "mémoquartz", "scrip", "assignat", "seal", "sceau", "universalis", "spend", "collection"]
);

[LocalizedToolbarWidget]
public sealed class CurrencyPlusWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : ToolbarWidget(info, guid, configValues)
{
    private CurrencyPlusPopup? _popup;

    public override WidgetPopup? Popup => _popup ??= new CurrencyPlusPopup(this);

    public override Node Node { get; } = new() {
        ClassList  = ["cp-bar"],
        Stylesheet = BarStylesheet,
        ChildNodes = [
            new() { ClassList = ["cp-empty"] },
        ],
    };

    private readonly Dictionary<uint, Node> _itemNodes = [];
    private          List<CurrencyState>    _states    = [];
    private          string                 _signature = string.Empty;
    private          DateTime               _nextRead  = DateTime.MinValue;

    /// <summary>Monnaies suivies, dans l'ordre de la config, lues au dernier rafraîchissement.</summary>
    public IReadOnlyList<CurrencyState> States => _states;

    // ------------------------------------------------------------------ Config

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        string cat = Strings.T("WidgetName");

        return [
            new BooleanWidgetConfigVariable("Decorate", Strings.T("Cfg.Decorate"), Strings.T("Cfg.Decorate.Desc"), true) { Category = cat },
            new BooleanWidgetConfigVariable("ShowCap", Strings.T("Cfg.ShowCap"), Strings.T("Cfg.ShowCap.Desc"), false) { Category = cat },
            new IntegerWidgetConfigVariable("AlertThreshold", Strings.T("Cfg.Threshold"), Strings.T("Cfg.Threshold.Desc"), 90, 1, 100) { Category = cat },
            new ColorWidgetConfigVariable("AlertColor", Strings.T("Cfg.AlertColor"), null, 0xFF40A0FF) { Category = cat },
            new SelectWidgetConfigVariable(
                "Market", Strings.T("Cfg.Market"), Strings.T("Cfg.Market.Desc"), "World",
                new() { ["World"] = Strings.T("Cfg.Market.World"), ["DataCenter"] = Strings.T("Cfg.Market.DC") }
            ) { Category = cat },
            new IntegerWidgetConfigVariable("CacheHours", Strings.T("Cfg.CacheHours"), Strings.T("Cfg.CacheHours.Desc"), 3, 1, 48) { Category = cat },
            new IntegerWidgetConfigVariable("MaxRows", Strings.T("Cfg.MaxRows"), null, 150, 10, 500) { Category = cat },
            ..GetCurrencyVariables(),
        ];
    }

    private static IEnumerable<IWidgetConfigVariable> GetCurrencyVariables()
    {
        string cat = Strings.T("Cfg.Currencies");

        foreach (var def in Currencies.Defaults) {
            yield return new BooleanWidgetConfigVariable(TrackVar(def), CurrencyLabel(def), Strings.T("Cfg.Track.Desc"), def.DefaultTracked) {
                Category = cat,
            };
        }

        yield return new StringWidgetConfigVariable("CustomIds", Strings.T("Cfg.CustomIds"), Strings.T("Cfg.CustomIds.Desc"), "", 256) {
            Category = cat,
        };
    }

    private static string TrackVar(CurrencyDef def) => $"Track_{def.Key}";

    /// <summary>Libellé de la case à cocher : nom de l'objet (langue du client) ou description pour les monnaies variables.</summary>
    private static string CurrencyLabel(CurrencyDef def)
    {
        string? fixedLabel = def.Key switch {
            "GCSeals"        => Strings.T("Cfg.GCSeals"),
            "LimitedTome"    => Strings.T("Cfg.LimitedTome"),
            "NonLimitedTome" => Strings.T("Cfg.NonLimitedTome"),
            _                => null,
        };

        string? itemName = null;
        try {
            uint id = def.ResolveItemId();
            if (id != 0) itemName = Currencies.Read(id)?.Name;
        } catch {
            // Données indisponibles (personnage non connecté…) : on garde la clé.
        }

        if (fixedLabel != null) return itemName != null ? $"{fixedLabel} ({itemName})" : fixedLabel;
        return itemName ?? def.Key;
    }

    // ------------------------------------------------------------- Lifecycle

    protected override void Initialize()
    {
        // Enregistré avant le gestionnaire d'Umbra (ajouté dans Setup après Initialize) :
        // on sait quelle monnaie est sous la souris quand la popup s'ouvre.
        Node.OnMouseDown += _ => {
            Vector2 mouse = ImGui.GetMousePos();
            foreach (var (id, node) in _itemNodes) {
                if (node.Bounds.MarginRect.Contains(mouse)) {
                    (_popup ??= new CurrencyPlusPopup(this)).Select(id);
                    break;
                }
            }
        };
    }

    protected override void OnUpdate()
    {
        Node.ToggleTag("ghost", !GetConfigValue<bool>("Decorate"));

        if (DateTime.UtcNow < _nextRead) return;
        _nextRead = DateTime.UtcNow.AddMilliseconds(500);

        try {
            _states = ReadTrackedCurrencies();
        } catch (Exception e) {
            Logger.Error($"[CurrencyPlus] Lecture des monnaies impossible : {e.Message}");
            return;
        }

        RebuildIfNeeded();
        UpdateNodes();
    }

    /// <summary>ID des monnaies suivies : cases cochées puis ID personnalisés, sans doublon.</summary>
    public List<uint> TrackedIds()
    {
        List<uint> ids = [];

        foreach (var def in Currencies.Defaults) {
            if (!GetConfigValue<bool>(TrackVar(def))) continue;

            uint id = def.ResolveItemId();
            if (id != 0 && !ids.Contains(id)) ids.Add(id);
        }

        foreach (string part in GetConfigValue<string>("CustomIds").Split(',', ';', ' ')) {
            if (uint.TryParse(part.Trim(), out uint id) && id != 0 && !ids.Contains(id)) ids.Add(id);
        }

        return ids;
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

    // ----------------------------------------------------------------- Display

    private void RebuildIfNeeded()
    {
        string signature = string.Join(",", _states.Select(s => s.ItemId));
        if (signature == _signature) return;
        _signature = signature;

        Node.Clear();
        _itemNodes.Clear();

        if (_states.Count == 0) {
            Node.AppendChild(new() { ClassList = ["cp-empty"], NodeValue = Strings.T("NoCurrency") });
            return;
        }

        foreach (var s in _states) {
            var node = new Node {
                ClassList  = ["cp-item"],
                ChildNodes = [
                    new() { ClassList = ["cp-icon"], Style = new() { IconId = s.IconId } },
                    new() { ClassList = ["cp-text"] },
                ],
            };

            _itemNodes[s.ItemId] = node;
            Node.AppendChild(node);
        }
    }

    private void UpdateNodes()
    {
        bool showCap = GetConfigValue<bool>("ShowCap");
        var  alert   = AlertColor;

        List<string> tooltip = [];

        foreach (var s in _states) {
            tooltip.Add(s.Cap > 0 ? $"{s.Name} : {s.Count:N0} / {s.Cap:N0}" : $"{s.Name} : {s.Count:N0}");

            if (!_itemNodes.TryGetValue(s.ItemId, out var node)) continue;

            var text = node.QuerySelector(".cp-text")!;
            text.NodeValue   = showCap && s.Cap > 0 ? $"{s.Count:N0}/{s.Cap:N0}" : $"{s.Count:N0}";
            text.Style.Color = IsAlert(s) ? alert : null;
        }

        if (tooltip.Count > 0) {
            tooltip.Add("");
            tooltip.Add(Strings.T("TooltipHint"));
        }

        Node.Tooltip = tooltip.Count > 0 ? string.Join("\n", tooltip) : null;
    }

    // -------------------------------------------------------------- Stylesheet

    private static Stylesheet BarStylesheet { get; } = new([
        new(".cp-bar", new() {
            Flow            = Flow.Horizontal,
            Anchor          = Anchor.MiddleLeft,
            Size            = new(0, SafeHeight),
            Gap             = 10,
            Padding         = new(0, 8),
            BackgroundColor = new("Widget.Background"),
            StrokeColor     = new("Widget.Border"),
            StrokeWidth     = 1,
            StrokeInset     = 1,
            BorderRadius    = 5,
        }),
        new(".cp-bar:ghost", new() {
            BackgroundColor = new(0),
            StrokeColor     = new(0),
        }),
        new(".cp-item", new() {
            Flow   = Flow.Horizontal,
            Anchor = Anchor.MiddleLeft,
            Gap    = 4,
            Size   = new(0, SafeHeight),
        }),
        new(".cp-icon", new() {
            Anchor = Anchor.MiddleLeft,
            Size   = new(20, 20),
        }),
        new(".cp-text", TextStyle()),
        new(".cp-empty", TextStyle()),
    ]);

    private static Style TextStyle() => new() {
        Anchor       = Anchor.MiddleLeft,
        TextAlign    = Anchor.MiddleLeft,
        Size         = new(0, SafeHeight),
        FontSize     = 13,
        Color        = new("Widget.Text"),
        OutlineColor = new("Widget.TextOutline"),
        OutlineSize  = 1,
        WordWrap     = false,
        TextOverflow = false,
    };
}
