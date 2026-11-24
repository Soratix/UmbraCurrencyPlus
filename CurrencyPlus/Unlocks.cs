using Dalamud.IoC;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace Umbra.CurrencyPlus;

/// <summary>
/// Accès à IUnlockState de Dalamud (montures, mascottes, orchestrions, cartes, emotes, coiffures…).
/// Umbra n'enregistre pas ce service dans son conteneur (Framework.Service), mais il expose
/// l'interface Dalamud du plugin (Framework.DalamudPlugin), dont Create&lt;T&gt; injecte les
/// propriétés [PluginService].
/// </summary>
public static class Unlocks
{
    private sealed class Services
    {
        [PluginService] public IUnlockState UnlockState { get; private set; } = null!;
    }

    private static IUnlockState? _state;
    private static bool          _failed;

    public static IUnlockState? State
    {
        get {
            if (_state != null || _failed) return _state;

            try {
                _state = Framework.DalamudPlugin.Create<Services>()?.UnlockState;
            } catch (Exception e) {
                Logger.Error($"[CurrencyPlus] IUnlockState indisponible : {e.Message}");
            }

            _failed = _state == null;
            return _state;
        }
    }

    /// <summary>
    /// Vrai si l'objet se débloque (monture, mascotte…), que le personnage ne l'a pas encore
    /// débloqué, et qu'il ne l'a pas déjà acheté sans l'utiliser (inventaire, sacoches de chocobo).
    /// </summary>
    public static bool IsMissingCollectible(IUnlockState state, Item item)
        => state.IsItemUnlockable(item) && !state.IsItemUnlocked(item) && !IsOwned(item.RowId);

    private static readonly InventoryType[] OwnedContainers = [
        InventoryType.SaddleBag1, InventoryType.SaddleBag2,
        InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2,
    ];

    /// <summary>L'objet est-il dans l'inventaire ou les sacoches ? (Les servants ne sont lus que s'ils ont été ouverts.)</summary>
    public static unsafe bool IsOwned(uint itemId)
    {
        var inv = InventoryManager.Instance();
        if (inv == null) return false;

        if (inv->GetInventoryItemCount(itemId, false, false, false, 0) > 0) return true;
        if (inv->GetInventoryItemCount(itemId, true, false, false, 0) > 0) return true;

        foreach (var container in OwnedContainers) {
            if (inv->GetItemCountInContainer(itemId, container, false, 0) > 0) return true;
        }

        return false;
    }
}
