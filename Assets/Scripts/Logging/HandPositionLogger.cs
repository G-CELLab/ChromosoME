using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Logs left and right hand positions to console and CSV file.
/// Tracks the actual XR hand objects directly from the XROrigin.
///
/// Also caches the most recently computed positions via public Last*
/// properties so CombinedLogger can merge them with the other logs.
/// </summary>
public class HandPositionLogger : MonoBehaviour
{
    [Header("Hand References")]
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;
    
    [Header("Logging Settings")]
    [SerializeField] private float loggingInterval = 0.1f;
    [SerializeField] private bool logToConsole = false;
    [SerializeField] private bool logToCSV = true;
    
    private string csvFilePath;
    private float timeSinceLastLog = 0f;
    private float sessionStartTime;

    // Keep one time anchor across cycle file rollovers/re-creations
    private static bool hasGlobalSessionStartTime;
    private static float globalSessionStartTime;
    
    // Cycle tracking
    private int currentCycle = 0;
    private int lastCycle = -1;

    // Phase tracking for Telophase-aware rollover
    private string lastPhaseValue = "";

    // ── Cached last-frame values (read by CombinedLogger) ──────────────────────

    public Vector3 LastLeftPosition  { get; private set; }
    public Vector3 LastRightPosition { get; private set; }

    private void Start()
    {
        if (logToConsole)
            Debug.Log("[HandPositionLogger] START called");
        
        if (leftHandTransform == null || rightHandTransform == null)
        {
            Debug.LogError("[HandPositionLogger] Left Hand Transform or Right Hand Transform not assigned in Inspector!");
            enabled = false;
            return;
        }

        if (!hasGlobalSessionStartTime)
        {
            globalSessionStartTime = Time.time;
            hasGlobalSessionStartTime = true;
        }
        sessionStartTime = globalSessionStartTime;

        if (logToConsole)
        {
            Debug.Log("[HandPositionLogger] Found Left Hand: " + leftHandTransform.name);
            Debug.Log("[HandPositionLogger] Found Right Hand: " + rightHandTransform.name);
        }

        if (logToCSV)
        {
            currentCycle = GameManager.GetHealingCycleCount();
            lastCycle = currentCycle;
            InitializeCSVFile();
        }

        if (logToConsole)
            Debug.Log("[HandPositionLogger] Initialized. Logging to: " + csvFilePath);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            hasGlobalSessionStartTime = false;
            globalSessionStartTime = 0f;
        }
    }

    private void Update()
    {
        // Roll over to a new CSV when the phase transitions into Interphase
        // after Telophase — NOT when healingCycleCount increments, because
        // CellDivided() increments the counter in the same frame as the
        // Telophase transition and would open a new file mid-Telophase.
        currentCycle = GameManager.GetHealingCycleCount();
        string currentPhase = GameManager.eGameStatus.ToString();
        bool enteringNewCycle = lastPhaseValue == "Telophase"
                             && currentPhase == "Interphase"
                             && currentCycle != lastCycle
                             && currentCycle < GameManager.MAX_HEALING_CYCLES;
        if (enteringNewCycle)
        {
            lastCycle = currentCycle;
            if (logToCSV)
            {
                InitializeCSVFile();
                if (logToConsole)
                    Debug.Log($"[HandPositionLogger] Started new cycle {currentCycle + 1}");
            }
        }
        lastPhaseValue = currentPhase;

        timeSinceLastLog += Time.deltaTime;
        if (timeSinceLastLog >= loggingInterval)
        {
            LogHandPositions();
            timeSinceLastLog = 0f;
        }
    }

    private void LogHandPositions()
    {
        if (leftHandTransform == null || rightHandTransform == null)
            return;

        float elapsed = Time.time - sessionStartTime;

        Vector3 leftPos = leftHandTransform.position;
        Vector3 rightPos = rightHandTransform.position;

        // Cache for CombinedLogger
        LastLeftPosition  = leftPos;
        LastRightPosition = rightPos;

        if (logToConsole)
            Debug.Log($"[Hand Position] T={elapsed:F2}s | Left:({leftPos.x:F3},{leftPos.y:F3},{leftPos.z:F3}) | Right:({rightPos.x:F3},{rightPos.y:F3},{rightPos.z:F3})");

        if (logToCSV)
            WriteToCSV(elapsed, leftPos, rightPos);
    }

    private void InitializeCSVFile()
    {
        try
        {
            string cycleNumber = (currentCycle + 1).ToString();
            csvFilePath = TrialLogPath.GetFilePath($"Hand_Position_Cycle{cycleNumber}.csv");
            
            string directory = Path.GetDirectoryName(csvFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = new StreamWriter(csvFilePath, false))
                writer.WriteLine("Time(s),LeftX,LeftY,LeftZ,RightX,RightY,RightZ");
        }
        catch (Exception ex)
        {
            Debug.LogError("[HandPositionLogger] Failed to initialize CSV: " + ex.Message);
        }
    }

    private void WriteToCSV(float elapsed, Vector3 leftPos, Vector3 rightPos)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, true))
                writer.WriteLine($"{elapsed:F3},{leftPos.x:F6},{leftPos.y:F6},{leftPos.z:F6},{rightPos.x:F6},{rightPos.y:F6},{rightPos.z:F6}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[HandPositionLogger] Failed to write to CSV: " + ex.Message);
        }
    }

    public void SetLoggingInterval(float interval)
    {
        loggingInterval = Mathf.Max(0.01f, interval);
    }

    public string GetCSVFilePath() => csvFilePath;

    private Transform FindHandByName(string handName)
    {
        Transform[] allTransforms = FindObjectsByType<Transform>();
        
        foreach (Transform t in allTransforms)
        {
            if (t.name == handName)
            {
                Debug.Log("[HandPositionLogger] Found exact match: " + t.name);
                return t;
            }
        }
        
        Debug.Log("[HandPositionLogger] No exact match for '" + handName + "'");
        return null;
    }
}