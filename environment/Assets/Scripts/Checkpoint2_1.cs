using UnityEngine;

public class Checkpoint2_1 : Checkpoint
{
    // In Inspector, assign Checkpoint2-2 object to 'nextCheckpoint_A'
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (wheelType == "BackWheel")
        {
            Debug.Log($"{name} successfully passed by BackWheel.");
            if (nextCheckpoint_A != null)
            {
                rootManager.AdvanceToSegment(nextCheckpoint_A);
            }
            else
            {
                Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 2-2) is not assigned!");
            }
        }
        else if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} hit by FrontWheel (Incorrect). Resetting.");
            rootManager.HandleCrash();
        }
    }
}