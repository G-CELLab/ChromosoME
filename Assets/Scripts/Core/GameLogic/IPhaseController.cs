/// <summary>
/// Implement this interface on any MonoBehaviour that needs to react to phase transitions.
/// GameManager calls OnPhaseEnter when entering a phase and OnPhaseExit when leaving it.
/// Scripts should NOT poll GameManager.eGameStatus in Update — react here instead.
/// </summary>
public interface IPhaseController
{
    void OnPhaseEnter(GameManager.GameState phase);
    void OnPhaseExit(GameManager.GameState phase);
}