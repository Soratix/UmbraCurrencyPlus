using System.Globalization;
using Lumina.Excel.Sheets;

namespace Umbra.CurrencyPlus;

/// <summary>
/// Popup du widget : la rangée des monnaies suivies (et le bouton « Gérer » qui ouvre le sélecteur
/// avec toutes les monnaies du jeu), puis deux onglets, chacun affiché seulement s'il a du contenu.
///  - Rentable : objets revendables, prix Universalis (médiane 7 j), triés par score = gils/point × ventes/7 j.
///  - Collection : objets à débloquer que le personnage n'a pas encore et ne possède pas (aucun appel réseau).
/// Clic sur un objet : drapeau sur le vendeur et téléportation à l'éthérite la plus proche.
/// </summary>
public sealed class CurrencyPlusPopup : WidgetPopup
{
    private enum Tab { Profit, Collection }

    private enum Column { Name, Cost, Price, Sales, GilPerPoint, Score, Type, Enough }

    private sealed record Row(ShopEntry Entry, double Price, double Sales, double GilPerPoint, double Score, bool Enough);

    private const int ListWidth  = 640;
    private const int ListHeight = 380;

    private static readonly Color YesColor = new(0xFF66DD66);
    private static readonly Color NoColor  = new(0xFF6666EE);

    private readonly CurrencyPlusWidget _widget;

    protected override Node Node { get; }

    private Node CurrencyBar => Node.QuerySelector(".cp-currencies")!;
    private Node Tabs        => Node.QuerySelector(".cp-tabs")!;
    private Node TabProfit   => Node.QuerySelector(".cp-tab.profit")!;
    private Node TabCollect  => Node.QuerySelector(".cp-tab.collection")!;
    private Node RefreshBtn  => Node.QuerySelector(".cp-refresh")!;
    private Node Header      => Node.QuerySelector(".cp-header")!;
    private Node List        => Node.QuerySelector(".cp-list")!;
    private Node Status      => Node.QuerySelector(".cp-status")!;
    private Node Footer      => Node.QuerySelector(".cp-footer")!;

    private uint   _selected;
    private Tab    _tab        = Tab.Profit;
    private Column _sortColumn = Column.Score;
    private bool   _sortDesc   = true;
    private bool   _forceRefresh;
    private bool   _managing;

    private string   _currencySignature = string.Empty;
    private string   _listSignature     = string.Empty;
    private DateTime _nextRequest       = DateTime.MinValue;

    // Objets de collection manquants, recalculés au plus toutes les 5 s.
    private string               _collectionKey = string.Empty;
    private List<ShopEntry>      _missing       = [];

    public CurrencyPlusPopup(CurrencyPlusWidget widget)
    {
        _widget = widget;

        Node = new() {
            Id         = "cp-popup",
            Stylesheet = PopupStylesheet,
            ChildNodes = [
                new() { ClassList = ["cp-currencies"] },
                new() {
                    ClassList  = ["cp-tabs"],
                    ChildNodes = [
                        new() { ClassList = ["cp-tab", "profit"] },
                        new() { ClassList = ["cp-tab", "collection"] },
                        new() { ClassList = ["cp-refresh"] },
                    ],
                },
                new() { ClassList = ["cp-header"] },
                new() { ClassList = ["cp-list"], Overflow = false },
                new() { ClassList = ["cp-status"] },
                new() { ClassList = ["cp-footer"] },
            ],
        };

        TabProfit.OnClick  += _ => SetTab(Tab.Profit);
        TabCollect.OnClick += _ => SetTab(Tab.Collection);
        RefreshBtn.OnClick += _ => {
            _forceRefresh  = true;
            _collectionKey = string.Empty;
            Invalidate();
        };
    }

    /// <summary>Choisit la monnaie affichée.</summary>
    public void Select(uint currencyId)
    {
        _selected = currencyId;
        _managing = false;
        Invalidate();
    }

    private void Invalidate()
    {
        _listSignature     = string.Empty;
        _currencySignature = string.Empty;
        _nextRequest       = DateTime.MinValue;
    }

