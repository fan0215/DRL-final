using UnityEngine;

public class Checkpoint7_2 : Checkpoint
{
    // In Inspector, assign Checkpoint8-1 to 'nextCheckpoint_A'
    private bool carHasSuccessfullyStopped = false;
    private CarController _carReference;

    public override void ActivateCheckpoint()
    {
        base.ActivateCheckpoint();
        carHasSuccessfullyStopped = false;
        _carReference = null;
        Debug.Log($"{name} activated. Waiting for car to enter and then stop fully.");
    }

    // Use Update to check if car (that has entered) stops.
    void Update()
    {
        if (isActive && !carHasSuccessfullyStopped && _carReference != null)
        {
            if (_carReference.IsFullyStopped())
            {
                Debug.Log($"{name}: Car has now successfully stopped inside the trigger area.");
                carHasSuccessfullyStopped = true;
            }
        }
    }

    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (_carReference == null) // First contact with any wheel
        {
            _carReference = car;
            Debug.Log($"{name}: Car has entered trigger. Now waiting for it to stop before FrontWheel pass.");
        }

        if (wheelType == "FrontWheel")
        {
            if (carHasSuccessfullyStopped)
            {
                Debug.Log($"{name} hit by FrontWheel AFTER stopping. Progressing.");
                if (nextCheckpoint_A != null)
                {
                    rootManager.AdvanceToSegment(nextCheckpoint_A);
                }
                else Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 8-1) is not assigned!");
            }
            else
            {
                // Car hit with FrontWheel but hadn't stopped yet (or hadn't even entered with another part to register _carReference and then stop).
                Debug.Log($"{name} hit by FrontWheel, but car had NOT successfully stopped yet. Resetting.");
                rootManager.HandleCrash();
            }
        }
        // No specific penalty for back wheel, but it won't progress.
        // The logic relies on car stopping THEN front wheel.
    }
}