using UnityEngine;

/// <summary>
/// Detects when both hands have touched their respective centrioles during Prophase.
/// Updates UI to signal the player can proceed to Metaphase.
/// </summary>
public class CentrioleTouchDetector : MonoBehaviour, IPhaseController
{
    [Header("State")]
    public bool leftHandTouched  = false;
    public bool rightHandTouched = false;

    [Header("Connections")]
    public CentrioleController leftCentriole;
    public CentrioleController rightCentriole;
    public MetaphaseAlignmentDetector alignmentDetector;

    [Header("UI")]
    public GameObject prophaseInfo;
    public GameObject metaphaseInfo;

    private bool isActive = false;

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase)
        {
            isActive         = true;
            leftHandTouched  = false;
            rightHandTouched = false;
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase)
            isActive = false;
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        if (other.CompareTag("Left") && leftCentriole.touched)
            leftHandTouched = true;
        else if (other.CompareTag("Right") && rightCentriole.touched)
            rightHandTouched = true;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive) return;
        if (!leftHandTouched || !rightHandTouched) return;
        if (alignmentDetector != null && alignmentDetector.alignmentSuccess) return;

        if (prophaseInfo  != null) prophaseInfo.SetActive(false);
        if (metaphaseInfo != null) metaphaseInfo.SetActive(true);
    }
}