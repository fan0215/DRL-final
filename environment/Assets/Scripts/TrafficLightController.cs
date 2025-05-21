using UnityEngine;
using System.Collections; // Needed for Coroutines

public class TrafficLightController : MonoBehaviour
{
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;

    public Material redOnMaterial;    // Assign the bright red material here
    public Material yellowOnMaterial; // Assign the bright yellow material here
    public Material greenOnMaterial;  // Assign the bright green material here
    public Material lightOffMaterial; // Assign the dark "off" material here

    public float redDuration = 5f;    // How long red stays on
    public float yellowDuration = 2f; // How long yellow stays on
    public float greenDuration = 5f;  // How long green stays on

    // Private references to the Renderers of the spheres
    private Renderer redRenderer;
    private Renderer yellowRenderer;
    private Renderer greenRenderer;

    void Start()
    {
        // Get the Renderer components from the spheres
        redRenderer = redLight.GetComponent<Renderer>();
        yellowRenderer = yellowLight.GetComponent<Renderer>();
        greenRenderer = greenLight.GetComponent<Renderer>();

        // Start the traffic light sequence
        StartCoroutine(TrafficLightSequence());
    }

    IEnumerator TrafficLightSequence()
    {
        while (true) // Loop forever
        {
            // RED Light ON
            SetLight(redRenderer, redOnMaterial);
            SetLight(yellowRenderer, lightOffMaterial);
            SetLight(greenRenderer, lightOffMaterial);
            yield return new WaitForSeconds(redDuration);

            // GREEN Light ON
            SetLight(redRenderer, lightOffMaterial);
            SetLight(yellowRenderer, lightOffMaterial);
            SetLight(greenRenderer, greenOnMaterial);
            yield return new WaitForSeconds(greenDuration);

            // YELLOW Light ON
            SetLight(redRenderer, lightOffMaterial);
            SetLight(yellowRenderer, yellowOnMaterial);
            SetLight(greenRenderer, lightOffMaterial);
            yield return new WaitForSeconds(yellowDuration);
        }
    }

    // Helper function to switch the material of a light
    void SetLight(Renderer lightRenderer, Material targetMaterial)
    {
        if (lightRenderer != null && targetMaterial != null)
        {
            lightRenderer.material = targetMaterial;
        }
    }
}