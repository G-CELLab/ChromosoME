using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Interphase)
        {
            isActive = true;
            ResetState();
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Interphase)
            isActive = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isActive || interphasePart2Triggered) return;

        if (MatchesNutrient(other, protein, "protein") && !proteinCollected)
        {
            proteinTimer += Time.deltaTime;
            if (proteinTimer >= collectTime)
            {
                proteinCollected = true;
                MainLogger.LogOtherEvent("System:Nutrient:ProteinCollected");
                gameManager?.NutrientCollision();
                CancelSelectionBeforeDisable(protein);
                protein.SetActive(false);
            }
        }
        else if (MatchesNutrient(other, magnesium, "magnesium") && !magnesiumCollected)
        {
            magnesiumTimer += Time.deltaTime;
            if (magnesiumTimer >= collectTime)
            {
                magnesiumCollected = true;
                MainLogger.LogOtherEvent("System:Nutrient:MagnesiumCollected");
                gameManager?.NutrientCollision();
                CancelSelectionBeforeDisable(magnesium);
                magnesium.SetActive(false);
            }
        }
        else if (MatchesNutrient(other, vitaminC, "vitamin") && !vitaminCCollected)
        {
            vitaminCTimer += Time.deltaTime;
            if (vitaminCTimer >= collectTime)
            {
                vitaminCCollected = true;
                MainLogger.LogOtherEvent("System:Nutrient:VitaminCCollected");
                gameManager?.NutrientCollision();
                CancelSelectionBeforeDisable(vitaminC);
                vitaminC.SetActive(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (MatchesNutrient(other, protein,   "protein")   && !proteinCollected)   proteinTimer   = 0f;
        if (MatchesNutrient(other, magnesium, "magnesium") && !magnesiumCollected) magnesiumTimer = 0f;
        if (MatchesNutrient(other, vitaminC,  "vitamin")   && !vitaminCCollected)  vitaminCTimer  = 0f;
    }

    private void Update()
    {
        if (!isActive || interphasePart2Triggered) return;

        if (proteinCollected && magnesiumCollected && vitaminCCollected)
        {
            interphasePart2Triggered = true;
            gameManager?.InterphasePart2();
        }
    }

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

    private void CancelSelectionBeforeDisable(GameObject nutrient)
    {
        if (nutrient == null) return;
        var grab = nutrient.GetComponent<XRGrabInteractable>();
        if (grab == null) return;
        var manager = grab.interactionManager;
        if (manager == null) return;
        manager.CancelInteractableSelection(
            (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
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

        if (protein   != null) protein.SetActive(true);
        if (magnesium != null) magnesium.SetActive(true);
        if (vitaminC  != null) vitaminC.SetActive(true);
    }
}