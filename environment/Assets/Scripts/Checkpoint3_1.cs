using UnityEngine;

public class Checkpoint3_1 : Checkpoint
{
    // No 'nextCheckpoint_A' here as RootManager handles CP3 completion logic.
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        // Only trigger if this part hasn't been hit correctly yet in this segment attempt
        if (!rootManager.checkpoint3_1_hitByCorrectWheel)
        {
            if (wheelType == "FrontWheel")
            {
                Debug.Log($"{name} correctly hit by FrontWheel.");
                rootManager.ReportCheckpoint3PartHit(true, true); // isPart1 = true, byCorrectWheel = true
                // This checkpoint can remain visually active or be made non-interactive by other means if desired,
                // but its logical state for completion is now with RootManager.
            }
            // No penalty for back wheel, just waits for front.
        }
    }
}