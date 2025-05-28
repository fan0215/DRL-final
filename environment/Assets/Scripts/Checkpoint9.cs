using UnityEngine;

public class Checkpoint9 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} (Checkpoint 9) touched by {wheelType}. Deactivating self and activating Checkpoint 1.");

            // Activate Checkpoint 1
            if (nextCheckpoint_A != null)
            {
                rootManager.AdvanceToSegment(nextCheckpoint_A);
            }
            else if (rootManager.checkpoint1_1_Ref != null) // Fallback if nextCheckpoint_A wasn't set
            {
                Debug.LogWarning($"{name}: nextCheckpoint_A not set, using rootManager.checkpoint1_1_Ref as fallback.");
                rootManager.AdvanceToSegment(rootManager.checkpoint1_1_Ref);
            }
            else
            {
                Debug.LogError($"{name}: Cannot activate Checkpoint 1. Neither nextCheckpoint_A nor rootManager.checkpoint1_1_Ref is set!");
            }
        }
    }
}