    private void SetTab(Tab tab)
    {
        if (_tab == tab) return;

        _tab        = tab;
        _sortColumn = tab == Tab.Profit ? Column.Score : Column.Name;
        _sortDesc   = tab == Tab.Profit;
        Invalidate();
    }

    private void SetSort(Column column)
    {
        if (_sortColumn == column) {
            _sortDesc = !_sortDesc;
        } else {
            _sortColumn = column;
            _sortDesc   = column != Column.Name && column != Column.Type;
        }

        _listSignature = string.Empty;
    }

    // ---------------------------------------------------------------- Cycle

    protected override void OnOpen()
    {
        ShopData.EnsureStarted();

        _managing      = false;
        _collectionKey = string.Empty;
        Invalidate();

        TabProfit.NodeValue  = Strings.T("Tab.Profit");
        TabCollect.NodeValue = Strings.T("Tab.Collection");
        RefreshBtn.NodeValue = Strings.T("Refresh");
    }

    protected override void OnUpdate()
    {
        var states = _widget.States;

        if (states.Count > 0 && states.All(s => s.ItemId != _selected)) {
            _selected = states[0].ItemId;
        }

        UpdateCurrencyBar(states);

        if (_managing) {
            UpdateManager();
            return;
        }

        var state = states.FirstOrDefault(s => s.ItemId == _selected);
        if (state == null) {
            Tabs.Style.IsVisible = false;
            ShowMessage(Strings.T("NoCurrencyHint"));
            return;
        }

        var entries = ShopData.For(_selected);
        if (entries == null) {
            Tabs.Style.IsVisible = false;
            ShowMessage(ShopData.Error is { } err ? Strings.T("Error.Shops", err) : Strings.T("Loading.Shops"));
            return;
        }

        // Onglets affichés seulement s'il y a quelque chose dedans.
        var  marketable    = entries.Where(e => e.IsMarketable).ToList();
        var  missing       = MissingCollectibles(entries);
        bool hasProfit     = marketable.Count > 0;
        bool hasCollection = missing is { Count: > 0 };

        TabProfit.Style.IsVisible  = hasProfit;
        TabCollect.Style.IsVisible = hasCollection;
        Tabs.Style.IsVisible       = hasProfit || hasCollection;

        if (_tab == Tab.Profit && !hasProfit && hasCollection) SetTab(Tab.Collection);
        if (_tab == Tab.Collection && !hasCollection && hasProfit) SetTab(Tab.Profit);

        TabProfit.ToggleClass("active", _tab == Tab.Profit);
        TabCollect.ToggleClass("active", _tab == Tab.Collection);

        if (!hasProfit && !hasCollection) {
            ShowMessage(Strings.T(entries.Count == 0 ? "Empty.Shop" : missing == null ? "Error.Unlocks" : "Empty.All"));
            return;
        }

        if (_tab == Tab.Profit) {
            UpdateProfit(state, marketable);
        } else {
            UpdateCollection(state, missing!);
        }
    }

    // ------------------------------------------------------------ Monnaies

    private void UpdateCurrencyBar(IReadOnlyList<CurrencyState> states)
    {
        string signature = string.Join(",", states.Select(s => $"{s.ItemId}:{s.Count}")) + $"|{_selected}|{_managing}";
        if (signature == _currencySignature) return;
        _currencySignature = signature;

        CurrencyBar.Clear();

        foreach (var s in states) {
            uint id = s.ItemId;
            var chip = new Node {
                ClassList  = ["cp-chip"],
                Tooltip    = s.Cap > 0 ? $"{s.Name} : {s.Count:N0} / {s.Cap:N0}" : $"{s.Name} : {s.Count:N0}",
                ChildNodes = [
                    new() { ClassList = ["cp-chip-icon"], Style = new() { IconId = s.IconId } },
                    new() {
                        ClassList = ["cp-chip-text"],
                        NodeValue = $"{s.Count:N0}",
                        Style     = new() { Color = _widget.IsAlert(s) ? _widget.AlertColor : null },
                    },
                ],
            };

            chip.ToggleClass("selected", !_managing && id == _selected);
            chip.OnClick += _ => Select(id);
            CurrencyBar.AppendChild(chip);
        }

        var manage = new Node {
            ClassList = ["cp-chip", "manage"],
            NodeValue = _managing ? Strings.T("Manage.Done") : Strings.T("Manage"),
            Tooltip   = Strings.T("Manage.Tooltip"),
        };
        manage.ToggleClass("selected", _managing);
        manage.OnClick += _ => {
            _managing = !_managing;
            Invalidate();
        };
        CurrencyBar.AppendChild(manage);
    }

