using UnityEngine;

public class Checkpoint5_1 : Checkpoint
{
    // In Inspector, assign Checkpoint5-2 to 'nextCheckpoint_A'
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        TrafficLight.LightState lightState = rootManager.GetTrafficLight5State();

        if (wheelType == "FrontWheel")
        {
            if (lightState == TrafficLight.LightState.Green)
            {
                Debug.Log($"{name} hit by FrontWheel on GREEN. Progressing.");
                if (nextCheckpoint_A != null)
                {
                    rootManager.AdvanceToSegment(nextCheckpoint_A);
                }
                else Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 5-2) not assigned!");
            }
            else
            {
                Debug.Log($"{name} hit by FrontWheel on {lightState} (Incorrect! Expected Green). Resetting.");
                rootManager.HandleCrash();
            }
        }
        else if (wheelType == "BackWheel")
        {
            Debug.Log($"{name} hit by BackWheel (Incorrect). Resetting.");
            rootManager.HandleCrash();
        }
    }
}