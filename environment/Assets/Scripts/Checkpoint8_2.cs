using UnityEngine;

public class Checkpoint8_2 : Checkpoint
{
    // In the Inspector for your CP8_2_Trigger GameObject:
    // - 'Next Checkpoint A' should be assigned your CP1_Trigger GameObject (or use rootManager.checkpoint1_Ref as fallback).
    // - 'Spawn Point Index' should be set if crashing while CP8-2 is active resets you to CP8-2's start.
    //   However, with this new loop-back logic, if CP8-2 is touched early, you go to CP8-1.
    //   If CP8-2 is the start of a segment (after CP8-1 deactivates), then it needs a spawn point index.

    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        // Typically, "passing" a checkpoint like this is done with the front wheel.
        if (wheelType == "FrontWheel")
        {
            if (rootManager.IsLevelCrossingLight8Active()) // Check if the light is STILL SHINING
            {
                Debug.Log($"{name} (CP8-2) touched by FrontWheel, but Level Crossing Light is STILL ACTIVE. Looping back to CP8-1.");
                rootManager.LoopBackToCheckpoint8_1(); // Tell RootManager to handle the loop back
                                                      // This will deactivate CP8-2 and activate CP8-1.
            }
            else // Light is OFF - This is the correct PASS condition
            {
                Debug.Log($"{name} (CP8-2) passed by FrontWheel. Level crossing light is OFF. Activating Checkpoint 1.");
                
                // Determine the reference to Checkpoint 1
                Checkpoint checkpoint1ToActivate = nextCheckpoint_A; // Prefer Inspector-assigned
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
        // If touched by other wheel types, you might choose to ignore or handle differently.
        // For this rule, the significant interaction is with the "passing" wheel type.
    }
}