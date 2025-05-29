using UnityEngine;

public class Checkpoint3_0 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        Debug.Log($"Collision.");
        if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} successfully passed by FrontWheel.");
            if (nextCheckpoint_A != null && nextCheckpoint_B != null)
            {
                rootManager.AdvanceToSegment(nextCheckpoint_A, nextCheckpoint_B);
            }
            else
            {
                Debug.LogError($"{name}: nextCheckpoint_A (for CP3-1) or nextCheckpoint_B (for CP3-2) is not assigned!");
            }
        }
    }
}