using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Comprehensive logging system that tracks:
/// - Gestures (left/right grab)
/// - Touches (what objects hands are touching)
/// - Info Panel state (which tutorial panel is active)
/// - Phase changes (game phase transitions)
/// - Other events (wound trigger, ATP charged, etc.)
/// - User speech and AI speech/gesture events
///
/// Logs to console and MainLog.csv with columns:
/// Time, Left Gesture, Right Gesture, Left Touch, Right Touch, Phase, User_Speech, AI_Speech, AI_Gesture, Other
///
/// Also caches the most recently computed values via public Last* properties
/// so CombinedLogger can merge them with hand/player position data without
/// re-deriving or re-dequeuing anything.
/// </summary>
public class MainLogger : MonoBehaviour
{
    [Header("Hand References")]
    [SerializeField] private HandManager leftHandManager;
    [SerializeField] private HandManager rightHandManager;

    [Header("Game References")]
    [SerializeField] private GameManager gameManager;

    [Header("Logging Settings")]
    [SerializeField] private float loggingInterval = 0.1f;
    [SerializeField] private bool logToConsole = false;
    [SerializeField] private bool logToCSV = true;

    // CSV and timing
    private string csvFilePath;
    private float timeSinceLastFrameLog = 0f;
    private float sessionStartTime;

    // Keep one time anchor across cycle file rollovers/re-creations
    private static bool hasGlobalSessionStartTime;
    private static float globalSessionStartTime;

    // Cycle tracking
    private int currentCycle = 0;
    private int lastCycle = -1;

    // Event queue for "Other" column
    private Queue<string> otherEventQueue = new Queue<string>();

    // Queue for AI-agent gesture events (speech-driven gesture triggers).
    private Queue<string> aiGestureEventQueue = new Queue<string>();

    // Queue for user utterances sent to the AI agent.
    private Queue<string> userSpeechQueue = new Queue<string>();

    // Last valid touch values used to smooth transient trigger dropouts while grabbing.
    private string lastStableLeftTouch  = "";
    private string lastStableRightTouch = "";

    public static MainLogger instance;

    // ── Cached last-frame values (read by CombinedLogger) ──────────────────────

