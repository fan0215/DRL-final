using UnityEngine;

public class Checkpoint3_1 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        // Only trigger if this part hasn't been hit correctly yet in this segment attempt
        if (!rootManager.checkpoint3_1_hitByCorrectWheel)
        {
            if (wheelType == "FrontWheel")
            {
                Debug.Log($"{name} correctly hit by FrontWheel.");
                rootManager.ReportCheckpoint3PartHit(true, true);
            }
        }
    }
}