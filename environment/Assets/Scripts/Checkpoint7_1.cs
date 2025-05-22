using UnityEngine;

public class Checkpoint7_1 : Checkpoint
{
    // Assign Checkpoint7-2 to 'nextCheckpoint_A' in Inspector
    private bool carStoppedWhileActive = false; // To detect if car stops BEFORE back wheel exits
    private CarController carRef;

    public override void ActivateCheckpoint()
    {
        base.ActivateCheckpoint();
        carStoppedWhileActive = false;
        carRef = null;
        Debug.Log($"{name} (CP7-1) activated. Waiting for BackWheel to pass (exit).");
    }
    
    protected override void HandleCollisionLogic(string wheelType, CarController car)
    {
        if (carRef == null) carRef = car; // Get car reference on first contact
        // Main logic is on exit.
        // Rule: "If the car stopped before 7-1 touches backwheel" -
        // This is tricky if "touches" means enters. Current logic is "stops while 7-1 is active AND before backwheel exits".
    }

    protected override void HandleExitLogic(string wheelType, CarController car) // Called when a wheel exits
    {
        if (!isActive) return;

        if (wheelType == "BackWheel")
        {
            if (carStoppedWhileActive) // Checked by Update
            {
                Debug.Log($"{name} (CP7-1): BackWheel exited, but car had stopped prematurely while 7-1 was active. CRASH.");
                rootManager.HandleCrash(); // Crash as per original implicit rule
            }
            else
            {
                Debug.Log($"{name} (CP7-1): BackWheel successfully exited. Deactivating self, activating CP7-2, and initiating CP7-2 stop condition.");
                DeactivateCheckpoint(); 
                if (nextCheckpoint_A != null) // nextCheckpoint_A should be CP7-2
                {
                    nextCheckpoint_A.ActivateCheckpoint();
                    // NEW: Tell RootManager to start monitoring for CP7-2's stop condition
                    if (car != null) // Pass the car reference
                    {
                        rootManager.InitiateCheckpoint7_2StopCondition(car);
                    }
                    else if (carRef != null)
                    {
                         rootManager.InitiateCheckpoint7_2StopCondition(carRef);
                    }
                     else
                    {
                        Debug.LogError($"{name}: Cannot initiate CP7-2 stop condition, CarController reference is missing.");
                    }
                }
                else Debug.LogError($"{name}: nextCheckpoint_A (for CP7-2) is not assigned!");
            }
        }
    }

    void Update()
    {
        // Monitor if car stops while this checkpoint is active AND before back wheel has successfully exited.
        if (isActive && !carStoppedWhileActive && carRef != null)
        {
            if (carRef.IsFullyStopped())
            {
                Debug.Log($"{name} (CP7-1): Car detected as stopped while this checkpoint is active and awaiting back wheel exit. Marked as premature stop.");
                carStoppedWhileActive = true; // Flag this. Exit logic will determine if it's a crash.
            }
        }
    }
}