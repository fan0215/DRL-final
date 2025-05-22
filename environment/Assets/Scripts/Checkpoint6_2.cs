using UnityEngine;

public class Checkpoint6_2 : Checkpoint
{
    // No 'nextCheckpoint_A' directly used here. RootManager handles CP6 completion.
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        switch (rootManager.cp6_CurrentInternalState)
        {
            case RootCheckpointManager.Checkpoint6InternalState.Idle:
                // Should not be possible if 6-1 activates 6-2 correctly.
                // If it happens, it's an error in sequence.
                Debug.LogWarning($"{name} (State: Idle) hit by {wheelType}. This implies 6-1 didn't activate 6-2 properly or state is desynced. Resetting for safety.");
                rootManager.HandleCrash();
                break;

            case RootCheckpointManager.Checkpoint6InternalState.WaitingForStop:
                // If 6-2 is hit by any wheel before the car stops and light turns yellow, it's a fault.
                Debug.Log($"{name} (State: WaitingForStop) hit by {wheelType} (Car did not stop before light yellow / incorrect arrangement). Resetting.");
                rootManager.HandleCrash();
                break;

            case RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_2_Front:
                if (wheelType == "FrontWheel")
                {
                    Debug.Log($"{name} (State: YellowLight_Need_6_2_Front) hit by FrontWheel. Correct.");
                    rootManager.SetCheckpoint6State(RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_1_Back);
                }
                else if (wheelType == "BackWheel") // Wrong wheel for this step
                {
                    Debug.Log($"{name} (State: YellowLight_Need_6_2_Front) hit by BackWheel (Incorrect). Resetting.");
                    rootManager.HandleCrash();
                }
                break;

            case RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_2_Back:
                if (wheelType == "BackWheel")
                {
                    Debug.Log($"{name} (State: YellowLight_Need_6_2_Back) hit by BackWheel. CP6 Sequence Complete!");
                    rootManager.CompleteCheckpoint6Sequence(); // This will deactivate 6-1, 6-2 and advance to 7-1
                }
                else if (wheelType == "FrontWheel") // Wrong wheel for this step
                {
                    Debug.Log($"{name} (State: YellowLight_Need_6_2_Back) hit by FrontWheel (Incorrect). Resetting.");
                    rootManager.HandleCrash();
                }
                break;

            case RootCheckpointManager.Checkpoint6InternalState.YellowLight_Need_6_1_Back:
                 // If 6-2 is hit during this state, it's an incorrect arrangement.
                Debug.Log($"{name} (State: YellowLight_Need_6_1_Back) hit by {wheelType} (Incorrect arrangement). Resetting.");
                rootManager.HandleCrash();
                break;
        }
    }
}