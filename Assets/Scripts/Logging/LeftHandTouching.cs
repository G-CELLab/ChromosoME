/// <summary>
/// Tracks which object the left hand is currently touching.
/// Attach to the left hand GameObject.
/// </summary>
public class LeftHandTouching : HandTouchDetector
{
    protected override void SetTouchObject(string name)
        => TouchTracker.SetLeftTouchObject(name);

    protected override void ClearTouchObject()
        => TouchTracker.ClearLeftTouchObject();
}