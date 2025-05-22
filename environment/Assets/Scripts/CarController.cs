using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("References")]
    public RootCheckpointManager rootCheckpointManager; // Assign in Inspector
    public Rigidbody rb; 

    [Header("Wheel Colliders (Assign from Hierarchy)")]
    public WheelCollider wheelBackLeft;
    public WheelCollider wheelBackRight;
    public WheelCollider wheelFrontLeft;
    public WheelCollider wheelFrontRight;

    [Header("Wheel Transforms (For Visuals - Optional)")]
    public Transform wheelFL_Transform;
    public Transform wheelFR_Transform;
    public Transform wheelBL_Transform;
    public Transform wheelBR_Transform;
    // IMPORTANT: Set this in Inspector to Quaternion.Euler(0, 0, 90) for your cylinder wheels
    public Quaternion wheelVisualsRotationOffset = Quaternion.Euler(0, 0, 90);

    [Header("Car Driving Parameters (Logic from your CarPhysics script)")]
    [Tooltip("How strong acceleration is. Corresponds to 'torquePower'.")]
    public float motorForce = 1500f;
    [Tooltip("Steering angle for front wheels.")]
    public float maxSteeringAngle = 30f;
    [Tooltip("Force applied to rear wheels when braking (Spacebar). Corresponds to 'brakeForce'.")]
    public float activeBrakeForce = 3000f;
    // Note: idleBrakeForce is intentionally omitted from driving logic to match your CarPhysics.cs

    [Header("Car State Properties (for Checkpoint System)")]
    [Tooltip("Velocity magnitude below which the car is considered stopped.")]
    public float stopVelocityThreshold = 0.1f;
    // CenterOfMassOffset and its application are removed as per your request.

    // Private variables to store calculated torque and steer, mirroring CarPhysics.cs
    private float currentCalculatedTorque = 0f;
    private float currentCalculatedSteerAngle = 0f;
    // isActiveBraking (from input) will be used directly

    public float torquePower = 1500f; // How strong acceleration is
    public float brakeForce = 3000f; // Force applied when braking

    private float currentTorque = 0f;
    private float currentSteer = 0f;

    void Update()
    {
        // Get input values
        float move = Input.GetAxis("Vertical"); // Up/Down arrow or W/S
        float steer = Input.GetAxis("Horizontal"); // Left/Right arrow or A/D
        bool isBraking = Input.GetKey(KeyCode.Space); // Spacebar for brakes

        // Apply torque to back wheels
        currentTorque = move * torquePower;

        // Apply steering to front wheels
        currentSteer = steer * maxSteeringAngle;

        // Apply forces to wheels
        ApplyTorque();
        ApplySteering();
        ApplyBrakes(isBraking);
    }

    void ApplyTorque()
    {
        wheelBackLeft.motorTorque = currentTorque;
        wheelBackRight.motorTorque = currentTorque;
    }

    void ApplySteering()
    {
        wheelFrontLeft.steerAngle = currentSteer;
        wheelFrontRight.steerAngle = currentSteer;
    }

    void ApplyBrakes(bool isBraking)
    {
        float brake = isBraking ? brakeForce : 0f;
        wheelBackLeft.brakeTorque = brake;
        wheelBackRight.brakeTorque = brake;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Edge"))
        {
            Debug.Log("Car collided with an Edge.");
            if (rootCheckpointManager != null) rootCheckpointManager.HandleCrash();
            else Debug.LogError("RootCheckpointManager not found by CarController for crash handling.");
        }
    }

    public bool IsFullyStopped()
    {
        if (rb == null) return true;
        bool wheelsPhysicallyStopped = true;
        WheelCollider[] currentActiveWheels = { wheelFrontLeft, wheelFrontRight, wheelBackLeft, wheelBackRight };
        foreach (WheelCollider wc_check in currentActiveWheels) // Renamed wc to wc_check to avoid conflict with UpdateSingleWheel parameter if copy-pasted into same scope by mistake
        {
            if (wc_check != null) wheelsPhysicallyStopped &= (Mathf.Abs(wc_check.rpm) < 5);
            else { wheelsPhysicallyStopped = false; break; }
        }
        return rb.linearVelocity.magnitude < stopVelocityThreshold &&
               rb.angularVelocity.magnitude < stopVelocityThreshold &&
               wheelsPhysicallyStopped;
    }

    public void ResetState(Vector3 position, Quaternion rotation)
    {
        // transform.position = position;
        // transform.rotation = rotation;
        // if (rb != null)
        // {
        //     rb.linearVelocity = Vector3.zero;
        //     rb.angularVelocity = Vector3.zero;
        // }
        // WheelCollider[] currentActiveWheels = { wheelFrontLeft, wheelFrontRight, wheelBackLeft, wheelBackRight };
        // foreach (WheelCollider wc_reset in currentActiveWheels) // Renamed wc to wc_reset
        // {
        //     if (wc_reset != null)
        //     {
        //         wc_reset.motorTorque = 0;
        //         // On reset, apply activeBrakeForce to all wheels to ensure it stops quickly
        //         wc_reset.brakeTorque = activeBrakeForce; 
        //         wc_reset.steerAngle = 0;
        //     }
        // }
        // currentMotorInput = 0f;
        // currentSteerInput = 0f;
        // isActiveBraking = true; // Set to true to reflect brakes being applied during reset
    }
}