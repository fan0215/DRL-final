using UnityEngine;

public class Checkpoint8_2 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (wheelType == "FrontWheel")
        {
            if (rootManager.IsLevelCrossingLight8Active()) // Check if the light is STILL SHINING
            {
                Debug.Log($"{name} (CP8-2) touched by FrontWheel, but Level Crossing Light is STILL ACTIVE. Looping back to CP8-1.");
                rootManager.HandleCrash();
                rootManager.LoopBackToCheckpoint8_1(); // Tell RootManager to handle the loop back
                                                      // This will deactivate CP8-2 and activate CP8-1.
            }
            else // Light is OFF - This is the correct PASS condition
            {
                Debug.Log($"{name} (CP8-2) passed by FrontWheel. Level crossing light is OFF. Activating Checkpoint 1.");
                
                // Determine the reference to Checkpoint 1
                Checkpoint checkpoint1ToActivate = nextCheckpoint_A;
                if (checkpoint1ToActivate == null)
                {
                    checkpoint1ToActivate = rootManager.checkpoint1_Ref; // Fallback to manager's reference
                    if (checkpoint1ToActivate != null)
                        Debug.LogWarning($"{name}: 'Next Checkpoint A' was not set. Using RootManager's 'checkpoint1_Ref' as fallback to activate Checkpoint 1.");
                }

                if (checkpoint1ToActivate != null)
                {
                    rootManager.AdvanceToSegment(checkpoint1ToActivate); // This will deactivate current (CP8-2)
                }
                else
                {
                    Debug.LogError($"{name}: Cannot activate Checkpoint 1. Neither 'Next Checkpoint A' nor 'rootManager.checkpoint1_Ref' is set!");
                }
            }
        }
    }
}