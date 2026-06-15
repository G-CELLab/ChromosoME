using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Combines the per-frame output of MainLogger, HandPositionLogger, and
/// PlayerPositionLogger into a single CombinedLog_CycleN.csv per healing cycle,
/// in addition to (not instead of) each logger's own individual CSV.
///
/// Runs in LateUpdate() on the same interval as the other loggers. LateUpdate
/// is guaranteed to run after every script's Update() each frame, so by the
/// time this fires, MainLogger / HandPositionLogger / PlayerPositionLogger
/// have already computed and cached their values for this frame via their
/// public Last* properties — no values are re-derived or double-consumed.
///
/// Header order:
/// Timestamp,Time(s),Left_Gesture,Right_Gesture,Left_Touch,Right_Touch,Phase,
/// User_Speech,AI_Speech,AI_Gesture,Other,
/// LeftX,LeftY,LeftZ,RightX,RightY,RightZ,
/// XPos,YPos,ZPos,Raycast
///
/// Timestamp is the wall-clock Unix epoch (whole seconds, UTC) at the moment
/// the row was written, for cross-referencing against other recordings.
/// </summary>
public class CombinedLogger : MonoBehaviour
{
    [Header("Source Loggers")]
    [SerializeField] private MainLogger mainLogger;
    [SerializeField] private HandPositionLogger handPositionLogger;
    [SerializeField] private PlayerPositionLogger playerPositionLogger;

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

    private void Start()
    {
        if (logToConsole)
            Debug.Log("[CombinedLogger] START called");

        if (!hasGlobalSessionStartTime)
        {
            globalSessionStartTime = Time.time;
            hasGlobalSessionStartTime = true;
        }
        sessionStartTime = globalSessionStartTime;

        if (mainLogger == null)           mainLogger           = FindAnyObjectByType<MainLogger>();
        if (handPositionLogger == null)   handPositionLogger   = FindAnyObjectByType<HandPositionLogger>();
        if (playerPositionLogger == null) playerPositionLogger = FindAnyObjectByType<PlayerPositionLogger>();

        if (mainLogger == null)
        {
            Debug.LogError("[CombinedLogger] MainLogger not found!");
            enabled = false;
            return;
        }
        if (handPositionLogger == null)
        {
            Debug.LogError("[CombinedLogger] HandPositionLogger not found!");
            enabled = false;
            return;
        }
        if (playerPositionLogger == null)
        {
            Debug.LogError("[CombinedLogger] PlayerPositionLogger not found!");
            enabled = false;
            return;
        }

        if (logToCSV)
        {
            currentCycle = GameManager.GetHealingCycleCount();
            lastCycle = currentCycle;
            InitializeCSVFile();
        }

        if (logToConsole)
            Debug.Log("[CombinedLogger] Initialized. Logging to: " + csvFilePath);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            hasGlobalSessionStartTime = false;
            globalSessionStartTime = 0f;
        }
    }

    // LateUpdate so MainLogger / HandPositionLogger / PlayerPositionLogger have
    // already run their Update() and cached this frame's values.
    private void LateUpdate()
    {
        currentCycle = GameManager.GetHealingCycleCount();
        if (currentCycle != lastCycle && currentCycle < GameManager.MAX_HEALING_CYCLES)
        {
            lastCycle = currentCycle;
            if (logToCSV)
            {
                InitializeCSVFile();
                if (logToConsole)
                    Debug.Log($"[CombinedLogger] Started new cycle {currentCycle + 1}");
            }
        }

        timeSinceLastLog += Time.deltaTime;
        if (timeSinceLastLog >= loggingInterval)
        {
            LogCombinedRow();
            timeSinceLastLog = 0f;
        }
    }

    private void LogCombinedRow()
    {
        float elapsed = Time.time - sessionStartTime;
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Vector3 leftHand  = handPositionLogger.LastLeftPosition;
        Vector3 rightHand = handPositionLogger.LastRightPosition;
        Vector3 headPos   = playerPositionLogger.LastHeadPosition;
        string  raycast   = playerPositionLogger.LastRaycastTarget;

        string leftGesture  = mainLogger.LastLeftGesture;
        string rightGesture = mainLogger.LastRightGesture;
        string leftTouch    = mainLogger.LastLeftTouch;
        string rightTouch   = mainLogger.LastRightTouch;
        string phase        = mainLogger.LastPhase;
        string userSpeech   = mainLogger.LastUserSpeech;
        string aiSpeech     = mainLogger.LastAISpeech;
        string aiGesture    = mainLogger.LastAIGesture;
        string other        = mainLogger.LastOther;

        if (logToConsole)
            Debug.Log($"[CombinedLog] T={elapsed:F2}s | TS={timestamp} | Phase:{phase} | L_Touch:{leftTouch} | R_Touch:{rightTouch}");

        if (logToCSV)
        {
            WriteToCSV(timestamp, elapsed,
                leftGesture, rightGesture, leftTouch, rightTouch, phase,
                userSpeech, aiSpeech, aiGesture, other,
                leftHand, rightHand, headPos, raycast);
        }
    }

    private void InitializeCSVFile()
    {
        try
        {
            string cycleNumber = (currentCycle + 1).ToString();
            csvFilePath = TrialLogPath.GetFilePath($"CombinedLog_Cycle{cycleNumber}.csv");

            string directory = Path.GetDirectoryName(csvFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = new StreamWriter(csvFilePath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(
                    "Timestamp,Time(s),Left_Gesture,Right_Gesture,Left_Touch,Right_Touch,Phase," +
                    "User_Speech,AI_Speech,AI_Gesture,Other," +
                    "LeftX,LeftY,LeftZ,RightX,RightY,RightZ," +
                    "XPos,YPos,ZPos,Raycast");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[CombinedLogger] Failed to initialize CSV: " + ex.Message);
        }
    }

    private void WriteToCSV(long timestamp, float elapsed,
        string leftGesture, string rightGesture, string leftTouch, string rightTouch,
        string phase, string userSpeech, string aiSpeech, string aiGesture, string other,
        Vector3 leftHand, Vector3 rightHand, Vector3 headPos, string raycast)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, true, new UTF8Encoding(true)))
            {
                writer.WriteLine(
                    $"{timestamp}," +
                    $"{elapsed:F3}," +
                    $"{EscapeCsvField(leftGesture)}," +
                    $"{EscapeCsvField(rightGesture)}," +
                    $"{EscapeCsvField(leftTouch)}," +
                    $"{EscapeCsvField(rightTouch)}," +
                    $"{EscapeCsvField(phase)}," +
                    $"{EscapeCsvField(userSpeech)}," +
                    $"{EscapeCsvField(aiSpeech)}," +
                    $"{EscapeCsvField(aiGesture)}," +
                    $"{EscapeCsvField(other)}," +
                    $"{leftHand.x:F6},{leftHand.y:F6},{leftHand.z:F6}," +
                    $"{rightHand.x:F6},{rightHand.y:F6},{rightHand.z:F6}," +
                    $"{headPos.x:F3},{headPos.y:F3},{headPos.z:F3}," +
                    $"{EscapeCsvField(raycast)}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[CombinedLogger] Failed to write to CSV: " + ex.Message);
        }
    }

    private string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    public string GetCSVFilePath() => csvFilePath;
}