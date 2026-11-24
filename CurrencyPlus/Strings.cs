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
        ["Cfg.Currencies"]          = ("Monnaies suivies", "Tracked currencies"),
        ["Cfg.Track.Desc"]          = ("Afficher cette monnaie dans la barre et dans la popup.", "Show this currency in the toolbar and in the popup."),
        ["Cfg.GCSeals"]             = ("Sceaux de votre Grande Compagnie", "Your Grand Company seals"),
        ["Cfg.NonLimitedTome"]      = ("Mémoquartz non limité de la saison", "Current uncapped tomestone"),
        ["Cfg.LimitedTome"]         = ("Mémoquartz limité de la saison", "Current capped tomestone"),
        ["Cfg.CustomIds"]           = ("Autres monnaies (ID d'objet)", "Other currencies (item IDs)"),
        ["Cfg.CustomIds.Desc"]      = ("ID d'objets séparés par des virgules, par exemple une monnaie de tribu alliée.", "Comma-separated item IDs, for example an allied society currency."),
        ["Cfg.Decorate"]            = ("Fond et bordure", "Background and border"),
        ["Cfg.Decorate.Desc"]       = ("Dessine le fond et la bordure du widget.", "Draws the widget background and border."),
        ["Cfg.ShowCap"]             = ("Afficher le plafond", "Show the cap"),
        ["Cfg.ShowCap.Desc"]        = ("Affiche « quantité/plafond » au lieu de la quantité seule.", "Shows “amount/cap” instead of the amount alone."),
        ["Cfg.Threshold"]           = ("Seuil d'alerte (% du plafond)", "Alert threshold (% of cap)"),
        ["Cfg.Threshold.Desc"]      = ("Au-delà, la quantité prend la couleur d'alerte.", "Above this, the amount uses the alert color."),
        ["Cfg.AlertColor"]          = ("Couleur d'alerte", "Alert color"),
        ["Cfg.Market"]              = ("Marché pour les prix", "Market for prices"),
        ["Cfg.Market.Desc"]         = ("Prix et ventes lus sur Universalis pour votre monde d'origine ou tout son centre de données.", "Prices and sales read from Universalis for your home world or its whole data center."),
        ["Cfg.Market.World"]        = ("Monde d'origine", "Home world"),
        ["Cfg.Market.DC"]           = ("Centre de données", "Data center"),
        ["Cfg.CacheHours"]          = ("Durée du cache des prix (heures)", "Price cache duration (hours)"),
        ["Cfg.CacheHours.Desc"]     = ("Les prix d'une monnaie ne sont redemandés à Universalis qu'après ce délai, ou avec le bouton « Rafraîchir ».", "Prices for a currency are only requested again from Universalis after this delay, or with the “Refresh” button."),
        ["Cfg.MaxRows"]             = ("Lignes affichées au maximum", "Maximum rows shown"),

        // Barre et infobulle
        ["NoCurrency"]          = ("Aucune monnaie", "No currency"),
        ["TooltipHint"]         = ("Clic : que faire de cette monnaie ?", "Click: what to spend this currency on?"),

        // Popup
        ["Tab.Profit"]          = ("Rentable", "Profit"),
        ["Tab.Collection"]      = ("Collection", "Collection"),
        ["Refresh"]             = ("Rafraîchir", "Refresh"),
        ["Col.Item"]            = ("Objet", "Item"),
        ["Col.Cost"]            = ("Coût", "Cost"),
        ["Col.Price"]           = ("Prix de vente", "Sale price"),
        ["Col.Sales"]           = ("Ventes/7 j", "Sales/7d"),
        ["Col.GilPerPoint"]     = ("Gils/point", "Gil/point"),
        ["Col.Type"]            = ("Type", "Type"),
        ["Col.Enough"]          = ("Assez ?", "Enough?"),
        ["Yes"]                 = ("Oui", "Yes"),
        ["No"]                  = ("Non", "No"),
        ["Loading.Shops"]       = ("Préparation de la liste des boutiques…", "Preparing the shop list…"),
        ["Loading.Prices"]      = ("Chargement des prix Universalis…", "Loading Universalis prices…"),
        ["Error.Shops"]         = ("Impossible de lire les boutiques : {0}", "Could not read the shops: {0}"),
        ["Error.Prices"]        = ("Universalis ne répond pas ({0}). Réessayez avec « Rafraîchir ».", "Universalis did not answer ({0}). Try again with “Refresh”."),
        ["Error.Unlocks"]       = ("L'état des déblocages n'est pas disponible.", "Unlock state is not available."),
        ["Error.NoMarket"]      = ("Monde inconnu : connectez-vous à un personnage.", "Unknown world: log in to a character."),
        ["Empty.Profit"]        = ("Aucun objet revendable avec cette monnaie.", "No resellable item for this currency."),
        ["Empty.Collection"]    = ("Rien à débloquer avec cette monnaie : collection complète !", "Nothing left to unlock with this currency: collection complete!"),
        ["Empty.Shop"]          = ("Aucune boutique connue pour cette monnaie.", "No known shop for this currency."),
        ["Footer.Prices"]       = ("{0} · médiane des ventes sur 7 jours · mis à jour il y a {1} min", "{0} · median of 7-day sales · updated {1} min ago"),
        ["Footer.Truncated"]    = ("{0} lignes sur {1}", "{0} of {1} rows"),
        ["Footer.Click"]        = ("Clic sur un objet : lien dans le chat et nom copié", "Click an item: link in chat and name copied"),
        ["Chat.Copied"]         = ("Nom copié : {0}", "Name copied: {0}"),
    };

    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var entry)) return key;
        return IsFrench ? entry.Fr : entry.En;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);
}
