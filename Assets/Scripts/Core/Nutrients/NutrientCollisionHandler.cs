using UnityEngine;

/// <summary>
/// Handles nutrient collection during Interphase.
/// Player holds their hand near a nutrient for 2 seconds (without grabbing) to collect it.
/// All three nutrients share the "Nutrient" tag — each is tracked by GameObject reference.
/// Once all 3 are collected, triggers InterphasePart2.
/// </summary>
public class NutrientCollisionHandler : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public GameManager gameManager;
    public LeftHandManager leftHandManager;
    public RightHandManager rightHandManager;

    [Header("Nutrients")]
    public GameObject protein;
    public GameObject magnesium;
    public GameObject vitaminC;

    [Header("Scene Objects")]
    public GameObject centriole1;
    public GameObject centriole2;
    private bool proteinCollected   = false;
    private bool magnesiumCollected = false;
    private bool vitaminCCollected  = false;
    private bool interphasePart2Triggered = false;

    private bool leftHandInTrigger  = false;
    private bool rightHandInTrigger = false;

    private float proteinTimer   = 0f;
    private float magnesiumTimer = 0f;
    private float vitaminCTimer  = 0f;

    private const float collectTime = 2f;
    private bool isActive = false;

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Interphase)
            isActive = true;
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Interphase)
            isActive = false;
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left"))  leftHandInTrigger  = true;
        if (other.CompareTag("Right")) rightHandInTrigger = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left"))  leftHandInTrigger  = false;
        if (other.CompareTag("Right")) rightHandInTrigger = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isActive) return;
        if (!other.CompareTag("Nutrient")) return;

        bool isGrabbing = leftHandManager.isGrabbed_left || rightHandManager.isGrabbed_right;
        bool handNearby = leftHandInTrigger || rightHandInTrigger;

        // Only collect if hand is near but NOT grabbing
        if (isGrabbing || !handNearby) return;

        if (other.gameObject == protein && !proteinCollected)
        {
            proteinTimer += Time.deltaTime;
            if (proteinTimer >= collectTime)
            {
                proteinCollected = true;
                gameManager.FoodCollision();
                Destroy(protein);
                Debug.Log("[NutrientCollisionHandler] Protein collected.");
            }
        }
        else if (other.gameObject == magnesium && !magnesiumCollected)
        {
            magnesiumTimer += Time.deltaTime;
            if (magnesiumTimer >= collectTime)
            {
                magnesiumCollected = true;
                gameManager.FoodCollision();
                Destroy(magnesium);
                Debug.Log("[NutrientCollisionHandler] Magnesium collected.");
            }
        }
        else if (other.gameObject == vitaminC && !vitaminCCollected)
        {
            vitaminCTimer += Time.deltaTime;
            if (vitaminCTimer >= collectTime)
            {
                vitaminCCollected = true;
                gameManager.FoodCollision();
                Destroy(vitaminC);
                Debug.Log("[NutrientCollisionHandler] Vitamin C collected.");
            }
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || interphasePart2Triggered) return;

        if (proteinCollected && magnesiumCollected && vitaminCCollected)
        {
            interphasePart2Triggered = true;

            if (centriole1 != null) centriole1.GetComponent<CapsuleCollider>().enabled = true;
            if (centriole2 != null) centriole2.GetComponent<CapsuleCollider>().enabled = true;

            gameManager.InterphasePart2();
            Debug.Log("[NutrientCollisionHandler] All nutrients collected — InterphasePart2.");
        }
    }
}