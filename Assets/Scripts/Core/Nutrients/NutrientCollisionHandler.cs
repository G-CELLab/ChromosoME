using UnityEngine;

/// <summary>
/// Handles nutrient collection during Interphase.
/// Player places a nutrient inside the Mitochondrion trigger zone.
/// Each nutrient collects after 2 seconds of being inside the zone.
/// Once all 3 are collected, triggers InterphasePart2.
/// </summary>
public class NutrientCollisionHandler : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public GameManager gameManager;

    [Header("Nutrients")]
    public GameObject protein;
    public GameObject magnesium;
    public GameObject vitaminC;

    private bool proteinCollected         = false;
    private bool magnesiumCollected       = false;
    private bool vitaminCCollected        = false;
    private bool interphasePart2Triggered = false;

    private float proteinTimer   = 0f;
    private float magnesiumTimer = 0f;
    private float vitaminCTimer  = 0f;

    private const float collectTime = 2f;
    private bool isActive = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameManager.Register(this);
        Debug.Log("[NutrientCollisionHandler] Registered with GameManager.");
    }

    private void OnDisable() => GameManager.Unregister(this);

    private void Start()
    {
        if (gameManager == null)
            gameManager = Object.FindAnyObjectByType<GameManager>();
    }

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        Debug.Log($"[NutrientCollisionHandler] OnPhaseEnter: {phase}");
        if (phase == GameManager.GameState.Interphase)
            isActive = true;
            ResetState();
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Interphase)
            isActive = false;
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerStay(Collider other)
    {
        if (!isActive || interphasePart2Triggered) return;

        if (MatchesNutrient(other, protein, "protein") && !proteinCollected)
        {
            proteinTimer += Time.deltaTime;
            if (proteinTimer >= collectTime)
            {
                proteinCollected = true;
                gameManager?.FoodCollision();
                protein.SetActive(false);
                Debug.Log("[NutrientCollisionHandler] Protein collected.");
            }
        }
        else if (MatchesNutrient(other, magnesium, "magnesium") && !magnesiumCollected)
        {
            magnesiumTimer += Time.deltaTime;
            if (magnesiumTimer >= collectTime)
            {
                magnesiumCollected = true;
                gameManager?.FoodCollision();
                magnesium.SetActive(false);
                Debug.Log("[NutrientCollisionHandler] Magnesium collected.");
            }
        }
        else if (MatchesNutrient(other, vitaminC, "vitamin") && !vitaminCCollected)
        {
            vitaminCTimer += Time.deltaTime;
            if (vitaminCTimer >= collectTime)
            {
                vitaminCCollected = true;
                gameManager?.FoodCollision();
                vitaminC.SetActive(false);
                Debug.Log("[NutrientCollisionHandler] Vitamin C collected.");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (MatchesNutrient(other, protein,   "protein")   && !proteinCollected)   proteinTimer   = 0f;
        if (MatchesNutrient(other, magnesium, "magnesium") && !magnesiumCollected) magnesiumTimer = 0f;
        if (MatchesNutrient(other, vitaminC,  "vitamin")   && !vitaminCCollected)  vitaminCTimer  = 0f;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || interphasePart2Triggered) return;

        if (proteinCollected && magnesiumCollected && vitaminCCollected)
        {
            interphasePart2Triggered = true;
            gameManager?.InterphasePart2();
            Debug.Log("[NutrientCollisionHandler] All nutrients collected — InterphasePart2.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool MatchesNutrient(Collider other, GameObject nutrient, string nutrientKey)
    {
        if (other == null || nutrient == null) return false;
        if (other.gameObject == nutrient) return true;

        Transform current = other.transform;
        while (current != null)
        {
            if (current.gameObject == nutrient) return true;
            if (current.name.ToLowerInvariant().Contains(nutrientKey)) return true;
            current = current.parent;
        }

        return false;
    }

    private void ResetState()
    {
        proteinCollected         = false;
        magnesiumCollected       = false;
        vitaminCCollected        = false;
        interphasePart2Triggered = false;
        proteinTimer   = 0f;
        magnesiumTimer = 0f;
        vitaminCTimer  = 0f;
        Debug.Log("[NutrientCollisionHandler] State reset for new Interphase.");
    }
}