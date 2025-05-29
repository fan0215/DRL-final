using UnityEngine;

public class Checkpoint2_0 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        Debug.Log($"Collision.");
        if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} successfully passed by FrontWheel.");
            if (nextCheckpoint_A != null)
            {
                rootManager.AdvanceToSegment(nextCheckpoint_A);
            }
            else
            {
                Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 2-1) is not assigned!");
            }
        }
    }
}