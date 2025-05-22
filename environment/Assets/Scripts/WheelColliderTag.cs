using UnityEngine;

public class WheelColliderTag : MonoBehaviour
{
    public enum WheelCategory
    {
        FrontWheel,
        BackWheel
    }

    public WheelCategory wheelCategory; // Set this in the Inspector for each wheel
    public CarController carController; // Assign the main CarController here

    void Start()
    {
        // Attempt to find CarController on parent if not assigned
        if (carController == null)
        {
            carController = GetComponentInParent<CarController>();
        }

        if (carController == null)
        {
            Debug.LogError($"WheelColliderTag on {gameObject.name} could not find CarController. Please assign it.");
        }

        // Ensure this GameObject can trigger OnTriggerEnter on checkpoints.
        // This usually means the checkpoint is a trigger, and this GameObject (or its Rigidbody parent) can interact.
        // WheelColliders are colliders, so this should work with a parent Rigidbody.
        // No Rigidbody needed on this specific GameObject if the parent Car object has one.
    }
}