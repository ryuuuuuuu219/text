using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class AircraftObjectSelector : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] TargetLineUIManager targetUIManager;
    [SerializeField] LayerMask selectableAircraftMask;
    [SerializeField, Min(1f)] float maxSelectionDistance = 5000f;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (BattleCommonScreenUI.IsPointerOverInterface(Mouse.current.position.ReadValue())) return;
        if (targetCamera == null || targetUIManager == null) return;

        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, maxSelectionDistance,
                selectableAircraftMask, QueryTriggerInteraction.Collide))
        {
            AircraftFlightAI aircraft = hit.collider.GetComponentInParent<AircraftFlightAI>();
            if (aircraft != null)
            {
                targetUIManager.SetSelectedAircraft(aircraft);
                return;
            }
        }

        targetUIManager.ClearSelection();
    }

    public void Configure(Camera cameraToUse, TargetLineUIManager manager, LayerMask mask)
    {
        targetCamera = cameraToUse;
        targetUIManager = manager;
        selectableAircraftMask = mask;
    }
}
