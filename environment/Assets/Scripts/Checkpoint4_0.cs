using UnityEngine;

public class Checkpoint4_0 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} successfully passed by FrontWheel.");
            if (nextCheckpoint_A != null)
            {
                rootManager.AdvanceToSegment(nextCheckpoint_A);
            }
            else
            {
                Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 4-1) is not assigned!");
            }
        }
        else if (wheelType == "BackWheel")
        {
            Debug.Log($"{name} hit by BackWheel (Incorrect). Resetting.");
            rootManager.HandleCrash();
        }
    }
}