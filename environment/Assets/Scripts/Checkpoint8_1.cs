using UnityEngine;

public class Checkpoint8_1 : Checkpoint
{
    // In Inspector, assign Checkpoint8-2 to 'nextCheckpoint_A'
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} successfully passed by FrontWheel. Activating Level Crossing Light and Checkpoint 8-2.");
            rootManager.ActivateLevelCrossingLight8(true);

            if (nextCheckpoint_A != null)
            {
                rootManager.AdvanceToSegment(nextCheckpoint_A);
            }
            else
            {
                Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 8-2) is not assigned!");
            }
        }
    }
}