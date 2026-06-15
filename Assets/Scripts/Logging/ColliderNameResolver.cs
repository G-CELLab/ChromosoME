using UnityEngine;

/// <summary>
/// Resolves a human-readable name for a collider/transform — used to turn
/// generic placeholder GameObject names (e.g. "Collider", "Trigger") into the
/// name of the actual object being touched or looked at (e.g. "Wound",
/// "RedChromatidL").
///
/// Shared by HandTouchDetector (Left_Touch / Right_Touch columns) and
/// PlayerPositionLogger (Raycast column) so both report consistent names.
/// </summary>
public static class ColliderNameResolver
{
    // Generic placeholder names that don't describe what's actually being
    // touched/looked at. When a transform's name matches one of these, we
    // walk up to its parent and try again instead of returning it.
    private static readonly string[] GenericNameFragments =
    {
        "collider", "trigger", "hitbox", "hit box"
    };

    /// <summary>
    /// Walks up from <paramref name="start"/>, checking for known nutrient
    /// names first, then skipping past generic placeholder names until it
    /// finds a transform whose name actually describes the object
    /// (e.g. "Wound", "RedChromatidL", "BlueDNA1").
    /// Returns the original transform's name if nothing better is found.
    /// </summary>
    public static string ResolveName(Transform start)
    {
        if (start == null) return "";

        Transform current = start;
        while (current != null)
        {
            string lower = current.name.ToLowerInvariant();

            if (lower.Contains("protein"))   return "Protein";
            if (lower.Contains("magnesium")) return "Magnesium";
            if (lower.Contains("vitamin"))   return "VitaminC";

            if (!IsGenericName(lower))
                return current.name;

            current = current.parent;
        }

        return start.name;
    }

    private static bool IsGenericName(string lower)
    {
        foreach (var fragment in GenericNameFragments)
            if (lower.Contains(fragment)) return true;
        return false;
    }
}