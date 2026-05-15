using UnityEngine;
/// <summary>
/// Plays a particle effect when activated by the GameManager.
/// </summary>
public class ParticleEffectTrigger : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public ParticleSystem particleEffect;

    // ── IPhaseController ──────────────────────────────────────────────────────
    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (particleEffect != null) particleEffect.Play();
        Debug.Log("[ParticleEffectTrigger] Effect activated.");
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (particleEffect != null) particleEffect.Stop();
        Debug.Log("[ParticleEffectTrigger] Effect stopped.");
    }
}