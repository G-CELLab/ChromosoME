using UnityEngine;

/// <summary>
/// Handles centriole movement to its pole position during Prophase.
/// Uses Rigidbody.MovePosition/MoveRotation in FixedUpdate for smooth movement.
/// </summary>
public class CentrioleController : MonoBehaviour, IPhaseController
{
    public enum Side { Left, Right }

    [Header("Configuration")]
    public Side side;

    [Header("Movement")]
    public Transform endLocation;
    [Tooltip("Speed the centriole moves toward its pole in meters per second.")]
    public float moveSpeed = 0.5f;
    [Tooltip("Speed the centriole rotates to match its pole orientation in degrees per second.")]
    public float rotationSpeed = 2f;
    [Tooltip("Seconds after Prophase starts before the centriole begins moving.")]
    public float startDelay = 1.5f;

    private Rigidbody rb;
    private bool isActive = false;
    private float timer   = 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb            = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
        }

        rb.isKinematic = true;
    }

    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase)
        {
            isActive = true;
            timer    = 0f;
            Debug.Log($"[CentrioleController] {side} centriole moving to pole.");
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase)
        {
            isActive = false;
            timer    = 0f;
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive) return;
        timer += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!isActive || timer < startDelay) return;

        Vector3 newPosition = Vector3.MoveTowards(
            rb.position,
            endLocation.position,
            moveSpeed * Time.fixedDeltaTime
        );

        Quaternion newRotation = Quaternion.RotateTowards(
            rb.rotation,
            endLocation.rotation,
            rotationSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(newPosition);
        rb.MoveRotation(newRotation);
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Finish"))
        {
            isActive = false;
            rb.MovePosition(endLocation.position);
            rb.MoveRotation(endLocation.rotation);
            Debug.Log($"[CentrioleController] {side} centriole reached pole.");
        }
    }
}