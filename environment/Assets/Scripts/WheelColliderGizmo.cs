using UnityEngine;

public class WheelColliderGizmo : MonoBehaviour
{
    void OnDrawGizmos()
    {
        // Set gizmo color to red
        Gizmos.color = Color.red;

        // Draw a wire sphere at the position of the wheel with the radius of the Wheel Collider
        WheelCollider wheelCollider = GetComponent<WheelCollider>();
        if (wheelCollider != null)
        {
            Gizmos.DrawWireSphere(transform.position, wheelCollider.radius);
        }
    }
}
