using UnityEngine;

public class Checkpoint7_1 : Checkpoint
{
    // In Inspector, assign Checkpoint7-2 to 'nextCheckpoint_A'
    private bool carStoppedPrematurely = false;
    private CarController _carReference; // To monitor IsFullyStopped

    public override void ActivateCheckpoint()
    {
        base.ActivateCheckpoint();
        carStoppedPrematurely = false;
        _carReference = null; // Reset car reference on activation
    }

    // Use Update to check if car stops while this checkpoint is active
    void Update()
    {
        if (isActive && !carStoppedPrematurely && _carReference != null)
        {
            if (_carReference.IsFullyStopped())
            {
                Debug.Log($"{name}: Car detected as stopped PREMATURELY while active and waiting for back wheel exit.");
                carStoppedPrematurely = true;
                // The actual crash due to this premature stop will be triggered if the back wheel eventually exits,
                // or if the rules imply an immediate crash (which would need HandleCrash() here).
                // Based on "If the car stopped before 7-1 touches backwheel" - this is if it stops while active
                // *before* the backwheel successfully *exits*.
                // Let HandleExitLogic check this flag.
            }
        }
    }
    
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        // Store car reference when it first enters, primarily for the Update stop check.
        if (_carReference == null)
        {
            _carReference = car;
        }
        // This checkpoint's main logic is on exit.
    }

    protected override void HandleExitLogic(string wheelType, CarController car)
    {
        if (!isActive) return;

        if (wheelType == "BackWheel")
        {
            if (carStoppedPrematurely)
            {
                Debug.Log($"{name}: BackWheel exited, but car had stopped prematurely. Resetting.");
                rootManager.HandleCrash();
            }
            else
            {
                Debug.Log($"{name}: BackWheel successfully exited. Progressing.");
                if (nextCheckpoint_A != null)
                {
                    rootManager.AdvanceToSegment(nextCheckpoint_A);
                }
                else Debug.LogError($"{name}: nextCheckpoint_A (for Checkpoint 7-2) is not assigned!");
            }
        }
    }
}