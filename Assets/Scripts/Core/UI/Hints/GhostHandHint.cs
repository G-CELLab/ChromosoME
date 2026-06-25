using UnityEngine;

public class GhostHandHint : MonoBehaviour
{
    [Header("Ghost Hand Settings")]
    [SerializeField] private GameObject ghostHandObject;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float targetAlpha = 0.4f;

    [Header("Collider Trigger")]
    [SerializeField] private Collider hintCollider;

    private Renderer[] renderers;
    private Animator animator;
    private float currentAlpha;
    private bool playerHandInside = false;
    private bool isPaused = false;

    void Start()
    {
        renderers = ghostHandObject.GetComponentsInChildren<Renderer>();
        animator  = GetComponent<Animator>();
        currentAlpha = targetAlpha;
        SetAlpha(currentAlpha);
        Debug.Log($"GhostHandHint found {renderers.Length} renderers");
    }

    void Update()
    {
        if (isPaused) return;
        float target = playerHandInside ? 0f : targetAlpha;
        currentAlpha = Mathf.MoveTowards(currentAlpha, target, fadeSpeed * Time.deltaTime);
        SetAlpha(currentAlpha);
    }

    public void PauseHint()
    {
        isPaused = true;
        playerHandInside = false; // Reset to ensure it fades back in when resumed
        if (animator != null) animator.speed = 0f;
        if (hintCollider != null) hintCollider.enabled = false;
        Debug.Log("[GhostHandHint] Paused.");
    }

    public void ResumeHint()
    {
        isPaused = false;
        if (animator != null) animator.speed = 1f;
        if (hintCollider != null) hintCollider.enabled = true;
        Debug.Log("[GhostHandHint] Resumed.");
    }

    void SetAlpha(float alpha)
    {
        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;
                Color emission = mat.GetColor("_EmissionColor");
                emission.a = alpha;
                mat.SetColor("_EmissionColor", emission * alpha);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Right"))
            playerHandInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Right"))
            playerHandInside = false;
    }

    public void DestroyHint()
    {
        Destroy(gameObject);
    }
}