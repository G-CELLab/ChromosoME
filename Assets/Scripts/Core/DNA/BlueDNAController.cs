using UnityEngine;

public class BlueDNAController : MonoBehaviour, IPhaseController
{
    [Header("Settings")]
    public float condenseDelay = 3.5f;

    private Animator animator;
    private float    timer    = 0f;
    private bool     isDone   = false;
    private bool     isActive = false;

    private void Start()     => animator = GetComponent<Animator>();
    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase != GameManager.GameState.Prophase) return;
        isActive = true;
        isDone   = false;
        timer    = 0f;
        animator?.Play("idle", 0, 0f);
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase)
            isActive = false;
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
            Debug.Log("[BlueDNAController] AI DNA condensation triggered.");
        }
    }
}