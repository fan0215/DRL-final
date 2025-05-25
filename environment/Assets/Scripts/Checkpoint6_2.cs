using UnityEngine;

public class Checkpoint6_2 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;
        Debug.Log($"{name} (CP6-2) touched by {wheelType}. Light is UP: {rootManager.isCheckpoint6LightUp}");

        if (rootManager.isCheckpoint6LightUp)
        {
            // Light is UP: This is the PASS condition
            Debug.Log($"{name} (CP6-2) with Light UP - Touched. Checkpoint 6 PASSED.");
            rootManager.CompleteCheckpoint6AndAdvance();
        }
        else
        {
            // Light is NOT UP: Loop back to CP6-1
            Debug.Log($"{name} (CP6-2) with Light NOT UP - Touched. Looping back to CP6-1.");
            rootManager.ResetCheckpoint6Sequence();
        }
    }
}