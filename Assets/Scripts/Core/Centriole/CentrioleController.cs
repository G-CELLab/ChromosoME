using UnityEngine;

/// <summary>
/// Handles centriole movement to its pole position during Prophase.
/// Draws a spindle fiber line once it reaches the pole and the hand touches it.
/// Replaces LeftCentrioleMove and RightCentrioleMove.
/// Assign Side in the inspector — Centriole1 = Left, Centriole2 = Right.
/// </summary>
public class CentrioleController : MonoBehaviour, IPhaseController
{
    public enum Side { Left, Right }

    [Header("Configuration")]
    public Side side;

    [Header("Movement")]
    public Transform endLocation;

    [Header("State")]
    public bool lineConnecting = false;
    public bool touched        = false;

    [Header("Connections")]
    [SerializeField] private GameObject target;
    [SerializeField] private GameObject finalTarget;
    public CentrioleTouchDetector hand;

    private LineRenderer lineRenderer;
    private float timer   = 0f;
    private bool isActive = false;

    private const float startDelay = 3f;
    private const float maxDuration = 10f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb   = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
    }

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase)
        {
            isActive = true;
            timer    = 0f;
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

        if (timer > startDelay && timer <= maxDuration)
            transform.position = Vector3.Lerp(transform.position, endLocation.position, 0.01f);

        DrawSpindleFiber();
    }

    // ── Spindle Fiber ─────────────────────────────────────────────────────────

    private void DrawSpindleFiber()
    {
        if (lineRenderer == null) return;

        bool handTouched = side == Side.Left ? hand.leftHandTouched : hand.rightHandTouched;

        if (lineConnecting && touched && !handTouched)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, target.transform.position);
        }
        else if (handTouched)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, finalTarget.transform.position);
        }
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        // Centriole reached its pole position
        if (other.CompareTag("Finish"))
        {
            lineConnecting = true;
            if (GetComponent<BoxCollider>())
                GetComponent<BoxCollider>().enabled = true;
        }

        // Hand touches the centriole after it has reached the pole
        if ((other.CompareTag("Left") || other.CompareTag("Right")) && lineConnecting)
        {
            bool alreadyTouched = side == Side.Left ? hand.leftHandTouched : hand.rightHandTouched;
            if (alreadyTouched) return;

            touched = true;

            if (hand != null)
            {
                if (side == Side.Left) hand.leftHandTouched  = true;
                else                   hand.rightHandTouched = true;
            }

            LogEventHelper.LogCentrioleMoved();

            if (GetComponent<BoxCollider>())
                GetComponent<BoxCollider>().enabled = false;

            CapsuleCollider child = GetComponentInChildren<CapsuleCollider>();
            if (child) child.enabled = false;

            Debug.Log($"[CentrioleController] {side} centriole locked.");
        }
    }
}