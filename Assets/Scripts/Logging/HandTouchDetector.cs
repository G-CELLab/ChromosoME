using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for hand touch detection and logging.
/// Tracks which object the hand is currently touching with priority-based selection.
/// Subclass this for left and right hands.
/// </summary>
public abstract class HandTouchDetector : MonoBehaviour
{
    [SerializeField] private float clearTouchGraceSeconds = 0.2f;

    private HashSet<Collider> activeColliders = new HashSet<Collider>();
    private Collider priorityCollider;
    private float noTouchTimer = 0f;

    // ── Abstract methods for left/right specific touch tracking ───────────────

    protected abstract void SetTouchObject(string name);
    protected abstract void ClearTouchObject();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (activeColliders.Count == 0)
        {
            if (priorityCollider != null)
            {
                noTouchTimer += Time.deltaTime;
                if (noTouchTimer >= clearTouchGraceSeconds)
                    ClearCurrentTouch();
            }
            return;
        }

        noTouchTimer = 0f;

        bool removedStale    = activeColliders.RemoveWhere(IsColliderStale) > 0;
        bool priorityIsStale = IsColliderStale(priorityCollider);

        if (removedStale || priorityIsStale)
            UpdatePriorityCollider();
    }

    private void OnDisable()  => ResetTouchState();
    private void OnDestroy()  => ResetTouchState();

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (ShouldIgnore(other)) return;
        activeColliders.Add(other);
        noTouchTimer = 0f;
        UpdatePriorityCollider();
    }

    private void OnTriggerStay(Collider other)
    {
        if (ShouldIgnore(other)) return;
        if (!activeColliders.Contains(other))
        {
            activeColliders.Add(other);
            noTouchTimer = 0f;
            UpdatePriorityCollider();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        activeColliders.Remove(other);
        noTouchTimer = 0f;
        if (activeColliders.Count > 0)
            UpdatePriorityCollider();
    }

    // ── Priority Selection ────────────────────────────────────────────────────

    private void UpdatePriorityCollider()
    {
        activeColliders.RemoveWhere(IsColliderStale);

        Collider best = SelectBestCollider();
        if (best == null) return;

        if (priorityCollider != best)
        {
            priorityCollider = best;
            SetTouchObject(ColliderNameResolver.ResolveName(best.transform));
        }
    }

    private Collider SelectBestCollider()
    {
        Collider best  = null;
        int bestScore  = int.MinValue;

        foreach (var col in activeColliders)
        {
            if (col == null || ShouldIgnore(col)) continue;
            int score = GetColliderPriority(col);
            if (score > bestScore)
            {
                bestScore = score;
                best      = col;
            }
        }

        return best;
    }

    private int GetColliderPriority(Collider col)
    {
        if (col == null) return int.MinValue;

        // Nutrients — highest priority
        if (col.CompareTag("Nutrient")) return 1000;

        string lower = col.name.ToLowerInvariant();
        if (lower.Contains("vitamin") || lower.Contains("protein") || lower.Contains("magnesium"))
            return 900;

        // Wound — important gameplay interaction, beats environment objects
        if (lower.Contains("wound")) return 800;

        // CentrioleCopy must be checked before Centriole since "centriole"
        // is a substring of "centriolecopy"
        if (lower.Contains("centriolecopy") || lower.Contains("centriole_copy") || lower.Contains("centriole copy"))
            return 700;

        if (lower.Contains("centriole"))
            return 600;

        if (lower.Contains("mitochond")) return 100;

        // Ghost hand / intro objects — lowest deliberate priority so any
        // real gameplay object always wins; also clears cleanly via
        // MainLogger's phase-change flush
        if (lower.Contains("ghost")) return -100;

        return 500;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ResetTouchState()
    {
        activeColliders.Clear();
        noTouchTimer = 0f;
        ClearCurrentTouch();
    }

    private void ClearCurrentTouch()
    {
        priorityCollider = null;
        ClearTouchObject();
    }

    private bool IsColliderStale(Collider col)
        => col == null || !col.enabled || !col.gameObject.activeInHierarchy;

    private bool ShouldIgnore(Collider other)
    {
        if (other == null) return true;

        if (other.GetComponentInParent<LeftHandTouching>()  != null ||
            other.GetComponentInParent<RightHandTouching>() != null)
            return true;

        // "Hand" name check must be specific — avoid filtering gameplay objects
        // like WoundedHandT or WoundedHand that contain "Hand" as a substring.
        // Only ignore actual XR hand controller objects by checking for known
        // hand controller name patterns.
        string otherNameLower = other.name.ToLowerInvariant();
        bool isHandController = otherNameLower == "lefthand"
                             || otherNameLower == "righthand"
                             || otherNameLower.StartsWith("xrhand")
                             || otherNameLower.StartsWith("hand_l")
                             || otherNameLower.StartsWith("hand_r");

        // Ignore the XR rig root and any of its named hierarchy objects
        bool isXRRig = otherNameLower.Contains("xr origin")
                    || otherNameLower.Contains("xr rig")
                    || otherNameLower.Contains("xrorigin")
                    || otherNameLower.Contains("xrrig");

        return other.name == "TopPlatform"
            || other.CompareTag("Left")
            || other.CompareTag("Right")
            || other.transform.root.CompareTag("Left")
            || other.transform.root.CompareTag("Right")
            || isHandController
            || isXRRig;
    }
}