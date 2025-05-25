using UnityEngine;
using System.Collections;

public class Checkpoint5_2 : Checkpoint
{
    // In Inspector, assign Checkpoint6-1 to 'nextCheckpoint_A'
    public float timeLimitSeconds = 7.0f; // Adjustable time limit
    private Coroutine timerCoroutine;
    private bool timeHasExpired = false;

    public override void ActivateCheckpoint()
    {
        base.ActivateCheckpoint();
        timeHasExpired = false;
        if (timerCoroutine != null) // Stop any existing timer from a previous activation
        {
            StopCoroutine(timerCoroutine);
        }
        timerCoroutine = StartCoroutine(TimerRoutine());
        Debug.Log($"{name} activated. Timer started for {timeLimitSeconds} seconds.");
    }

    public override void DeactivateCheckpoint()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
        base.DeactivateCheckpoint();
    }

    IEnumerator TimerRoutine()
    {
        yield return new WaitForSeconds(timeLimitSeconds);
        if (isActive) // Only expire if still active (not yet passed or reset)
        {
            timeHasExpired = true;
            Debug.Log($"{name} timer EXPIRED. Any further correct pass will fail due to time.");
            // Optional: Could force a crash here if desired, but current logic is to check on collision.
        }
    }

    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (timeHasExpired)
        {
            Debug.Log($"{name} hit, but timer previously EXPIRED. Resetting.");
            rootManager.HandleCrash();
            return;
        }

        TrafficLight.LightState lightState = rootManager.GetTrafficLight5State();

        if (wheelType == "FrontWheel")
        {
            if (lightState != TrafficLight.LightState.Red)
            {
                Debug.Log($"{name} hit by FrontWheel on {lightState} (Not Red) within time. Progressing.");
                if (nextCheckpoint_A != null)
                {
                    rootManager.AdvanceToSegment(nextCheckpoint_A);
                }
                else Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 6-1) not assigned!");
            }
            else // Hit on Red light
            {
                Debug.Log($"{name} hit by FrontWheel on RED (Incorrect!). Resetting.");
                rootManager.HandleCrash();
            }
        }
        else if (wheelType == "BackWheel") // Hit by wrong wheel
        {
            Debug.Log($"{name} hit by BackWheel (Incorrect). Resetting.");
            rootManager.HandleCrash();
        }
    }
}