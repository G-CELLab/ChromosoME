/// <summary>
/// Live snapshot of the VR scene handed to the AI on every turn.
/// Owns all AI-facing content — phase context, objectives, visible objects,
/// healing progress, and proactive Telophase messages.
/// Call Refresh() before each query to pull current state from GameManager.
/// </summary>
[System.Serializable]
public class TutorSceneState
{
    // ── Fields sent to the AI every turn ──────────────────────────────────────

    public string Phase            { get; private set; } = "intro";
    public string SceneSummary     { get; private set; } = "";
    public string CurrentObjective { get; private set; } = "";
    public string VisibleObjects   { get; private set; } = "";
    public string HealingProgress  { get; private set; } = "";

    // ── Sync from GameManager ─────────────────────────────────────────────────

    /// <summary>
    /// Pulls all fields from GameManager's current state.
    /// Called by AITutor at the start of every query and on phase transitions.
    /// </summary>
    public void Refresh()
    {
        int cycle    = GameManager.GetHealingCycleCount();
        int maxCycle = GameManager.MAX_HEALING_CYCLES;
        int current  = cycle + 1; // cycles completed = cycles done, current = next one

        HealingProgress = GameManager.IsWoundHealed()
            ? "All 3 healing cycles complete. The wound is fully healed."
            : $"Currently on healing cycle {current} of {maxCycle}. " +
            $"{cycle} cycle(s) completed so far, {maxCycle - cycle} remaining.";

        switch (GameManager.eGameStatus)
        {
            case GameManager.GameState.Intro:
                Phase            = "intro";
                SceneSummary     = "The learner sees a wounded hand and must touch it to begin.";
                CurrentObjective = "Touch and hold the wound for 3 seconds to enter the cell.";
                VisibleObjects   = "Wounded hand (interactive).";
                break;

            case GameManager.GameState.Interphase:
                Phase            = "interphase";
                SceneSummary     = "Inside the cell. The cell is preparing energy for division.";
                CurrentObjective = "Place the three nutrient capsules into the mitochondria to fill the ATP bar.";
                VisibleObjects   = "Three green nutrient capsules (Protein, Magnesium, Vitamin C); Mitochondria; Green ATP bar.";
                break;

            case GameManager.GameState.InterphasePart2:
                Phase            = "interphase";
                SceneSummary     = "ATP bar is full. Now the centriole must be duplicated.";
                CurrentObjective = "Grab the yellow centriole and move it to duplicate it.";
                VisibleObjects   = "Yellow barrel-shaped centriole; ATP bar (full).";
                break;

            case GameManager.GameState.Prophase:
                Phase            = "prophase";
                SceneSummary     = "The cell is preparing to divide. DNA must condense into chromosomes.";
                CurrentObjective = "Hold the red DNA steady for 3 seconds so it condenses into an X-shaped chromosome.";
                VisibleObjects   = "Red stringy DNA; Blue X-shaped chromosomes (already condensed examples).";
                break;

            case GameManager.GameState.Metaphase:
                Phase            = "metaphase";
                SceneSummary     = "Chromosomes must align at the center of the cell.";
                CurrentObjective = "Move the red chromosome to the glowing yellow spot at the center line.";
                VisibleObjects   = "Red X-shaped chromosome (misaligned); Blue chromosomes (aligned); Glowing yellow placement spot; Spindle fibers.";
                break;

            case GameManager.GameState.Anaphase:
                Phase            = "anaphase";
                SceneSummary     = "Chromosomes must be pulled apart to opposite ends of the cell.";
                CurrentObjective = "Pull the red chromosome apart and place each half on the glowing yellow markers at opposite ends.";
                VisibleObjects   = "Red X-shaped chromosome; Glowing yellow markers at each end; Blue chromatids (examples); Spindle fibers.";
                break;

            case GameManager.GameState.Telophase:
                Phase            = "telophase";
                SceneSummary     = "Cell division is completing. Two new nuclei are forming.";
                CurrentObjective = "Watch the cell divide, then exit and touch the wound again.";
                VisibleObjects   = "Two groups of chromatids at opposite poles; Reforming nuclear envelopes; Wounded hand (outside cell).";
                break;

            case GameManager.GameState.GameOver:
                Phase            = "complete";
                SceneSummary     = "All healing cycles complete. The wound is fully healed.";
                CurrentObjective = "Touch the healed hand to finish the simulation.";
                VisibleObjects   = "Healed hand.";
                break;

            default:
                Phase            = "unknown";
                SceneSummary     = "";
                CurrentObjective = "";
                VisibleObjects   = "";
                break;
        }
    }

    // ── Proactive Telophase messages ──────────────────────────────────────────

    /// <summary>
    /// Returns the proactive spoken message for Telophase based on
    /// how many healing cycles have been completed.
    /// Called by GameManager — no AI strings live there.
    /// </summary>
    public string GetTelophaseMessage()
    {
        switch (GameManager.GetHealingCycleCount())
        {
            case 0:
                return "Great work! You've completed the first round of cell division. " +
                       "The wound isn't fully healed yet though — in real tissue, many cells must " +
                       "divide multiple times to fully repair the damage. Touch the wound again to continue.";
            case 1:
                return "Excellent! Two rounds of cell division complete. The wound is healing nicely, " +
                       "but we need one more cycle to fully restore the tissue. Let's finish this!";
            case 2:
                return "Congratulations! You've completed all three rounds of mitosis and fully healed the wound. " +
                       "You've just seen how cells work together to repair tissue. " +
                       "Touch the healed hand to complete the simulation.";
            default:
                return "";
        }
    }

    // ── Context string for AI prompt ──────────────────────────────────────────

    /// <summary>
    /// Formats all scene fields into a single context block
    /// ready to inject into the AI user message.
    /// </summary>
    public string ToContextString()
    {
        return
            $"Phase: {Phase}\n" +
            $"Scene: {SceneSummary}\n" +
            $"Current objective: {CurrentObjective}\n" +
            $"Visible objects: {VisibleObjects}\n" +
            $"Healing progress: {HealingProgress}";
    }

    public override string ToString() =>
        $"TutorSceneState(phase={Phase}, healing={HealingProgress})";
}