    // -------------------------------------------------- Sélecteur de monnaies

    private void UpdateManager()
    {
        var tracked = _widget.TrackedIds();

        string signature = $"M|{string.Join(",", tracked)}";
        if (signature == _listSignature) return;
        _listSignature = signature;

        Tabs.Style.IsVisible   = false;
        Header.Style.IsVisible = false;
        Status.Style.IsVisible = false;
        List.Style.IsVisible   = true;
        Header.Clear();
        List.Clear();

        List<CatalogEntry> catalog;
        try {
            catalog = Currencies.Catalog();
        } catch (Exception e) {
            Logger.Error($"[CurrencyPlus] Liste des monnaies illisible : {e.Message}");
            ShowMessage(Strings.T("Error.Catalog"));
            return;
        }

        foreach (var group in catalog.GroupBy(c => c.Group)) {
            List.AppendChild(new() { ClassList = ["cp-group"], NodeValue = Strings.T($"Group.{group.Key}") });

            var grid = new Node { ClassList = ["cp-grid"] };
            foreach (var c in group) {
                uint id   = c.ItemId;
                var  tile = new Node {
                    ClassList  = ["cp-tile"],
                    Tooltip    = c.Name,
                    ChildNodes = [
                        new() { ClassList = ["cp-tile-icon"], Style = new() { IconId = c.IconId } },
                        new() { ClassList = ["cp-tile-text"], NodeValue = c.Name },
                    ],
                };

                tile.ToggleClass("selected", tracked.Contains(id));
                tile.OnClick += _ => {
                    _widget.ToggleTracked(id);
                    _listSignature = string.Empty;
                };
                grid.AppendChild(tile);
            }

            List.AppendChild(grid);
        }

        Footer.NodeValue = Strings.T("Manage.Footer");
    }

    // ------------------------------------------------------------ Rentable

    private string MarketName()
    {
        var player = Framework.Service<IPlayer>();
        string world = player.HomeWorldName;
        if (string.IsNullOrEmpty(world)) return string.Empty;

        if (_widget.GetConfigValue<string>("Market") != "DataCenter") return world;

        foreach (var w in Framework.Service<IDataManager>().GetExcelSheet<World>()) {
            if (w.Name.ExtractText() == world) {
                string dc = w.DataCenter.ValueNullable?.Name.ExtractText() ?? string.Empty;
                return string.IsNullOrEmpty(dc) ? world : dc;
            }
        }

        return world;
    }

