using UnityEngine;

public class Checkpoint6_1 : Checkpoint
{
    public override void ActivateCheckpoint()
    {
        base.ActivateCheckpoint();
        // This method is called when the CP6 segment starts OR when looping back.
        // RootManager's ResetCheckpoint6Sequence or DefineSegmentStart will handle deactivating 6-0/6-2
        // and resetting light/timer states.
        Debug.Log($"{name} (CP6-1) is now active, waiting for FrontWheel.");
    }

    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (wheelType == "FrontWheel")
        {
            Debug.Log($"{name} (CP6-1) hit by FrontWheel. Activating CP6-0 & CP6-2. Deactivating self. Initiating stop check.");

            if (rootManager.checkpoint6_0_Ref != null)
                rootManager.checkpoint6_0_Ref.ActivateCheckpoint();
            else Debug.LogError($"{name}: checkpoint6_0_Ref is null in RootManager!");

            if (rootManager.checkpoint6_2_Ref != null)
                rootManager.checkpoint6_2_Ref.ActivateCheckpoint();
            else Debug.LogError($"{name}: checkpoint6_2_Ref is null in RootManager!");

            rootManager.StartCheckingForCP6StopCondition(car); // Notify RootManager
            DeactivateCheckpoint(); // Deactivate 6-1 itself
        }
        // No penalty for other wheel types, just waits for FrontWheel.
    }
}