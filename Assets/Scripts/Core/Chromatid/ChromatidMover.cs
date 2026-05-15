using UnityEngine;
/// <summary>
/// Moves a chromatid to its target position during Anaphase.
/// </summary>
public class ChromatidMover : MonoBehaviour, IPhaseController
{
    [Header("Anaphase Movement")]
    public Transform anaphaseEndLocation;

    private float timer   = 0f;
    private bool isDone   = false;
    private bool isActive = false;

    private const float startDelay       = 2f;
    private const float anaphaseDuration = 10f;

    // ── IPhaseController ──────────────────────────────────────────────────────
    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Anaphase)
        {
            isActive = true;
            isDone   = false;
            timer    = 0f;
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Anaphase)
            isActive = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!isActive || isDone) return;

        timer += Time.deltaTime;

        if (timer <= startDelay) return;

        MoveAnaphase();
    }

    private void MoveAnaphase()
    {
        if (anaphaseEndLocation == null) return;

        transform.position = Vector3.Lerp(transform.position, anaphaseEndLocation.position, 0.01f);

        if (timer >= anaphaseDuration)
        {
            isDone = true;
            Debug.Log("[ChromatidMover] Anaphase movement complete.");
        }
    }
}