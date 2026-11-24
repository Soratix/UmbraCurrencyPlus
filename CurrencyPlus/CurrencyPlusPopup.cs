using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel.Sheets;

namespace Umbra.CurrencyPlus;

/// <summary>
/// Popup du widget : une rangée de monnaies, puis deux onglets.
///  - Rentable : objets revendables, prix Universalis (médiane 7 j), triés par score = gils/point × ventes/7 j.
///  - Collection : objets à débloquer que le personnage n'a pas encore (IUnlockState, aucun appel réseau).
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
    private Node TabProfit   => Node.QuerySelector(".cp-tab.profit")!;
    private Node TabCollect  => Node.QuerySelector(".cp-tab.collection")!;
    private Node Header      => Node.QuerySelector(".cp-header")!;
    private Node List        => Node.QuerySelector(".cp-list")!;
    private Node Status      => Node.QuerySelector(".cp-status")!;
    private Node Footer      => Node.QuerySelector(".cp-footer")!;

    private uint   _selected;
    private Tab    _tab        = Tab.Profit;
    private Column _sortColumn = Column.Score;
    private bool   _sortDesc   = true;
    private bool   _forceRefresh;

    private string   _currencySignature = string.Empty;
    private string   _listSignature     = string.Empty;
    private DateTime _nextRequest       = DateTime.MinValue;

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
        Node.QuerySelector(".cp-refresh")!.OnClick += _ => {
            _forceRefresh  = true;
            _listSignature = string.Empty;
            _nextRequest   = DateTime.MinValue;
        };
    }

    /// <summary>Choisit la monnaie affichée (clic sur une monnaie dans la barre).</summary>
    public void Select(uint currencyId)
    {
        _selected      = currencyId;
        _listSignature = string.Empty;
        _nextRequest   = DateTime.MinValue;
    }

    private void SetTab(Tab tab)
    {
        if (_tab == tab) return;

        _tab        = tab;
        _sortColumn = tab == Tab.Profit ? Column.Score : Column.Name;
        _sortDesc   = tab == Tab.Profit;

        _listSignature = string.Empty;
        _nextRequest   = DateTime.MinValue;
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

        _currencySignature = string.Empty;
        _listSignature     = string.Empty;
        _nextRequest       = DateTime.MinValue;

        TabProfit.NodeValue  = Strings.T("Tab.Profit");
        TabCollect.NodeValue = Strings.T("Tab.Collection");
        Node.QuerySelector(".cp-refresh")!.NodeValue = Strings.T("Refresh");
    }

    protected override void OnUpdate()
    {
        var states = _widget.States;

        if (states.Count > 0 && states.All(s => s.ItemId != _selected)) {
            _selected = states[0].ItemId;
        }

        UpdateCurrencyBar(states);

        TabProfit.ToggleClass("active", _tab == Tab.Profit);
        TabCollect.ToggleClass("active", _tab == Tab.Collection);

        var state = states.FirstOrDefault(s => s.ItemId == _selected);
        if (state == null) {
            ShowMessage(Strings.T("NoCurrency"));
            return;
        }

        var entries = ShopData.For(_selected);
        if (entries == null) {
            ShowMessage(ShopData.Error is { } err ? Strings.T("Error.Shops", err) : Strings.T("Loading.Shops"));
            return;
        }

        if (_tab == Tab.Profit) {
            UpdateProfit(state, entries);
        } else {
            UpdateCollection(state, entries);
        }
    }

    // ------------------------------------------------------------ Monnaies

    private void UpdateCurrencyBar(IReadOnlyList<CurrencyState> states)
    {
        string signature = string.Join(",", states.Select(s => $"{s.ItemId}:{s.Count}")) + $"|{_selected}";
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

            chip.ToggleClass("selected", id == _selected);
            chip.OnClick += _ => Select(id);
            CurrencyBar.AppendChild(chip);
        }
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

    private void UpdateProfit(CurrencyState state, IReadOnlyList<ShopEntry> entries)
    {
        var marketable = entries.Where(e => e.IsMarketable).ToList();
        if (marketable.Count == 0) {
            ShowMessage(Strings.T(entries.Count == 0 ? "Empty.Shop" : "Empty.Profit"));
            return;
        }

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

        string signature = $"P|{_selected}|{market}|{snapshot.Version}|{_sortColumn}|{_sortDesc}|{(int)(DateTime.UtcNow - snapshot.FetchedAt).TotalMinutes}";
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

        Column[] columns = [Column.Name, Column.Cost, Column.Price, Column.Sales, Column.GilPerPoint, Column.Score];
        RenderTable(columns, Sort(rows), row => [
            Fmt(row.Entry.Cost),
            row.Price > 0 ? Fmt(row.Price) : "—",
            row.Sales > 0 ? row.Sales.ToString("0.#", CultureInfo.CurrentCulture) : "0",
            row.GilPerPoint > 0 ? row.GilPerPoint.ToString(row.GilPerPoint >= 100 ? "N0" : "N1", CultureInfo.CurrentCulture) : "—",
            row.Score > 0 ? Fmt(row.Score) : "—",
        ]);

        int minutes = (int)Math.Max(0, (DateTime.UtcNow - snapshot.FetchedAt).TotalMinutes);
        SetFooter(snapshot.Status == PriceStatus.Loading ? Strings.T("Loading.Prices") : Strings.T("Footer.Prices", market, minutes), rows.Count);
    }

    // ---------------------------------------------------------- Collection

    private void UpdateCollection(CurrencyState state, IReadOnlyList<ShopEntry> entries)
    {
        // Rafraîchi toutes les 5 s (un objet a pu être débloqué entre-temps) ou si la quantité change.
        long bucket = DateTime.UtcNow.Ticks / TimeSpan.FromSeconds(5).Ticks;
        string signature = $"C|{_selected}|{state.Count}|{_sortColumn}|{_sortDesc}|{bucket}";
        if (signature == _listSignature) return;
        _listSignature = signature;
        _forceRefresh  = false;

        if (entries.Count == 0) {
            ShowMessage(Strings.T("Empty.Shop"));
            return;
        }

        var unlocks = Unlocks.State;
        if (unlocks == null) {
            ShowMessage(Strings.T("Error.Unlocks"));
            return;
        }

        var items = Framework.Service<IDataManager>().GetExcelSheet<Item>();

        List<Row> rows = [];
        foreach (var e in entries) {
            var item = items.GetRowOrDefault(e.ItemId);
            if (item == null || !Unlocks.IsMissingCollectible(unlocks, item.Value)) continue;

            rows.Add(new(e, 0, 0, 0, 0, state.Count >= e.Cost));
        }

        if (rows.Count == 0) {
            ShowMessage(Strings.T("Empty.Collection"));
            return;
        }

        Column[] columns = [Column.Name, Column.Type, Column.Cost, Column.Enough];
        RenderTable(columns, Sort(rows), row => [
            row.Entry.Category,
            Fmt(row.Entry.Cost),
            row.Enough ? Strings.T("Yes") : Strings.T("No"),
        ]);

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

    private void RenderTable(Column[] columns, List<Row> rows, Func<Row, string[]> cells)
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

            var cell = new Node { ClassList = ["cp-cell", "head", ColumnClass(col)], NodeValue = label };
            cell.OnClick += _ => SetSort(col);
            Header.AppendChild(cell);
        }

        List.Clear();

        int max = _widget.GetConfigValue<int>("MaxRows");
        foreach (var row in rows.Take(max)) {
            var entry = row.Entry;
            var node  = new Node {
                ClassList  = ["cp-row"],
                ChildNodes = [
                    new() { ClassList = ["cp-cell", "icon"], Style = new() { IconId = entry.IconId } },
                    new() { ClassList = ["cp-cell", ColumnClass(Column.Name)], NodeValue = entry.Quantity > 1 ? $"{entry.Name} ×{entry.Quantity}" : entry.Name },
                ],
            };

            string[] values = cells(row);
            for (int i = 0; i < values.Length && i + 1 < columns.Length; i++) {
                var col  = columns[i + 1];
                var cell = new Node { ClassList = ["cp-cell", ColumnClass(col)], NodeValue = values[i] };
                if (col == Column.Enough) cell.Style.Color = row.Enough ? YesColor : NoColor;
                node.AppendChild(cell);
            }

            node.OnClick += _ => OnItemClicked(entry);
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
        parts.Add(Strings.T("Footer.Click"));

        Footer.NodeValue = string.Join(" · ", parts);
    }

    /// <summary>
    /// Clic sur un objet : son lien s'affiche dans le chat (localement, cliquable, avec la fiche de l'objet)
    /// et son nom est copié dans le presse-papiers, prêt à coller dans la recherche du tableau des ventes.
    /// Envoyer le lien dans un canal public demanderait d'injecter du texte dans la saisie du chat.
    /// </summary>
    private static void OnItemClicked(ShopEntry entry)
    {
        try {
            ImGui.SetClipboardText(entry.Name);

            var message = new SeStringBuilder()
                .Append(SeString.CreateItemLink(entry.ItemId, false))
                .AddText($" — {Strings.T("Chat.Copied", entry.Name)}")
                .Build();

            Framework.Service<IChatGui>().Print(message, "Currencies+");
        } catch (Exception e) {
            Logger.Error($"[CurrencyPlus] Clic sur l'objet {entry.ItemId} : {e.Message}");
        }
    }

    private static string Fmt(double value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static string ColumnLabel(Column col) => col switch {
        Column.Name        => Strings.T("Col.Item"),
        Column.Cost        => Strings.T("Col.Cost"),
        Column.Price       => Strings.T("Col.Price"),
        Column.Sales       => Strings.T("Col.Sales"),
        Column.GilPerPoint => Strings.T("Col.GilPerPoint"),
        Column.Score       => "Score",
        Column.Type        => Strings.T("Col.Type"),
        Column.Enough      => Strings.T("Col.Enough"),
        _                  => string.Empty,
    };

    private static string ColumnClass(Column col) => col switch {
        Column.Name        => "name",
        Column.Type        => "type",
        Column.Enough      => "enough",
        _                  => "num",
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

    private static Stylesheet PopupStylesheet { get; } = new([
        new("#cp-popup", new() { Flow = Flow.Vertical, Padding = new(8), Gap = 6 }),

        new(".cp-currencies", new() { Flow = Flow.Horizontal, Gap = 4 }),
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
            Flow                 = Flow.Vertical,
            Size                 = new(ListWidth, ListHeight),
            ScrollbarTrackColor  = new(0x30000000),
            ScrollbarThumbColor  = new("Widget.Border"),
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

        new(".cp-status", new() {
            Size         = new(ListWidth, 60),
            FontSize     = 13,
            TextAlign    = Anchor.MiddleCenter,
            WordWrap     = true,
            Color        = new("Widget.PopupMenuTextMuted"),
            OutlineColor = new("Widget.PopupMenuTextOutline"),
            OutlineSize  = 1,
        }),
        new(".cp-footer", new() {
            Size         = new(ListWidth, 0),
            FontSize     = 11,
            WordWrap     = true,
            Color        = new("Widget.PopupMenuTextMuted"),
            OutlineColor = new("Widget.PopupMenuTextOutline"),
            OutlineSize  = 1,
        }),
    ]);
}
