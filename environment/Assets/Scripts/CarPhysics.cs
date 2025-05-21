using UnityEngine;

public class CarPhysics : MonoBehaviour
{
    public WheelCollider Wheel_RF;
    public WheelCollider Wheel_LF;
    public WheelCollider Wheel_RB;
    public WheelCollider Wheel_LB;

    public float torquePower = 1500f;
    public float maxSteeringAngle = 30f;
    public float brakeForce = 3000f;

    private float currentTorque = 0f;
    private float currentSteer = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // get input values
        float move = Input.GetAxis("Vertical");
        float steer = Input.GetAxis("Horizontal");
        bool isBraking = Input.GetKey(KeyCode.Space);

        // apply torque for back wheels
        currentTorque = move * torquePower;

        // apply steering to front wheels
        currentSteer = steer * maxSteeringAngle;

        // apply forces to wheels
        ApplyTorque();
        ApplySteering();
        ApplyBrakes(isBraking);
    }

    void ApplyTorque()
    {
        Wheel_LB.motorTorque = currentTorque;
        Wheel_RB.motorTorque = currentTorque;
    }

    void ApplySteering()
    {
        Wheel_LF.steerAngle = currentSteer;
        Wheel_RF.steerAngle = currentSteer;
    }

    void ApplyBrakes(bool isBraking)
    {
        float brake = isBraking ? brakeForce : 0f;
        Wheel_LB.brakeTorque = brake;
        Wheel_RB.brakeTorque = brake;
    }
}
