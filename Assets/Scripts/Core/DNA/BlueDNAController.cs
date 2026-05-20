using UnityEngine;

public class BlueDNAController : MonoBehaviour, IPhaseController
{
    [Header("References")]
    [SerializeField] private GameObject DNAText;
    [SerializeField] private GameObject chromosomeText;

    [Header("Settings")]
    public float condenseDelay = 5.9f;

    private Animator animator;
    private float    timer    = 0f;
    private bool     isDone   = false;
    private bool     isActive = false;
    private bool     isInitialized = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        isInitialized = true;
        ResetState();
    }

    private void OnEnable()
    {
        GameManager.Register(this);
        if (isInitialized) ResetState();
    }

    private void OnDisable() => GameManager.Unregister(this);

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase != GameManager.GameState.Prophase) return;

        ResetState();
        isActive = true;
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase && !isDone)
            ResetState();
    }

    private void Update()
    {
        if (!isActive || isDone) return;

        timer += Time.deltaTime;

        float normalized = Mathf.Clamp01(timer / condenseDelay);
        animator?.Play("Scene", 0, normalized);

        if (timer >= condenseDelay)
        {
            isDone = true;
            DNAText.SetActive(false);
            chromosomeText.SetActive(true);
            Debug.Log("[BlueDNAController] AI DNA condensation triggered.");
        }
    }

    private void ResetState()
    {
        isDone   = false;
        timer    = 0f;
        isActive = false;

        if (animator != null && animator.isInitialized)
            animator.Play("idle", 0, 0f);
    }
}