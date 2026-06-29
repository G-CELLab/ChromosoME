using UnityEngine;

/// <summary>
/// Helper class for logging special events to the MainLogger.
/// Other scripts can call these methods when specific events occur.
///
/// Other column format:
///   Action:Dropped:ObjectName:Destination  — user placement events
///   System:VariableName:State              — system/game state events
/// </summary>
public class LogEventHelper : MonoBehaviour
{
    public static void LogATPCharged()
    {
        MainLogger.LogOtherEvent("System:ATP:Charged");
        Debug.Log("[LogEventHelper] ATP charged");
    }

    public static void LogCustomEvent(string eventName)
    {
        MainLogger.LogOtherEvent(eventName);
        Debug.Log("[LogEventHelper] Event logged: " + eventName);
    }
}