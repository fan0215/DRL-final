using UnityEngine;

public class Checkpoint4_6 : Checkpoint
{
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
                Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 4-7) is not assigned!");
            }
        }
        else if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} hit by FrontWheel (Incorrect). Resetting.");
            rootManager.HandleCrash();
        }
    }
}