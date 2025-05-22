using UnityEngine;

public class Checkpoint6_1 : Checkpoint
{
    // nextCheckpoint_A is not directly used for progression here; RootManager handles CP6 completion.
    // However, we need a reference to Checkpoint6-2 to activate it.
    // Assign Checkpoint6-2 (rootManager.checkpoint6_2_Ref) via RootManager or a direct public field if preferred.

    private CarController _carReference; // To monitor IsFullyStopped

    void Update()
    {
        if (!isActive || _carReference == null) return;

        if (rootManager.cp6_CurrentInternalState == RootCheckpointManager.Checkpoint6InternalState.WaitingForStop)
        {
            if (_carReference.IsFullyStopped())
            {
                Debug.Log("Car has stopped for Checkpoint 6. Light turning yellow.");
                rootManager.SetCheckpoint6LightMaterial(true); // Turn light yellow
                rootManager.SetCheckpoint6State(RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_2_Front);
            }
        }
    }

    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;
        if (_carReference == null) _carReference = car;


        switch (rootManager.cp6_CurrentInternalState)
        {
            case RootCheckpointManager.Checkpoint6InternalState.Idle:
                if (wheelType == "FrontWheel")
                {
                    Debug.Log($"{name} (State: Idle) hit by FrontWheel. Activating 6-2 and waiting for stop.");
                    if (rootManager.checkpoint6_2_Ref != null)
                    {
                        rootManager.checkpoint6_2_Ref.ActivateCheckpoint(); // Activate companion checkpoint
                        rootManager.SetCheckpoint6State(RootCheckpointManager.Checkpoint6InternalState.WaitingForStop);
                        // This checkpoint (6-1) remains active.
                    }
                    else Debug.LogError($"{name}: Reference to Checkpoint6-2 (rootManager.checkpoint6_2_Ref) is missing!");
                }
                // No penalty for back wheel in Idle state, waits for front.
                break;

            case RootCheckpointManager.Checkpoint6InternalState.WaitingForStop:
                // Car must stop. If hit by BackWheel now, it's a fault.
                // If hit by FrontWheel again, it's not a fault per se but doesn't change state until stop.
                if (wheelType == "BackWheel")
                {
                    Debug.Log($"{name} (State: WaitingForStop) hit by BackWheel (Incorrect arrangement). Resetting.");
                    rootManager.HandleCrash();
                }
                break;

            case RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_1_Back:
                if (wheelType == "BackWheel")
                {
                    Debug.Log($"{name} (State: YellowLight_Need_6_1_Back) hit by BackWheel. Correct.");
                    rootManager.SetCheckpoint6State(RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_2_Back);
                }
                else if (wheelType == "FrontWheel") // Wrong wheel for this step
                {
                    Debug.Log($"{name} (State: YellowLight_Need_6_1_Back) hit by FrontWheel (Incorrect). Resetting.");
                    rootManager.HandleCrash();
                }
                break;

            case RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_2_Front:
            case RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_2_Back:
                // If 6-1 is hit during these states, it's an incorrect arrangement.
                Debug.Log($"{name} (State: {rootManager.cp6_CurrentInternalState}) hit by {wheelType} (Incorrect arrangement). Resetting.");
                rootManager.HandleCrash();
                break;
        }
    }
}