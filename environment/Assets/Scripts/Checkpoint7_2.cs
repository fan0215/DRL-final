using UnityEngine;

public class Checkpoint7_2 : Checkpoint
{
    // nextCheckpoint_A in inspector should be Checkpoint8-1

    public override void ActivateCheckpoint()
    {
        base.ActivateCheckpoint();
        // When 7-2 is activated, RootManager's InitiateCheckpoint7_2StopCondition
        // (called by 7-1) will set cp7_2_canBePassedAfterStop to false initially.
        // It will become true only after car stops for 1 second.
        Debug.Log($"{name} (CP7-2) activated. Waiting for car to stop for 1 sec before it can be passed.");
    }
    
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (wheelType == "FrontWheel") // Assuming FrontWheel is the trigger for pass/fail
        {
            if (rootManager.cp7_2_canBePassedAfterStop)
            {
                Debug.Log($"{name} (CP7-2) - PASSED by FrontWheel (car had stopped for 1s). Activating CP8-1.");
                DeactivateCheckpoint(); // Deactivate self
                
                // REMOVED: rootManager.ResetCP7_2State(); 
                // The state reset will happen as part of AdvanceToSegment -> DefineSegmentStart

                if (nextCheckpoint_A != null) // Should be CP8-1
                {
                    rootManager.AdvanceToSegment(nextCheckpoint_A);
                }
                else
                {
                    Debug.LogError($"{name}: nextCheckpoint_A (for CP8-1) is not assigned!");
                }
            }
            else // Touched by FrontWheel but cp7_2_canBePassedAfterStop is false
            {
                Debug.Log($"{name} (CP7-2) - Touched by FrontWheel TOO EARLY (stop condition not met). Looping back to CP7-1.");
                rootManager.LoopBackToCheckpoint7_1(); // This method will deactivate this checkpoint (7-2)
            }
        }
    }
}