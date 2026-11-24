using Dalamud.Game;

namespace Umbra.CurrencyPlus;

/// <summary>
/// Textes du widget en français et en anglais.
/// Umbra ne permet pas aux widgets tiers d'ajouter leurs clés à sa propre table I18N :
/// on suit la langue d'Umbra (I18N.GetCurrentLanguage : réglage d'Umbra, sinon langue de
/// l'interface Dalamud), et à défaut celle du client du jeu. Français si « fr », anglais sinon.
/// </summary>
internal static class Strings
{
    public static bool IsFrench
    {
        get {
            try {
                string lang = I18N.GetCurrentLanguage();
                if (!string.IsNullOrEmpty(lang)) return lang.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
            } catch {
                // I18N indisponible : on se rabat sur la langue du client.
            }

            try {
                return Framework.Service<IClientState>().ClientLanguage == ClientLanguage.French;
            } catch {
                return false;
            }
        }
    }

    private static readonly Dictionary<string, (string Fr, string En)> Table = new() {
        ["WidgetName"]          = ("Monnaies+", "Currencies+"),
        ["WidgetDesc"]          = ("Vos monnaies dans la barre et, au clic, quoi en faire : objets à revendre les plus rentables (Universalis) et objets de collection qui vous manquent.",
                                   "Your currencies in the toolbar and, on click, what to spend them on: the most profitable items to resell (Universalis) and the collectibles you are missing."),

        // Réglages
        ["Cfg.TrackedIds"]          = ("Monnaies suivies (ID)", "Tracked currencies (IDs)"),
        ["Cfg.TrackedIds.Desc"]     = ("Se règle depuis la popup, bouton « Gérer ».", "Set from the popup, “Manage” button."),
        ["Cfg.Threshold"]           = ("Seuil d'alerte (% du plafond)", "Alert threshold (% of cap)"),
        ["Cfg.Threshold.Desc"]      = ("Au-delà, l'icône et la quantité prennent la couleur d'alerte.", "Above this, the icon and the amount use the alert color."),
        ["Cfg.AlertColor"]          = ("Couleur d'alerte", "Alert color"),
        ["Cfg.Teleport"]            = ("Téléporter au clic sur un objet", "Teleport when clicking an item"),
        ["Cfg.Teleport.Desc"]       = ("Pose un drapeau sur le vendeur et téléporte à l'éthérite la plus proche. Décoché : drapeau seulement.", "Places a flag on the vendor and teleports to the nearest aetheryte. Unchecked: flag only."),
        ["Cfg.Market"]              = ("Marché pour les prix", "Market for prices"),
        ["Cfg.Market.Desc"]         = ("Prix et ventes lus sur Universalis pour votre monde d'origine ou tout son centre de données.", "Prices and sales read from Universalis for your home world or its whole data center."),
        ["Cfg.Market.World"]        = ("Monde d'origine", "Home world"),
        ["Cfg.Market.DC"]           = ("Centre de données", "Data center"),
        ["Cfg.CacheHours"]          = ("Durée du cache des prix (heures)", "Price cache duration (hours)"),
        ["Cfg.CacheHours.Desc"]     = ("Les prix d'une monnaie ne sont redemandés à Universalis qu'après ce délai, ou avec le bouton « Rafraîchir ».", "Prices for a currency are only requested again from Universalis after this delay, or with the “Refresh” button."),
        ["Cfg.MaxRows"]             = ("Lignes affichées au maximum", "Maximum rows shown"),
        ["Cfg.Columns"]             = ("Colonnes « Rentable »", "“Profit” columns"),
        ["Cfg.Column.Desc"]         = ("Afficher cette colonne dans l'onglet « Rentable ».", "Show this column in the “Profit” tab."),

        // Barre et infobulle
        ["NoCurrency"]          = ("Aucune monnaie suivie", "No tracked currency"),
        ["NoCurrencyHint"]      = ("Aucune monnaie suivie : cliquez sur « Gérer » pour en choisir.", "No tracked currency: click “Manage” to pick some."),
        ["TooltipHint"]         = ("Clic : vos monnaies et quoi en faire", "Click: your currencies and what to spend them on"),

        // Sélecteur
        ["Manage"]              = ("⚙ Gérer", "⚙ Manage"),
        ["Manage.Done"]         = ("✓ Terminé", "✓ Done"),
        ["Manage.Tooltip"]      = ("Choisir les monnaies suivies", "Pick the tracked currencies"),
        ["Manage.Footer"]       = ("Cliquez sur une monnaie pour la suivre ou ne plus la suivre.", "Click a currency to track or untrack it."),
        ["Group.Tomestones"]        = ("Mémoquartz", "Tomestones"),
        ["Group.GrandCompany"]      = ("Grande Compagnie", "Grand Company"),
        ["Group.Battle"]            = ("Combat", "Battle"),
        ["Group.CraftingGathering"] = ("Artisanat et récolte", "Crafting & gathering"),
        ["Group.Tribes"]            = ("Tribus alliées", "Allied societies"),
        ["Group.Other"]             = ("Autres", "Other"),

        // Popup
        ["Tab.Profit"]          = ("Rentable", "Profit"),
        ["Tab.Collection"]      = ("Collection", "Collection"),
        ["Refresh"]             = ("Rafraîchir", "Refresh"),
        ["Col.Item"]            = ("Objet", "Item"),
        ["Col.Cost"]            = ("Coût", "Cost"),
        ["Col.Price"]           = ("Prix de vente", "Sale price"),
        ["Col.Sales"]           = ("Ventes/7 j", "Sales/7d"),
        ["Col.GilPerPoint"]     = ("Gils/point", "Gil/point"),
        ["Col.Score"]           = ("Score", "Score"),
        ["Col.Type"]            = ("Type", "Type"),
        ["Col.Enough"]          = ("Assez ?", "Enough?"),
        ["Yes"]                 = ("Oui", "Yes"),
        ["No"]                  = ("Non", "No"),
        ["Loading.Shops"]       = ("Préparation de la liste des boutiques…", "Preparing the shop list…"),
        ["Loading.Prices"]      = ("Chargement des prix Universalis…", "Loading Universalis prices…"),
        ["Error.Shops"]         = ("Impossible de lire les boutiques : {0}", "Could not read the shops: {0}"),
        ["Error.Prices"]        = ("Universalis ne répond pas ({0}). Réessayez avec « Rafraîchir ».", "Universalis did not answer ({0}). Try again with “Refresh”."),
        ["Error.Unlocks"]       = ("L'état des déblocages n'est pas disponible.", "Unlock state is not available."),
        ["Error.Catalog"]       = ("Impossible de lire la liste des monnaies.", "Could not read the currency list."),
        ["Error.NoMarket"]      = ("Monde inconnu : connectez-vous à un personnage.", "Unknown world: log in to a character."),
        ["Empty.Shop"]          = ("Aucune boutique connue pour cette monnaie.", "No known shop for this currency."),
        ["Empty.All"]           = ("Rien à acheter avec cette monnaie : aucun objet revendable, et vous avez déjà tous les objets de collection.", "Nothing to buy with this currency: no resellable item, and you already have every collectible."),
        ["Footer.Prices"]       = ("{0} · médiane des ventes sur 7 jours · mis à jour il y a {1} min", "{0} · median of 7-day sales · updated {1} min ago"),
        ["Footer.Truncated"]    = ("{0} lignes sur {1}", "{0} of {1} rows"),
        ["Footer.Click.Teleport"] = ("Clic sur un objet : drapeau sur le vendeur et téléportation", "Click an item: flag on the vendor and teleport"),
        ["Footer.Click.Flag"]     = ("Clic sur un objet : drapeau sur le vendeur", "Click an item: flag on the vendor"),
        ["Row.Tooltip.Teleport"]  = ("Drapeau sur le vendeur + téléportation", "Flag on the vendor + teleport"),
        ["Row.Tooltip.Flag"]      = ("Drapeau sur le vendeur", "Flag on the vendor"),

        // Déplacement
        ["Travel.NoVendor"]       = ("emplacement du vendeur inconnu.", "vendor location unknown."),
        ["Travel.NoAetheryte"]    = ("Aucune éthérite débloquée près de ce vendeur : drapeau posé, pas de téléportation.", "No unlocked aetheryte near this vendor: flag placed, no teleport."),
        ["Travel.CannotTeleport"] = ("Téléportation impossible pour le moment : drapeau posé.", "Cannot teleport right now: flag placed."),
    };

    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var entry)) return key;
        return IsFrench ? entry.Fr : entry.En;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);
}
