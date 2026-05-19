using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DNACondenser : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public GameManager gameManager;
    [SerializeField] private GameObject DNAText;
    [SerializeField] private GameObject chromosomeText;

    [Header("Settings")]
    public float holdDuration = 6f;
    public float startDelay   = 2f;

    [Header("Debug")]
    [SerializeField] private float holdTimer = 0f;

    private Animator           animator;
    private XRGrabInteractable grabInteractable;
    private float delayTimer    = 0f;
    private bool  isDone        = false;
    private bool  xriHeld       = false;
    private bool  isInitialized = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        animator         = GetComponent<Animator>();
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void Start()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        isInitialized = true;
        ResetState();
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnEnable()
    {
        GameManager.Register(this);
        if (isInitialized) ResetState();
    }

    private void OnDisable() { GameManager.Unregister(this); }

    // ── XRI ───────────────────────────────────────────────────────────────────
    private void OnGrabbed(SelectEnterEventArgs args)  => xriHeld = true;
    private void OnReleased(SelectExitEventArgs args)  => xriHeld = false;

    // ── IPhaseController ──────────────────────────────────────────────────────
    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase) ResetState();
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase && !isDone) ResetState();
    }

    // ── Update ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (GameManager.eGameStatus != GameManager.GameState.Prophase) return;
        if (isDone) return;

        delayTimer += Time.deltaTime;
        if (delayTimer < startDelay) return;

        if (xriHeld)
            holdTimer = Mathf.Clamp(holdTimer + Time.deltaTime, 0f, holdDuration);
        else
            holdTimer = Mathf.Clamp(holdTimer - Time.deltaTime, 0f, holdDuration);

        // Scrub the Scene clip directly — 0 = start, 1 = fully condensed
        float normalizedTime = holdTimer / holdDuration;
        if (animator != null) animator.Play("Scene", 0, normalizedTime);

        if (holdTimer >= holdDuration)
            Complete();
    }

    // ── Complete ──────────────────────────────────────────────────────────────
    private void Complete()
    {
        if (isDone) return;
        isDone = true;

        DNAText.SetActive(false);
        chromosomeText.SetActive(true);

        if (gameManager != null) gameManager.Metaphase();
        Debug.Log("[DNACondenser] Condensed.");
    }

    // ── ResetState ────────────────────────────────────────────────────────────
    private void ResetState()
    {
        isDone     = false;
        holdTimer  = 0f;
        delayTimer = 0f;
        xriHeld    = false;
        if (animator != null && animator.isInitialized)
            animator.Play("idle", 0, 0f);
    }
}