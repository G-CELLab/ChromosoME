using UnityEngine;

/// <summary>
/// Moves a chromosome to its metaphase plate position when Metaphase begins.
/// Activates chromatid objects and disables itself on completion.
/// </summary>
public class ChromosomeMover : MonoBehaviour, IPhaseController
{
    [Header("Metaphase Movement")]
    public Transform metaphaseEndLocation;
    public GameObject chromatidLeft;
    public GameObject chromatidRight;

    private float timer   = 0f;
    private bool isDone   = false;
    private bool isActive = false;

    private const float startDelay = 2f;
    private const float moveSpeed  = 0.2f; // units per second — tune to match world scale

    // ── IPhaseController ──────────────────────────────────────────────────────

    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase)
        {
            isActive = true;
            isDone   = false;
            timer    = 0f;
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase)
            isActive = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || isDone) return;

        timer += Time.deltaTime;

        if (timer <= startDelay) return;

        MoveMetaphase();
    }

    private void MoveMetaphase()
    {
        if (metaphaseEndLocation == null) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            metaphaseEndLocation.position,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, metaphaseEndLocation.position) < 0.01f)
        {
            transform.position = metaphaseEndLocation.position;
            OnMovementComplete();
        }
    }

    private void OnMovementComplete()
    {
        if (chromatidLeft  != null) chromatidLeft.SetActive(true);
        if (chromatidRight != null) chromatidRight.SetActive(true);

        isDone = true;
        gameObject.SetActive(false);
        Debug.Log("[ChromosomeMover] Metaphase movement complete.");
    }
}