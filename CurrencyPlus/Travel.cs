using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Umbra.CurrencyPlus;

/// <summary>
/// Clic sur un objet : drapeau sur la carte à l'emplacement du vendeur, puis téléportation
/// à l'éthérite débloquée la plus proche (même logique que le widget Drapeau d'Umbra :
/// marqueurs d'éthérite de la carte via IZoneManager, éthérites débloquées via IAetheryteList).
/// </summary>
public static unsafe class Travel
{
    private const uint FlagIconId = 60561; // icône du drapeau posé par le joueur

    private sealed record Plan(NpcLocation Vendor, IAetheryteEntry? Aetheryte);

    public static void GoTo(ShopEntry entry, bool teleport)
    {
        var chat = Framework.Service<IChatGui>();

        var vendors = ShopData.VendorsOf(entry);
        if (vendors.Count == 0) {
            chat.Print(new SeStringBuilder()
                .Append(SeString.CreateItemLink(entry.ItemId, false))
                .AddText($" — {Strings.T("Travel.NoVendor")}")
                .Build(), "Currencies+");
            return;
        }

        var plan = ChoosePlan(vendors);
        var v    = plan.Vendor;

        var agent = AgentMap.Instance();
        if (agent != null) agent->SetFlagMapMarker(v.TerritoryId, v.MapId, v.Position, FlagIconId);

        var    data    = Framework.Service<IDataManager>();
        string npcName = data.GetExcelSheet<ENpcResident>().GetRowOrDefault(v.NpcId)?.Singular.ExtractText() ?? "?";

        var message = new SeStringBuilder()
            .Append(SeString.CreateItemLink(entry.ItemId, false))
            .AddText($" — {npcName}");

        // Le lien de carte attend des coordonnées « lisibles » (11,5 ; 8,2), pas des coordonnées du monde.
        if (data.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRowOrDefault(v.MapId) is { } map) {
            Vector2 coords = MapUtil.WorldToMap(new Vector2(v.Position.X, v.Position.Z), map);
            message.AddText(" : ").Append(SeString.CreateMapLink(v.TerritoryId, v.MapId, coords.X, coords.Y, 0f));
        }
        chat.Print(message.Build(), "Currencies+");

        if (!teleport) return;

        var player = Framework.Service<IPlayer>();
        uint here  = Framework.Service<IClientState>().TerritoryType;

        if (here == v.TerritoryId) return; // déjà dans la zone : le drapeau suffit

        if (plan.Aetheryte == null) {
            chat.PrintError(Strings.T("Travel.NoAetheryte"), "Currencies+");
            return;
        }

        if (!player.CanUseTeleportAction) {
            chat.PrintError(Strings.T("Travel.CannotTeleport"), "Currencies+");
            return;
        }

        Telepo.Instance()->Teleport(plan.Aetheryte.AetheryteId, plan.Aetheryte.SubIndex);
    }

    /// <summary>
    /// Vendeur dans la zone actuelle s'il y en a un ; sinon celui dont l'éthérite débloquée
    /// coûte le moins cher ; sinon le premier connu (drapeau seul).
    /// </summary>
    private static Plan ChoosePlan(List<NpcLocation> vendors)
    {
        uint here = Framework.Service<IClientState>().TerritoryType;

        var local = vendors.FirstOrDefault(v => v.TerritoryId == here);
        if (local != null) return new(local, FindAetheryte(local));

        Plan? best = null;
        foreach (var v in vendors) {
            var a = FindAetheryte(v);
            if (a == null) continue;
            if (best?.Aetheryte == null || a.GilCost < best.Aetheryte.GilCost) best = new(v, a);
        }

        return best ?? new(vendors[0], null);
    }

    /// <summary>
    /// 1. l'éthérite de la carte la plus proche du PNJ ;
    /// 2. sinon une éthérite de la même zone ;
    /// 3. sinon, pour un quartier de ville sans éthérite, la grande éthérite du même réseau (aethernet).
    /// Seules les éthérites débloquées par le personnage sont retenues.
    /// </summary>
    private static IAetheryteEntry? FindAetheryte(NpcLocation v)
    {
        var unlocked = Framework.Service<IAetheryteList>().Where(a => a.Ward == 0 && a.Plot == 0 && !a.IsSharedHouse && !a.IsApartment).ToList();
        IAetheryteEntry? Unlocked(uint id) => unlocked.FirstOrDefault(a => a.AetheryteId == id);

        try {
            var zone = Framework.Service<IZoneManager>().GetZone(v.MapId);
            var pos  = new Vector2(v.Position.X, v.Position.Z);

            var nearest = zone.StaticMarkers
                .Where(m => m.Type == ZoneMarkerType.Aetheryte)
                .OrderBy(m => Vector2.Distance(new(m.WorldPosition.X, m.WorldPosition.Z), pos))
                .Select(m => Unlocked(m.DataId))
                .FirstOrDefault(a => a != null);

            if (nearest != null) return nearest;
        } catch (Exception e) {
            Logger.Warning($"[CurrencyPlus] Carte {v.MapId} illisible : {e.Message}");
        }

        var sheet = Framework.Service<IDataManager>().GetExcelSheet<Aetheryte>();

        foreach (var row in sheet) {
            if (row.IsAetheryte && row.Territory.RowId == v.TerritoryId && Unlocked(row.RowId) is { } a) return a;
        }

        foreach (var shard in sheet) {
            if (shard.IsAetheryte || shard.AethernetGroup == 0 || shard.Territory.RowId != v.TerritoryId) continue;

            foreach (var main in sheet) {
                if (main.IsAetheryte && main.AethernetGroup == shard.AethernetGroup && Unlocked(main.RowId) is { } a) return a;
            }
        }

        // Azurée (886) n'a pas d'éthérite : on y va par le réseau d'Ishgard, depuis L'Assise (éthérite 70).
        if (v.TerritoryId == FirmamentTerritory) return Unlocked(FoundationAetheryte);

        return null;
    }

    private const uint FirmamentTerritory  = 886;
    private const uint FoundationAetheryte = 70;
}
