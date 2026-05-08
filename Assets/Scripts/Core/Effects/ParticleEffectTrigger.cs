using UnityEngine;

/// <summary>
/// Activates a particle effect after a delay when a specified phase begins.
/// </summary>
public class ParticleEffectTrigger : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public ParticleSystem particleEffect;

    [Header("Settings")]
    public GameManager.GameState targetPhase = GameManager.GameState.Metaphase;
    public float delay = 8f;

    private float timer   = 0f;
    private bool isDone   = false;
    private bool isActive = false;

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == targetPhase)
        {
            isActive = true;
            isDone   = false;
            timer    = 0f;
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == targetPhase)
            isActive = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || isDone) return;

        timer += Time.deltaTime;
        if (timer >= delay)
        {
            isDone = true;
            if (particleEffect != null) particleEffect.Play();
            Debug.Log($"[ParticleEffectTrigger] Effect activated at {targetPhase}.");
        }
    }
}