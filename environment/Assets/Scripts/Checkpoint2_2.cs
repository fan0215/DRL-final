using UnityEngine;

public class Checkpoint2_2 : Checkpoint
{
    // In Inspector:
    // Assign Checkpoint3-1 object to 'nextCheckpoint_A'
    // Assign Checkpoint3-2 object to 'nextCheckpoint_B'
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (wheelType == "BackWheel")
        {
            Debug.Log($"{name} successfully passed by BackWheel.");
            if (nextCheckpoint_A != null && nextCheckpoint_B != null) // Both must be assigned for CP3
            {
                rootManager.AdvanceToSegment(nextCheckpoint_A, nextCheckpoint_B);
            }
            else
            {
                Debug.LogError($"{name}: nextCheckpoint_A (for CP3-1) or nextCheckpoint_B (for CP3-2) is not assigned!");
            }
        }
        else if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} hit by FrontWheel (Incorrect). Resetting.");
            rootManager.HandleCrash();
        }
    }
}