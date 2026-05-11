/// <summary>
/// Tracks which object the right hand is currently touching.
/// Attach to the right hand GameObject.
/// </summary>
public class RightHandTouching : HandTouchDetector
{
    protected override void SetTouchObject(string name)
        => TouchTracker.SetRightTouchObject(name);

    protected override void ClearTouchObject()
        => TouchTracker.ClearRightTouchObject();
}