    private void UpdateProfit(CurrencyState state, List<ShopEntry> marketable)
    {
        string market = MarketName();
        if (string.IsNullOrEmpty(market)) {
            ShowMessage(Strings.T("Error.NoMarket"));
            return;
        }

        // Aucun appel réseau tant que la popup est fermée : on n'arrive ici que si elle est ouverte.
        if (DateTime.UtcNow >= _nextRequest) {
            _nextRequest = DateTime.UtcNow.AddSeconds(1);
            var ttl = TimeSpan.FromHours(Math.Max(1, _widget.GetConfigValue<int>("CacheHours")));
            PriceService.Request(_selected, market, marketable.Select(e => e.ItemId).ToList(), ttl, _forceRefresh);
            _forceRefresh = false;
        }

        var snapshot = PriceService.Get(_selected, market);

        List<Column> columns = [Column.Name];
        foreach (string c in CurrencyPlusWidget.ProfitColumns) {
            if (_widget.IsColumnVisible(c)) columns.Add(Enum.Parse<Column>(c));
        }

        // Tri sur une colonne masquée : on revient au nom.
        if (!columns.Contains(_sortColumn) && _sortColumn != Column.Score) {
            _sortColumn = Column.Name;
            _sortDesc   = false;
        }

        string signature = $"P|{_selected}|{market}|{snapshot.Version}|{_sortColumn}|{_sortDesc}|{string.Join(",", columns)}|{(int)(DateTime.UtcNow - snapshot.FetchedAt).TotalMinutes}";
        if (signature == _listSignature) return;
        _listSignature = signature;

        switch (snapshot.Status) {
            case PriceStatus.Loading when snapshot.Stats.Count == 0:
            case PriceStatus.Idle:
                ShowMessage(Strings.T("Loading.Prices"));
                return;
            case PriceStatus.Error:
                ShowMessage(Strings.T("Error.Prices", snapshot.Error ?? "?"));
                return;
        }

        List<Row> rows = marketable.Select(e => {
            snapshot.Stats.TryGetValue(e.ItemId, out var st);
            double price = st?.MedianPrice ?? 0;
            double sales = st?.Sales7Days ?? 0;
            double gpp   = e.CostPerUnit > 0 ? price / e.CostPerUnit : 0;
            return new Row(e, price, sales, gpp, gpp * sales, state.Count >= e.Cost);
        }).ToList();

        RenderTable(columns, Sort(rows), (row, col) => col switch {
            Column.Cost        => Fmt(row.Entry.Cost),
            Column.Price       => row.Price > 0 ? Fmt(row.Price) : "—",
            Column.Sales       => row.Sales > 0 ? row.Sales.ToString("0.#", CultureInfo.CurrentCulture) : "0",
            Column.GilPerPoint => row.GilPerPoint > 0 ? row.GilPerPoint.ToString(row.GilPerPoint >= 100 ? "N0" : "N1", CultureInfo.CurrentCulture) : "—",
            Column.Score       => row.Score > 0 ? Fmt(row.Score) : "—",
            _                  => string.Empty,
        });

        int minutes = (int)Math.Max(0, (DateTime.UtcNow - snapshot.FetchedAt).TotalMinutes);
        SetFooter(snapshot.Status == PriceStatus.Loading ? Strings.T("Loading.Prices") : Strings.T("Footer.Prices", market, minutes), rows.Count);
    }

    // ---------------------------------------------------------- Collection

    /// <summary>Objets débloquables pas encore débloqués ni possédés. Null si l'état des déblocages est indisponible.</summary>
    private List<ShopEntry>? MissingCollectibles(IReadOnlyList<ShopEntry> entries)
    {
        long   bucket = DateTime.UtcNow.Ticks / TimeSpan.FromSeconds(5).Ticks;
        string key    = $"{_selected}|{bucket}";
        if (key == _collectionKey) return _missing;

        var unlocks = Unlocks.State;
        if (unlocks == null) return null;

        var items = Framework.Service<IDataManager>().GetExcelSheet<Item>();

        List<ShopEntry> missing = [];
        foreach (var e in entries) {
            var item = items.GetRowOrDefault(e.ItemId);
            if (item != null && Unlocks.IsMissingCollectible(unlocks, item.Value)) missing.Add(e);
        }

        _collectionKey = key;
        _missing       = missing;
        return missing;
    }

    private void UpdateCollection(CurrencyState state, List<ShopEntry> missing)
    {
        string signature = $"C|{_selected}|{state.Count}|{_sortColumn}|{_sortDesc}|{_collectionKey}";
        if (signature == _listSignature) return;
        _listSignature = signature;

        List<Row> rows = missing.Select(e => new Row(e, 0, 0, 0, 0, state.Count >= e.Cost)).ToList();

        Column[] columns = [Column.Name, Column.Type, Column.Cost, Column.Enough];
        RenderTable(columns, Sort(rows), (row, col) => col switch {
            Column.Type   => row.Entry.Category,
            Column.Cost   => Fmt(row.Entry.Cost),
            Column.Enough => row.Enough ? Strings.T("Yes") : Strings.T("No"),
            _             => string.Empty,
        });

        SetFooter(null, rows.Count);
    }

