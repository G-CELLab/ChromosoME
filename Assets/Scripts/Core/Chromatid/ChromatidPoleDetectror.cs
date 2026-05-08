using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Detects when the player holds a chromatid in the correct pole zone during Anaphase.
/// Place one on each pole zone — set Side to Left or Right in the inspector.
/// Both sides must succeed to trigger Telophase.
/// </summary>
public class ChromatidPoleDetector : MonoBehaviour, IPhaseController
{
    public enum Side { Left, Right }

    [Header("Configuration")]
    public Side side;

    [Header("References")]
    public GameManager gameManager;
    public ChromatidPoleDetector otherSide;
    public ParticleSystem particleEffect;

    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;

    [Header("On Success")]
    public Sprite checkedSprite;

    [Header("State")]
    public bool success = false;

    private bool isActive = false;

    private string chromatidTag => side == Side.Left ? "Chromatid1" : "Chromatid2";

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Anaphase)
            isActive = true;
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Anaphase)
            isActive = false;
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerStay(Collider other)
    {
        if (!isActive || success) return;
        if (!other.CompareTag(chromatidTag)) return;

        if (loadingCircle.Tick(Time.deltaTime))
            CompleteDetection(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(chromatidTag) && !success)
            loadingCircle.Reset();
    }

    // ── Completion ────────────────────────────────────────────────────────────

    private void CompleteDetection(Collider chromatidCollider)
    {
        success = true;

        loadingCircle.Complete();
        if (checkedSprite != null) loadingCircle.SetSprite(checkedSprite);

        var grab = chromatidCollider.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;

        var col = chromatidCollider.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (particleEffect != null) particleEffect.gameObject.SetActive(false);

        Debug.Log($"[ChromatidPoleDetector] {side} side complete.");

        if (otherSide != null && otherSide.success)
        {
            ScoreManager.HPtracking += 0.3f;
            if (gameManager != null) gameManager.Telophase();
        }
    }
}