using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects when the player holds their hand over the wound for a set duration.
/// Uses a HashSet to handle multiple hand colliders entering and exiting
/// independently without false resets.
/// On completion, calls Interphase on GameManager.
/// </summary>
public class WoundHealingDetector : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;

    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;

    private bool isFinished = false;
    private HashSet<Collider> activeColliders = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Left") && !other.CompareTag("Right")) return;
        if (activeColliders.Add(other))
            Debug.Log($"[WoundHealingDetector] Collider added: {other.name}. Total: {activeColliders.Count}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Left") && !other.CompareTag("Right")) return;
        if (activeColliders.Remove(other))
            Debug.Log($"[WoundHealingDetector] Collider removed: {other.name}. Remaining: {activeColliders.Count}");

        if (activeColliders.Count == 0 && !isFinished)
        {
            loadingCircle.Reset();
            Debug.Log("[WoundHealingDetector] Timer reset — no hand in trigger.");
        }
    }

    private void Update()
    {
        if (isFinished || activeColliders.Count == 0) return;

        if (loadingCircle.Tick(Time.deltaTime))
            FinishInteraction();
    }

    private void FinishInteraction()
    {
        isFinished = true;
        if (gameManager != null) gameManager.Interphase();
        Debug.Log("[WoundHealingDetector] Wound interaction complete — starting Interphase.");
        this.enabled = false;
    }
}