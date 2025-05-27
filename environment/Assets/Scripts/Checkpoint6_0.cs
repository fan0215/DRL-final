using UnityEngine;

public class Checkpoint6_0 : Checkpoint
{
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        Debug.Log($"{name} (CP6-0) touched by {wheelType}. Light is UP: {rootManager.isCheckpoint6LightUp}. Looping back to CP6-1.");
        rootManager.ResetCheckpoint6Sequence(); // RootManager handles state reset and light
    }
}