    public float  LastElapsed      { get; private set; }
    public string LastLeftGesture  { get; private set; } = "";
    public string LastRightGesture { get; private set; } = "";
    public string LastLeftTouch    { get; private set; } = "";
    public string LastRightTouch   { get; private set; } = "";
    public string LastPhase        { get; private set; } = "";
    public string LastUserSpeech   { get; private set; } = "";
    public string LastAISpeech     { get; private set; } = "";
    public string LastAIGesture    { get; private set; } = "";
    public string LastOther        { get; private set; } = "";

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Start()
    {
        if (logToConsole)
            Debug.Log("[MainLogger] START called");

        if (!hasGlobalSessionStartTime)
        {
            globalSessionStartTime    = Time.time;
            hasGlobalSessionStartTime = true;
        }
        sessionStartTime = globalSessionStartTime;

        if (leftHandManager == null)
        {
            Debug.LogError("[MainLogger] Left HandManager not assigned!");
            enabled = false;
            return;
        }
        if (rightHandManager == null)
        {
            Debug.LogError("[MainLogger] Right HandManager not assigned!");
            enabled = false;
            return;
        }

        if (logToConsole)
        {
            Debug.Log("[MainLogger] Found Left Hand Manager");
            Debug.Log("[MainLogger] Found Right Hand Manager");
        }

        if (logToCSV)
        {
            currentCycle = GameManager.GetHealingCycleCount();
            lastCycle    = currentCycle;
            InitializeCSVFile();
        }

        if (logToConsole)
            Debug.Log("[MainLogger] Initialized. Logging to: " + csvFilePath);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            hasGlobalSessionStartTime = false;
            globalSessionStartTime    = 0f;
        }
    }

    private void Update()
    {
        currentCycle = GameManager.GetHealingCycleCount();
        if (currentCycle != lastCycle && currentCycle < GameManager.MAX_HEALING_CYCLES)
        {
            lastCycle = currentCycle;
            if (logToCSV)
            {
                InitializeCSVFile();
                if (logToConsole)
                    Debug.Log($"[MainLogger] Started new cycle {currentCycle + 1}");
            }
        }

        timeSinceLastFrameLog += Time.deltaTime;
        if (timeSinceLastFrameLog >= loggingInterval)
        {
            LogFrame();
            timeSinceLastFrameLog = 0f;
        }
    }

    private void LogFrame()
    {
        float elapsed = Time.time - sessionStartTime;

        string leftGesture  = GetLeftGesture();
        string rightGesture = GetRightGesture();
        string aiGesture    = GetAIGestureEvents();
        string userSpeech   = GetUserSpeechEvents();
        string leftTouch    = GetLeftTouch();
        string rightTouch   = GetRightTouch();
        string phase        = GetCurrentPhase();
        string aiSpeech     = GetAISpeech();
        string otherEvent   = otherEventQueue.Count > 0 ? otherEventQueue.Dequeue() : "";

        // Cache for CombinedLogger
        LastElapsed      = elapsed;
        LastLeftGesture  = leftGesture;
        LastRightGesture = rightGesture;
        LastLeftTouch    = leftTouch;
        LastRightTouch   = rightTouch;
        LastPhase        = phase;
        LastUserSpeech   = userSpeech;
        LastAISpeech     = aiSpeech;
        LastAIGesture    = aiGesture;
        LastOther        = otherEvent;

        if (logToConsole)
            Debug.Log($"[MainLog] T={elapsed:F2}s | L_Ges:{leftGesture} | R_Ges:{rightGesture} | AI_Ges:{aiGesture} | User:{userSpeech} | L_Touch:{leftTouch} | R_Touch:{rightTouch} | Phase:{phase} | AI:{aiSpeech} | Other:{otherEvent}");

        if (logToCSV)
            WriteToCSV(elapsed, leftGesture, rightGesture, leftTouch, rightTouch, phase, userSpeech, aiSpeech, aiGesture, otherEvent);

        if (!string.IsNullOrEmpty(aiSpeech))
            TextToSpeechPlayer.ClearCurrentSpeech();
    }

    private string GetLeftGesture()
    {
        if (leftHandManager == null || !leftHandManager.isGrabbed)
            return "";
        return "Left_Grab";
    }

    private string GetRightGesture()
    {
        if (rightHandManager == null || !rightHandManager.isGrabbed)
            return "";
        return "Right_Grab";
    }

    private string GetLeftTouch()
    {
        bool isGrabbing = leftHandManager != null && leftHandManager.isGrabbed;
        if (isGrabbing)
        {
            string selectedNutrient = GetSelectedNutrientName(leftHandManager);
            if (!string.IsNullOrEmpty(selectedNutrient))
            {
                lastStableLeftTouch = selectedNutrient;
                return selectedNutrient;
            }
        }

        string rawTouch     = TouchTracker.GetLeftTouchObject();
        string cleanedTouch = SanitizeTouchValue(rawTouch);

        if (!string.IsNullOrEmpty(cleanedTouch))
        {
            lastStableLeftTouch = cleanedTouch;
            return cleanedTouch;
        }

        if (isGrabbing && !string.IsNullOrEmpty(lastStableLeftTouch))
            return lastStableLeftTouch;

        if (!isGrabbing)
            lastStableLeftTouch = "";

        return "";
    }

    private string GetRightTouch()
    {
        bool isGrabbing = rightHandManager != null && rightHandManager.isGrabbed;
        if (isGrabbing)
        {
            string selectedNutrient = GetSelectedNutrientName(rightHandManager);
            if (!string.IsNullOrEmpty(selectedNutrient))
            {
                lastStableRightTouch = selectedNutrient;
                return selectedNutrient;
            }
        }

        string rawTouch     = TouchTracker.GetRightTouchObject();
        string cleanedTouch = SanitizeTouchValue(rawTouch);

        if (!string.IsNullOrEmpty(cleanedTouch))
        {
            lastStableRightTouch = cleanedTouch;
            return cleanedTouch;
        }

        if (isGrabbing && !string.IsNullOrEmpty(lastStableRightTouch))
            return lastStableRightTouch;

        if (!isGrabbing)
            lastStableRightTouch = "";

        return "";
    }

    private string SanitizeTouchValue(string touchValue)
    {
        if (string.IsNullOrWhiteSpace(touchValue))
            return "";

        string normalized = touchValue.Trim();
        string lower      = normalized.ToLowerInvariant();

        if (lower.Contains("lefttouchdetector") || lower.Contains("righttouchdetector"))
            return "";

        if (lower.Contains("protein"))   return "Protein";
        if (lower.Contains("magnesium")) return "Magnesium";
        if (lower.Contains("vitamin"))   return "VitaminC";

        return normalized;
    }

    private string GetSelectedNutrientName(Component handManager)
    {
        if (handManager == null) return "";

        NearFarInteractor interactor = handManager.GetComponentInChildren<NearFarInteractor>();
        if (interactor == null || !interactor.hasSelection) return "";

        object selected = TryGetSelectedInteractable(interactor);
        if (selected == null) return "";

        Transform selectedTransform = GetInteractableTransform(selected);
        return ResolveNutrientName(selectedTransform);
    }

    private object TryGetSelectedInteractable(object interactor)
    {
        if (interactor == null) return null;

        Type interactorType = interactor.GetType();
        PropertyInfo firstSelectedProperty = interactorType.GetProperty("firstInteractableSelected", BindingFlags.Public | BindingFlags.Instance);
        object firstSelected = firstSelectedProperty?.GetValue(interactor);
        if (firstSelected != null) return firstSelected;

        PropertyInfo selectedListProperty = interactorType.GetProperty("interactablesSelected", BindingFlags.Public | BindingFlags.Instance);
        object selectedList = selectedListProperty?.GetValue(interactor);
        if (selectedList is System.Collections.IEnumerable enumerable)
        {
            foreach (object item in enumerable)
                if (item != null) return item;
        }

        return null;
    }

    private Transform GetInteractableTransform(object interactable)
    {
        if (interactable is Component component) return component.transform;

        PropertyInfo transformProperty = interactable.GetType().GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
        if (transformProperty?.GetValue(interactable) is Transform transform) return transform;

        return null;
    }

    private string ResolveNutrientName(Transform selectedTransform)
    {
        Transform current = selectedTransform;
        while (current != null)
        {
            if (current.CompareTag("Food"))  return "Protein";
            if (current.CompareTag("Food2")) return "Magnesium";
            if (current.CompareTag("Food3")) return "VitaminC";

            string lower = current.name.ToLowerInvariant();
            if (lower.Contains("protein"))   return "Protein";
            if (lower.Contains("magnesium")) return "Magnesium";
            if (lower.Contains("vitamin"))   return "VitaminC";

            current = current.parent;
        }
        return "";
    }

    private string GetAIGestureEvents()
    {
        if (aiGestureEventQueue.Count == 0) return "";
        var eventsThisFrame = new List<string>();
        while (aiGestureEventQueue.Count > 0)
            eventsThisFrame.Add(aiGestureEventQueue.Dequeue());
        return string.Join("|", eventsThisFrame);
    }

    private string GetUserSpeechEvents()
    {
        if (userSpeechQueue.Count == 0) return "";
        var utterancesThisFrame = new List<string>();
        while (userSpeechQueue.Count > 0)
            utterancesThisFrame.Add(userSpeechQueue.Dequeue());
        return string.Join(" | ", utterancesThisFrame);
    }

    private string GetCurrentPhase()
    {
        if (gameManager == null) return "";
        return GameManager.eGameStatus.ToString();
    }

    private string GetAISpeech()
    {
        string speech = TextToSpeechPlayer.GetCurrentSpeech();
        return string.IsNullOrEmpty(speech) ? "" : speech;
    }

    private void InitializeCSVFile()
    {
        try
        {
            string cycleNumber = (currentCycle + 1).ToString();
            csvFilePath = TrialLogPath.GetFilePath($"MainLog_Cycle{cycleNumber}.csv");

            string directory = Path.GetDirectoryName(csvFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = new StreamWriter(csvFilePath, false, new UTF8Encoding(true)))
                writer.WriteLine("Time(s),Left_Gesture,Right_Gesture,Left_Touch,Right_Touch,Phase,User_Speech,AI_Speech,AI_Gesture,Other");
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainLogger] Failed to initialize CSV: " + ex.Message);
        }
    }

    private void WriteToCSV(float elapsed, string leftGesture, string rightGesture, string leftTouch,
                            string rightTouch, string phase, string userSpeech, string aiSpeech,
                            string aiGesture, string otherEvent)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, true, new UTF8Encoding(true)))
            {
                writer.WriteLine(
                    $"{elapsed:F3}," +
                    $"{EscapeCsvField(leftGesture)}," +
                    $"{EscapeCsvField(rightGesture)}," +
                    $"{EscapeCsvField(leftTouch)}," +
                    $"{EscapeCsvField(rightTouch)}," +
                    $"{EscapeCsvField(phase)}," +
                    $"{EscapeCsvField(userSpeech)}," +
                    $"{EscapeCsvField(aiSpeech)}," +
                    $"{EscapeCsvField(aiGesture)}," +
                    $"{EscapeCsvField(otherEvent)}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainLogger] Failed to write to CSV: " + ex.Message);
        }
    }

    private string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    public static void LogOtherEvent(string eventName)
    {
        if (instance != null)
            instance.otherEventQueue.Enqueue(eventName);
    }

    public static void LogAIGestureEvent(string gestureName)
    {
        if (instance == null || string.IsNullOrEmpty(gestureName)) return;
        instance.aiGestureEventQueue.Enqueue(gestureName);
    }

    public static void LogUserSpeech(string utterance)
    {
        if (instance == null || string.IsNullOrWhiteSpace(utterance)) return;
        instance.userSpeechQueue.Enqueue(utterance.Trim());
    }

    public string GetCSVFilePath() => csvFilePath;
}