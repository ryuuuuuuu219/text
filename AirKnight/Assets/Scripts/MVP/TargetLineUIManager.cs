using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class TargetLineUIManager : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float lineWidth = 1f;
    LineRenderer line;
    AircraftFlightAI selectedAircraft;
    Material runtimeMaterial;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.enabled = false;

        if (line.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                runtimeMaterial = new Material(shader) { name = "TargetLine (Runtime)" };
                line.material = runtimeMaterial;
            }
        }
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }

    void OnValidate()
    {
        if (TryGetComponent(out LineRenderer renderer))
        {
            renderer.startWidth = lineWidth;
            renderer.endWidth = lineWidth;
        }
    }

    void LateUpdate()
    {
        if (selectedAircraft == null || selectedAircraft.CurrentTarget == null)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        Color color = selectedAircraft.affiliation == AircraftAffiliation.A ? Color.blue : Color.red;
        line.startColor = color;
        line.endColor = color;
        line.SetPosition(0, selectedAircraft.transform.position);
        line.SetPosition(1, selectedAircraft.CurrentTarget.transform.position);
    }

    public void SetSelectedAircraft(AircraftFlightAI aircraft)
    {
        selectedAircraft = aircraft;
    }

    public void ClearSelection()
    {
        selectedAircraft = null;
        if (line != null) line.enabled = false;
    }
}
