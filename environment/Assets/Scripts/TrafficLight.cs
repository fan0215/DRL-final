using UnityEngine;
using System.Collections; // Required for Coroutines

public class TrafficLight : MonoBehaviour
{
    public enum LightState { Green, Yellow, Red }
    public LightState currentLightState = LightState.Green;

    [Header("Light GameObjects")]
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;

    [Header("Light Materials")]
    public Material redOnMaterial;    // Assign the bright red material here
    public Material yellowOnMaterial; // Assign the bright yellow material here
    public Material greenOnMaterial;  // Assign the bright green material here
    public Material lightOffMaterial; // Assign the dark "off" material here

    [Header("Durations")]
    public float redDuration = 10f;
    public float yellowDuration = 5f;
    public float greenDuration = 15f;

    // Private references to the Renderers of the spheres
    private Renderer redRenderer;
    private Renderer yellowRenderer;
    private Renderer greenRenderer;

    // Use Awake for initialization of references
    void Awake()
    {
        // Get the Renderer components from the spheres
        if (redLight != null) redRenderer = redLight.GetComponent<Renderer>();
        if (yellowLight != null) yellowRenderer = yellowLight.GetComponent<Renderer>();
        if (greenLight != null) greenRenderer = greenLight.GetComponent<Renderer>();

        // Optional: Log warnings if any renderers couldn't be found
        if (redRenderer == null) Debug.LogWarning("RedLight GameObject or its Renderer not assigned/found on TrafficLight script.", this);
        if (yellowRenderer == null) Debug.LogWarning("YellowLight GameObject or its Renderer not assigned/found on TrafficLight script.", this);
        if (greenRenderer == null) Debug.LogWarning("GreenLight GameObject or its Renderer not assigned/found on TrafficLight script.", this);
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
        // Initialize the light state at the beginning
        SetLightState(LightState.Green); // Start with green light

        // Automated cycling logic
        while (true)
        {
            yield return new WaitForSeconds(greenDuration);
            SetLightState(LightState.Yellow);
            yield return new WaitForSeconds(yellowDuration);
            SetLightState(LightState.Red);
            yield return new WaitForSeconds(redDuration);
            SetLightState(LightState.Green);
        }
    }

    // Method to set the visual state of the traffic lights
    public void SetLightState(LightState newState)
    {
        currentLightState = newState;
        Debug.Log($"Traffic Light changed to: {currentLightState}");

        // Turn all lights off first
        if (redRenderer != null) redRenderer.material = lightOffMaterial;
        if (yellowRenderer != null) yellowRenderer.material = lightOffMaterial;
        if (greenRenderer != null) greenRenderer.material = lightOffMaterial;

        // Turn on the material for the current state
        switch (newState)
        {
            case LightState.Green:
                if (greenRenderer != null) greenRenderer.material = greenOnMaterial;
                break;
            case LightState.Yellow:
                if (yellowRenderer != null) yellowRenderer.material = yellowOnMaterial;
                break;
            case LightState.Red:
                if (redRenderer != null) redRenderer.material = redOnMaterial;
                break;
        }
    }
}