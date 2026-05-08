using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allows the player to grab this object during specified phases.
/// Movement is handled by XRGrabInteractable — this script gates
/// interaction to the configured active phases only.
/// Replaces MetaphaseGrabHandler and ChromatidAnaphaseGrabHandler.
/// </summary>
public class PhaseGrabHandler : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public LeftHandManager leftHand;
    public RightHandManager rightHand;

    [Header("Active Phases")]
    [Tooltip("This object can only be grabbed during these phases.")]
    public List<GameManager.GameState> activePhases = new List<GameManager.GameState>();

    [Header("Settings")]
    public float grabRange = 0.4f;

    private Transform activeHand;
    private bool isActive = false;

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (activePhases.Contains(phase))
            isActive = true;
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (activePhases.Contains(phase) && !activePhases.Contains(GameManager.eGameStatus))
        {
            isActive   = false;
            activeHand = null;
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive) return;

        // Release check
        if (activeHand != null)
        {
            bool stillHeld = (activeHand == leftHand?.transform  && leftHand.isGrabbed_left)
                          || (activeHand == rightHand?.transform && rightHand.isGrabbed_right);
            if (!stillHeld) activeHand = null;
        }

        // New grab check
        if (activeHand == null)
        {
            if (leftHand != null && leftHand.isGrabbed_left && IsNear(leftHand.transform))
                activeHand = leftHand.transform;
            else if (rightHand != null && rightHand.isGrabbed_right && IsNear(rightHand.transform))
                activeHand = rightHand.transform;
        }
    }

    private bool IsNear(Transform hand)
        => Vector3.Distance(transform.position, hand.position) < grabRange;
}