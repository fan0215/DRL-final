using UnityEngine;

public class Checkpoint3_2 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!rootManager.checkpoint3_2_hitByCorrectWheel)
        {
            if (wheelType == "BackWheel")
            {
                Debug.Log($"{name} correctly hit by BackWheel.");
                rootManager.ReportCheckpoint3PartHit(false, true); // isPart1 = false, byCorrectWheel = true
            }
            // No penalty for front wheel.
        }
    }
}