    // ----------------------------------------------------------------- Rendu

    private List<Row> Sort(List<Row> rows)
    {
        Func<Row, object> key = _sortColumn switch {
            Column.Cost        => r => r.Entry.CostPerUnit,
            Column.Price       => r => r.Price,
            Column.Sales       => r => r.Sales,
            Column.GilPerPoint => r => r.GilPerPoint,
            Column.Score       => r => r.Score,
            Column.Type        => r => r.Entry.Category,
            Column.Enough      => r => r.Enough,
            _                  => r => r.Entry.Name,
        };

        var comparer = Comparer<object>.Create((a, b) => a is string sa && b is string sb
            ? StringComparer.CurrentCultureIgnoreCase.Compare(sa, sb)
            : Comparer<object>.Default.Compare(a, b));

        var ordered = _sortDesc ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer);
        return ordered.ThenBy(r => r.Entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private void RenderTable(IReadOnlyList<Column> columns, List<Row> rows, Func<Row, Column, string> cell)
    {
        Status.Style.IsVisible = false;
        Header.Style.IsVisible = true;
        List.Style.IsVisible   = true;

        // En-têtes cliquables pour trier.
        Header.Clear();
        Header.AppendChild(new() { ClassList = ["cp-cell", "icon"] });
        foreach (var col in columns) {
            string label = ColumnLabel(col);
            if (col == _sortColumn) label += _sortDesc ? " ▼" : " ▲";

            var head = new Node { ClassList = ["cp-cell", "head", ColumnClass(col)], NodeValue = label };
            head.OnClick += _ => SetSort(col);
            Header.AppendChild(head);
        }

        List.Clear();

        bool teleport = _widget.GetConfigValue<bool>("Teleport");
        int  max      = _widget.GetConfigValue<int>("MaxRows");

        foreach (var row in rows.Take(max)) {
            var entry = row.Entry;
            var node  = new Node {
                ClassList  = ["cp-row"],
                Tooltip    = teleport ? Strings.T("Row.Tooltip.Teleport") : Strings.T("Row.Tooltip.Flag"),
                ChildNodes = [
                    new() { ClassList = ["cp-cell", "icon"], Style = new() { IconId = entry.IconId } },
                ],
            };

            foreach (var col in columns) {
                string text = col == Column.Name
                    ? (entry.Quantity > 1 ? $"{entry.Name} ×{entry.Quantity}" : entry.Name)
                    : cell(row, col);

                var c = new Node { ClassList = ["cp-cell", ColumnClass(col)], NodeValue = text };
                if (col == Column.Enough) c.Style.Color = row.Enough ? YesColor : NoColor;
                node.AppendChild(c);
            }

            node.OnClick += _ => OnItemClicked(entry, teleport);
            List.AppendChild(node);
        }
    }

    private void ShowMessage(string message)
    {
        Header.Clear();
        List.Clear();
        Header.Style.IsVisible = false;
        List.Style.IsVisible   = false;

        Status.Style.IsVisible = true;
        Status.NodeValue       = message;
        Footer.NodeValue       = string.Empty;
    }

    private void SetFooter(string? info, int total)
    {
        int max = _widget.GetConfigValue<int>("MaxRows");

        List<string> parts = [];
        if (!string.IsNullOrEmpty(info)) parts.Add(info);
        if (total > max) parts.Add(Strings.T("Footer.Truncated", max, total));
        parts.Add(_widget.GetConfigValue<bool>("Teleport") ? Strings.T("Footer.Click.Teleport") : Strings.T("Footer.Click.Flag"));

        Footer.NodeValue = string.Join(" · ", parts);
    }

    private void OnItemClicked(ShopEntry entry, bool teleport)
    {
        try {
            Travel.GoTo(entry, teleport);
            if (teleport) Close();
        } catch (Exception e) {
            Logger.Error($"[CurrencyPlus] Clic sur l'objet {entry.ItemId} : {e}");
        }
    }

    private static string Fmt(double value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static string ColumnLabel(Column col) => col switch {
        Column.Name        => Strings.T("Col.Item"),
        Column.Cost        => Strings.T("Col.Cost"),
        Column.Price       => Strings.T("Col.Price"),
        Column.Sales       => Strings.T("Col.Sales"),
        Column.GilPerPoint => Strings.T("Col.GilPerPoint"),
        Column.Score       => Strings.T("Col.Score"),
        Column.Type        => Strings.T("Col.Type"),
        Column.Enough      => Strings.T("Col.Enough"),
        _                  => string.Empty,
    };

    private static string ColumnClass(Column col) => col switch {
        Column.Name   => "name",
        Column.Type   => "type",
        Column.Enough => "enough",
        _             => "num",
    };

    // ------------------------------------------------------------ Stylesheet

    private static Style CellText(int width, Anchor align) => new() {
        Size         = new(width, 22),
        FontSize     = 13,
        TextAlign    = align,
        Anchor       = Anchor.MiddleLeft,
        Color        = new("Widget.PopupMenuText"),
        OutlineColor = new("Widget.PopupMenuTextOutline"),
        OutlineSize  = 1,
        WordWrap     = false,
        TextOverflow = false,
    };

    private static Style Muted(int fontSize, int width) => new() {
        Size         = new(width, 0),
        FontSize     = fontSize,
        WordWrap     = true,
        Color        = new("Widget.PopupMenuTextMuted"),
        OutlineColor = new("Widget.PopupMenuTextOutline"),
        OutlineSize  = 1,
    };

    private static Stylesheet PopupStylesheet { get; } = new([
        new("#cp-popup", new() { Flow = Flow.Vertical, Padding = new(8), Gap = 6 }),

        new(".cp-currencies", new() { Flow = Flow.Horizontal, Gap = 4, Size = new(ListWidth, 0) }),
        new(".cp-chip", new() {
            Flow         = Flow.Horizontal,
            Gap          = 4,
            Padding      = new(3, 6),
            BorderRadius = 4,
            StrokeColor  = new("Widget.Border"),
            StrokeWidth  = 1,
        }),
        new(".cp-chip:hover", new() { BackgroundColor = new("Widget.PopupMenuBackgroundHover") }),
        new(".cp-chip.selected", new() { BackgroundColor = new("Widget.PopupMenuBackgroundHover"), StrokeColor = new("Widget.PopupMenuText") }),
        new(".cp-chip.manage", new() {
            FontSize     = 12,
            TextAlign    = Anchor.MiddleCenter,
            Size         = new(0, 26),
            Padding      = new(0, 10),
            Color        = new("Widget.PopupMenuText"),
            OutlineColor = new("Widget.PopupMenuTextOutline"),
            OutlineSize  = 1,
        }),
        new(".cp-chip-icon", new() { Size = new(20, 20), Anchor = Anchor.MiddleLeft }),
        new(".cp-chip-text", CellText(0, Anchor.MiddleLeft)),

        new(".cp-tabs", new() {
            Flow        = Flow.Horizontal,
            Gap         = 4,
            Padding     = new() { Top = 2, Bottom = 4 },
            BorderColor = new() { Bottom = new("Widget.Border") },
            BorderWidth = new() { Bottom = 1 },
            Size        = new(ListWidth, 0),
        }),
        new(".cp-tab", new() {
            Padding      = new(3, 10),
            BorderRadius = 4,
            FontSize     = 13,
            Color        = new("Widget.PopupMenuTextMuted"),
            OutlineColor = new("Widget.PopupMenuTextOutline"),
            OutlineSize  = 1,
        }),
        new(".cp-tab:hover", new() { Color = new("Widget.PopupMenuTextHover"), BackgroundColor = new("Widget.PopupMenuBackgroundHover") }),
        new(".cp-tab.active", new() { Color = new("Widget.PopupMenuText"), BackgroundColor = new("Widget.PopupMenuBackgroundHover") }),
        new(".cp-refresh", new() {
            Anchor       = Anchor.TopRight,
            Padding      = new(3, 10),
            BorderRadius = 4,
            FontSize     = 12,
            StrokeColor  = new("Widget.Border"),
            StrokeWidth  = 1,
            Color        = new("Widget.PopupMenuText"),
            OutlineColor = new("Widget.PopupMenuTextOutline"),
            OutlineSize  = 1,
        }),
        new(".cp-refresh:hover", new() { BackgroundColor = new("Widget.PopupMenuBackgroundHover") }),

        new(".cp-header", new() {
            Flow        = Flow.Horizontal,
            Gap         = 6,
            Padding     = new() { Bottom = 3 },
            BorderColor = new() { Bottom = new("Widget.Border") },
            BorderWidth = new() { Bottom = 1 },
        }),
        new(".cp-list", new() {
            Flow                = Flow.Vertical,
            Size                = new(ListWidth, ListHeight),
            ScrollbarTrackColor = new(0x30000000),
            ScrollbarThumbColor = new("Widget.Border"),
        }),
        new(".cp-row", new() { Flow = Flow.Horizontal, Gap = 6, Padding = new(1, 0), BorderRadius = 3 }),
        new(".cp-row:hover", new() { BackgroundColor = new("Widget.PopupMenuBackgroundHover") }),

        new(".cp-cell.icon", new() { Size = new(22, 22), Anchor = Anchor.MiddleLeft }),
        new(".cp-cell.name", CellText(240, Anchor.MiddleLeft)),
        new(".cp-cell.type", CellText(150, Anchor.MiddleLeft)),
        new(".cp-cell.num", CellText(68, Anchor.MiddleRight)),
        new(".cp-cell.enough", CellText(60, Anchor.MiddleCenter)),
        new(".cp-cell.head", new() { Color = new("Widget.PopupMenuTextMuted") }),
        new(".cp-cell.head:hover", new() { Color = new("Widget.PopupMenuTextHover") }),

        // Sélecteur de monnaies : une grille de tuiles par groupe, comme la fenêtre « Devises ».
        new(".cp-group", new() {
            Size         = new(ListWidth - 16, 0),
            Padding      = new() { Top = 6, Bottom = 2 },
            FontSize     = 12,
            Color        = new("Widget.PopupMenuTextMuted"),
            OutlineColor = new("Widget.PopupMenuTextOutline"),
            OutlineSize  = 1,
            BorderColor  = new() { Bottom = new("Widget.Border") },
            BorderWidth  = new() { Bottom = 1 },
        }),
        new(".cp-grid", new() { Flow = Flow.Vertical, Gap = 2 }),
        new(".cp-tile", new() {
            Flow         = Flow.Horizontal,
            Gap          = 6,
            Padding      = new(2, 6),
            BorderRadius = 4,
            Size         = new(ListWidth - 16, 0),
        }),
        new(".cp-tile:hover", new() { BackgroundColor = new("Widget.PopupMenuBackgroundHover") }),
        new(".cp-tile.selected", new() { BackgroundColor = new(0x4066DD66), StrokeColor = new(0xFF66DD66), StrokeWidth = 1 }),
        new(".cp-tile-icon", new() { Size = new(24, 24), Anchor = Anchor.MiddleLeft }),
        new(".cp-tile-text", CellText(0, Anchor.MiddleLeft)),

        new(".cp-status", new() {
            Size         = new(ListWidth, 60),
            FontSize     = 13,
            TextAlign    = Anchor.MiddleCenter,
            WordWrap     = true,
            Color        = new("Widget.PopupMenuTextMuted"),
            OutlineColor = new("Widget.PopupMenuTextOutline"),
            OutlineSize  = 1,
        }),
        new(".cp-footer", Muted(11, ListWidth)),
    ]);
}
