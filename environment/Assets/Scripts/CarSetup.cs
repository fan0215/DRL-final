using UnityEngine;

public class CarSetup : MonoBehaviour
{
    public Transform CarRoot;
    public Transform CarBody;
    public Transform Wheel_RF;
    public Transform Wheel_LF;
    public Transform Wheel_RB;
    public Transform Wheel_LB;
    public Transform Camera_front;
    public Transform Camera_right;
    public Transform Camera_left;

    public Vector3 test_start_position = new Vector3(-42.6f, 1.6f, 17.41f);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Root of the object
        CarRoot.position = test_start_position;
        CarRoot.rotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

        // Car Body
        CarBody.localPosition = Vector3.zero;
        CarBody.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);
        CarBody.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        // Right Front Wheel
        Wheel_RF.localPosition = new Vector3(1.25f, -1.1f, 1.5f);
        Wheel_RF.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
        Wheel_RF.localScale = new Vector3(0.8f, 0.2f, 0.8f);

        // Left Back Wheel
        Wheel_LF.localPosition = new Vector3(-1.25f, -1.1f, 1.5f);
        Wheel_LF.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
        Wheel_LF.localScale = new Vector3(0.8f, 0.2f, 0.8f);

        // Right Back Wheel
        Wheel_RB.localPosition = new Vector3(1.25f, -1.1f, -1.5f);
        Wheel_RB.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
        Wheel_RB.localScale = new Vector3(0.8f, 0.2f, 0.8f);

        // Left Back Wheel
        Wheel_LB.localPosition = new Vector3(-1.25f, -1.1f, -1.5f);
        Wheel_LB.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
        Wheel_LB.localScale = new Vector3(0.8f, 0.2f, 0.8f);

        // Front Camera
        Camera_front.localPosition = new Vector3(0.0f, 1.0f, 0.0f);
        Camera_front.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);
        Camera_front.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        // Right Camera
        Camera_right.localPosition = new Vector3(0.0f, 1.0f, 0.0f);
        Camera_right.localRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
        Camera_right.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        // Left Camera
        Camera_left.localPosition = new Vector3(0.0f, 1.0f, 0.0f);
        Camera_left.localRotation = Quaternion.Euler(0.0f, -90.0f, 0.0f);
        Camera_left.localScale = new Vector3(1.0f, 1.0f, 1.0f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
