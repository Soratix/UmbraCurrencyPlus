using Dalamud.IoC;
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

    /// <summary>Vrai si l'objet se débloque (monture, mascotte…) et que le personnage ne l'a pas encore.</summary>
    public static bool IsMissingCollectible(IUnlockState state, Item item)
        => state.IsItemUnlockable(item) && !state.IsItemUnlocked(item);
}
