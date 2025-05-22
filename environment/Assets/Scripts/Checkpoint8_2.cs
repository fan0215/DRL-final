using UnityEngine;

public class Checkpoint8_2 : Checkpoint
{
    // For 'nextCheckpoint_A', you'll want to assign Checkpoint1 (rootManager.checkpoint1_Ref)
    // This can be done in Inspector if Checkpoint1 script is on a GameObject.
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (wheelType == "FrontWheel")
        {
            if (!rootManager.IsLevelCrossingLight8Active())
            {
                Debug.Log($"{name} passed by FrontWheel. Level crossing light is OFF. Progressing (Looping to CP1).");
                rootManager.ActivateLevelCrossingLight8(false); // Ensure it's explicitly off if it wasn't auto

                Checkpoint cp1Ref = rootManager.checkpoint1_Ref; // Use the specific ref from RootManager
                if (cp1Ref != null)
                {
                    rootManager.AdvanceToSegment(cp1Ref);
                }
                else
                {
                    Debug.LogError($"{name}: Reference to Checkpoint1 (rootManager.checkpoint1_Ref) is not assigned for loop back!");
                }
            }
            else
            {
                Debug.Log($"{name} hit by FrontWheel, but Level Crossing Light is STILL ACTIVE. Resetting.");
                rootManager.HandleCrash();
            }
        }
        // No penalty for back wheel.
    }
}