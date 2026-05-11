using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Handles DNA condensation during Prophase.
/// Player must hold/grab the DNA for 6 seconds to condense it.
/// Releasing early causes progress to revert at the same speed.
/// Drives CondenseProgress (0-1) on the Animator for DNAColliderManager collider scaling.
/// </summary>
public class DNACondenser : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;

    // TODO: Review for removal if tutorial system is cut
    public GameObject text1;
    public GameObject text2;

    [Header("Condense Settings")]
    public float targetCondenseTime = 6f;
    public float prophaseStartDelay = 2f;

    [Header("Completion Effect")]
    public ParticleSystem completionParticles;

    private Animator animator;
    private XRGrabInteractable grabInteractable;
    private LeftHandManager leftManager;
    private RightHandManager rightManager;

    private float progress = 0f;
    private float delayTimer = 0f;
    private bool isDone = false;
    private Transform activeHand;
    private Vector3 grabPosOffset;
    private Quaternion grabRotOffset;

    private const string PARAM_PROGRESS  = "CondenseProgress";
    private const string PARAM_COMPLETE  = "CondenseComplete";
    private const string PARAM_IS_OPENED = "isOpened";
    private const string PARAM_IS_IDLE   = "isIdle";

    void Start()
    {
        animator         = GetComponent<Animator>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        leftManager      = Object.FindAnyObjectByType<LeftHandManager>();
        rightManager     = Object.FindAnyObjectByType<RightHandManager>();

        if (grabInteractable != null)
        {
            grabInteractable.movementType  = XRBaseInteractable.MovementType.Instantaneous;
            grabInteractable.useDynamicAttach = true;
        }
    }

    void OnEnable()
    {
        ResetState();
    }

    void Update()
    {
        if (GameManager.eGameStatus != GameManager.GameState.Prophase)
        {
            if (GameManager.eGameStatus == GameManager.GameState.Intro ||
                GameManager.eGameStatus == GameManager.GameState.Interphase)
            {
                if (isDone || progress > 0 || delayTimer > 0)
                    ResetState();
            }
            return;
        }

        if (isDone) return;

        delayTimer += Time.deltaTime;
        if (delayTimer < prophaseStartDelay) return;

        bool isHeld = IsBeingHeld();

        if (isHeld)
        {
            progress = Mathf.Clamp01(progress + Time.deltaTime / targetCondenseTime);
            SetAnimatorProgress(progress);

            if (progress >= 1f)
                CompleteCondensation();
        }
        else if (progress > 0f)
        {
            progress = Mathf.Clamp01(progress - Time.deltaTime / targetCondenseTime);
            SetAnimatorProgress(progress);
        }
    }

    // ── Grab detection ────────────────────────────────────────────────────────

    private bool IsBeingHeld()
    {
        bool heldByXRI = grabInteractable != null && grabInteractable.isSelected;
        HandleManualGrab();
        return heldByXRI || activeHand != null;
    }

    private void HandleManualGrab()
    {
        if (activeHand != null)
        {
            bool stillHeld = (activeHand == leftManager?.transform  && leftManager.isGrabbed_left)
                          || (activeHand == rightManager?.transform && rightManager.isGrabbed_right);
            if (!stillHeld) activeHand = null;
        }

        if (activeHand == null)
        {
            if (leftManager != null && leftManager.isGrabbed_left && IsNear(leftManager.transform))
            {
                activeHand    = leftManager.transform;
                grabPosOffset = activeHand.InverseTransformPoint(transform.position);
                grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
            }
            else if (rightManager != null && rightManager.isGrabbed_right && IsNear(rightManager.transform))
            {
                activeHand    = rightManager.transform;
                grabPosOffset = activeHand.InverseTransformPoint(transform.position);
                grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
            }
        }
    }

    private bool IsNear(Transform hand)
        => Vector3.Distance(transform.position, hand.position) < 0.25f;

    // ── Animator helpers ──────────────────────────────────────────────────────

    private void SetAnimatorProgress(float value)
    {
        if (animator == null) return;
        if (HasParam(PARAM_PROGRESS))  animator.SetFloat(PARAM_PROGRESS, value);
        if (HasParam(PARAM_IS_OPENED)) animator.SetBool(PARAM_IS_OPENED, value > 0f);
        if (HasParam(PARAM_IS_IDLE))   animator.SetBool(PARAM_IS_IDLE,   value <= 0f);
    }

    private bool HasParam(string paramName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == paramName) return true;
        return false;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private void ResetState()
    {
        isDone     = false;
        progress   = 0f;
        delayTimer = 0f;
        activeHand = null;
        SetAnimatorProgress(0f);
    }

    private void CompleteCondensation()
    {
        if (isDone) return;
        isDone = true;

        Debug.Log("[DNACondenser] DNA condensed — transitioning to Metaphase.");

        if (animator != null && HasParam(PARAM_COMPLETE))
            animator.SetTrigger(PARAM_COMPLETE);

        if (completionParticles != null)
            completionParticles.Play();

        // TODO: Remove if tutorial system is cut
        if (text1 != null) text1.SetActive(false);
        if (text2 != null) text2.SetActive(true);

        if (gameManager != null)
            gameManager.Metaphase();
    }
}