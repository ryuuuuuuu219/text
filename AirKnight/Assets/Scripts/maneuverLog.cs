using UnityEngine;

public class maneuverLog : MonoBehaviour
{
    public float loginterval = 2f; // Time interval between log entries in seconds
    float timer = 0f;
    public string log;
    int i;

    AircraftController AircraftController;

    private void Start()
    {
        AircraftController = GetComponent<AircraftController>();
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        if(timer >= loginterval)
        {
            timer = 0f;
            i++;
            log += "\nNo." + i.ToString() +
                "\nposition: " + transform.position.ToString() + " rotation: " + transform.rotation.eulerAngles.ToString() +
                "\npitchpaformance: " + AircraftController.pitchPerformance.ToString() +
                "\npitch: " + AircraftController.pitchDeltaDegrees.ToString();
        }

    }
}
