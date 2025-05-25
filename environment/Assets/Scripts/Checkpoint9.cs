using UnityEngine;

public class Checkpoint9 : Checkpoint
{
    // In the Inspector for the CP9_Trigger GameObject, assign your
    // CP1_Trigger GameObject to the 'Next Checkpoint A' slot.

    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        // This checkpoint can be simple: any wheel touch, or specify a wheel type.
        // Let's assume a front wheel touch for consistency with how other checkpoints are "passed".
        if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} (Checkpoint 9) touched by {wheelType}. Deactivating self and activating Checkpoint 1.");
            
            // Deactivate this checkpoint (Checkpoint 9)
            // DeactivateCheckpoint(); // AdvanceToSegment will handle deactivating this as current.

            // Activate Checkpoint 1
            if (nextCheckpoint_A != null) // nextCheckpoint_A should be checkpoint1_Ref
            {
                rootManager.AdvanceToSegment(nextCheckpoint_A);
            }
            else if (rootManager.checkpoint1_Ref != null) // Fallback if nextCheckpoint_A wasn't set
            {
                Debug.LogWarning($"{name}: nextCheckpoint_A not set, using rootManager.checkpoint1_Ref as fallback.");
                rootManager.AdvanceToSegment(rootManager.checkpoint1_Ref);
            }
            else
            {
                Debug.LogError($"{name}: Cannot activate Checkpoint 1. Neither nextCheckpoint_A nor rootManager.checkpoint1_Ref is set!");
            }
        }
        // If you want any wheel to trigger it, remove the "if (wheelType == "FrontWheel")" condition.
    }

    // No exit logic needed for this simple checkpoint
    // protected override void HandleExitLogic(string wheelType, CarController car) { }
}