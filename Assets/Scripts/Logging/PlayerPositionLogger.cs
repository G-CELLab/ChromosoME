using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Logs player head position and what the player is looking at via raycast.
/// 
/// Logs to console and Player_Position.csv with columns:
/// Time(s), XPos, YPos, ZPos, Raycast
///
/// Also caches the most recently computed values via public Last*
/// properties so CombinedLogger can merge them with the other logs.
/// </summary>
public class PlayerPositionLogger : MonoBehaviour
{
    [Header("Player Head Reference")]
    [SerializeField] private Transform playerHeadTransform;
    
    [Header("Raycast Settings")]
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private LayerMask raycastLayerMask = -1;
    
    [Header("Logging Settings")]
    [SerializeField] private float loggingInterval = 0.1f;
    [SerializeField] private bool logToConsole = false;
    [SerializeField] private bool logToCSV = true;

    // CSV and timing
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

    public Vector3 LastHeadPosition  { get; private set; }
    public string  LastRaycastTarget { get; private set; } = "";

    private void Start()
    {
        if (logToConsole)
            Debug.Log("[PlayerPositionLogger] START called");

        if (!hasGlobalSessionStartTime)
        {
            globalSessionStartTime = Time.time;
            hasGlobalSessionStartTime = true;
        }
        sessionStartTime = globalSessionStartTime;

        if (playerHeadTransform == null)
        {
            GameObject mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCameraObj != null)
            {
                playerHeadTransform = mainCameraObj.transform;
                if (logToConsole)
                    Debug.Log("[PlayerPositionLogger] Auto-found Main Camera");
            }
            else
            {
                Debug.LogError("[PlayerPositionLogger] Player Head Transform not assigned and Main Camera not found!");
                enabled = false;
                return;
            }
        }

        if (logToCSV)
        {
            currentCycle = GameManager.GetHealingCycleCount();
            lastCycle = currentCycle;
            InitializeCSVFile();
        }

        if (logToConsole)
            Debug.Log("[PlayerPositionLogger] Initialized. Logging to: " + csvFilePath);
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
                    Debug.Log($"[PlayerPositionLogger] Started new cycle {currentCycle + 1}");
            }
        }
        lastPhaseValue = currentPhase;

        timeSinceLastLog += Time.deltaTime;
        if (timeSinceLastLog >= loggingInterval)
        {
            LogPositionFrame();
            timeSinceLastLog = 0f;
        }
    }

    private void LogPositionFrame()
    {
        float elapsed = Time.time - sessionStartTime;
        
        Vector3 headPos = playerHeadTransform.position;
        string raycastHit = GetRaycastTarget();

        // Cache for CombinedLogger
        LastHeadPosition  = headPos;
        LastRaycastTarget = raycastHit;
        
        if (logToConsole)
            Debug.Log($"[PlayerPosLog] T={elapsed:F2}s | X:{headPos.x:F2} | Y:{headPos.y:F2} | Z:{headPos.z:F2} | Looking at:{raycastHit}");

        if (logToCSV)
            WriteToCSV(elapsed, headPos.x, headPos.y, headPos.z, raycastHit);
    }

    private string GetRaycastTarget()
    {
        Ray ray = new Ray(playerHeadTransform.position, playerHeadTransform.TransformDirection(Vector3.forward));
        
        if (Physics.Raycast(ray, out RaycastHit hitInfo, raycastDistance, raycastLayerMask))
        {
            Debug.DrawRay(playerHeadTransform.position, playerHeadTransform.TransformDirection(Vector3.forward) * hitInfo.distance, Color.yellow);
            return ColliderNameResolver.ResolveName(hitInfo.collider.transform);
        }
        else
        {
            Debug.DrawRay(playerHeadTransform.position, playerHeadTransform.TransformDirection(Vector3.forward) * raycastDistance, Color.cyan);
            return "";
        }
    }

    private void InitializeCSVFile()
    {
        try
        {
            string cycleNumber = (currentCycle + 1).ToString();
            csvFilePath = TrialLogPath.GetFilePath($"Player_Position_Cycle{cycleNumber}.csv");
            
            string directory = Path.GetDirectoryName(csvFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = new StreamWriter(csvFilePath, false))
                writer.WriteLine("Time(s),XPos,YPos,ZPos,Raycast");
        }
        catch (Exception ex)
        {
            Debug.LogError("[PlayerPositionLogger] Failed to initialize CSV: " + ex.Message);
        }
    }

    private void WriteToCSV(float elapsed, float xPos, float yPos, float zPos, string raycastHit)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, true))
                writer.WriteLine($"{elapsed:F3},{xPos:F3},{yPos:F3},{zPos:F3},{raycastHit}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[PlayerPositionLogger] Failed to write to CSV: " + ex.Message);
        }
    }

    public string GetCSVFilePath() => csvFilePath;
}