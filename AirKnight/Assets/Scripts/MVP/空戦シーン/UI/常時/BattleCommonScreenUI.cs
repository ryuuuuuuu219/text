using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class BattleCommonScreenUI : MonoBehaviour
{
    const float ReferenceWidth = 1024f;
    const float ReferenceHeight = 768f;
    const float MapWorldHalfSize = 14000f;
    const float MapIconSize = 10f;

    static readonly Rect MenuRect = new(0f, 0f, 80f, 80f);
    static readonly Rect ZoomRect = new(0f, 80f, 100f, 30f);
    static readonly Rect InfoButtonRect = new(944f, 0f, 80f, 80f);
    static readonly Rect InfoBoardRect = new(400f, 80f, 624f, 520f);
    static readonly Rect MiniMapRect = new(0f, 568f, 200f, 200f);
    static readonly Rect CarrierButtonRect = new(202f, 648f, 80f, 80f);
    static readonly Rect FlightListRect = new(310f, 608f, 200f, 160f);
    static readonly Rect AircraftListRect = new(510f, 608f, 200f, 160f);
    static readonly Rect CriterionStatusRect = new(720f, 600f, 120f, 50f);
    static readonly Rect CriterionButtonsRect = new(720f, 650f, 300f, 118f);

    [SerializeField] FixedBattleCameraController battleCamera;
    [SerializeField] TargetLineUIManager observationManager;
    [SerializeField] Transform carrier;
    [SerializeField] AircraftAffiliation playerAffiliation = AircraftAffiliation.A;

    readonly List<AircraftFlightAI> selectedFlight = new();
    Vector2 infoScroll;
    Vector2 flightScroll;
    Vector2 aircraftScroll;
    bool infoBoardOpen;
    bool pausedByThisUI;
    float timeScaleBeforePause = 1f;
    string lastOrderMessage = "指示なし";
    GUIStyle centeredStyle;
    GUIStyle smallStyle;

    public IReadOnlyList<AircraftFlightAI> SelectedFlight => selectedFlight;

    public void Configure(
        FixedBattleCameraController cameraController,
        TargetLineUIManager targetManager,
        Transform carrierTransform = null)
    {
        battleCamera = cameraController;
        observationManager = targetManager;
        carrier = carrierTransform;
    }

    void Awake()
    {
        if (battleCamera == null && Camera.main != null)
            battleCamera = Camera.main.GetComponent<FixedBattleCameraController>();
        if (observationManager == null)
            observationManager = FindAnyObjectByType<TargetLineUIManager>();
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame) return;
        battleCamera?.ToggleFirstPerson(observationManager != null
            ? observationManager.ObservationTarget
            : null);
    }

    void OnDestroy()
    {
        if (pausedByThisUI) Time.timeScale = timeScaleBeforePause;
    }

    public static bool IsPointerOverInterface(Vector2 screenPosition)
    {
        Vector2 point = new(
            screenPosition.x * ReferenceWidth / Mathf.Max(1f, Screen.width),
            (Screen.height - screenPosition.y) * ReferenceHeight / Mathf.Max(1f, Screen.height));
        return MenuRect.Contains(point)
            || ZoomRect.Contains(point)
            || InfoButtonRect.Contains(point)
            || MiniMapRect.Contains(point)
            || CarrierButtonRect.Contains(point)
            || FlightListRect.Contains(point)
            || AircraftListRect.Contains(point)
            || CriterionStatusRect.Contains(point)
            || CriterionButtonsRect.Contains(point)
            || InstanceHasOpenPanelAt(point);
    }

    static bool InstanceHasOpenPanelAt(Vector2 point)
    {
        BattleCommonScreenUI instance = FindAnyObjectByType<BattleCommonScreenUI>();
        if (instance == null) return false;
        return instance.infoBoardOpen && InfoBoardRect.Contains(point);
    }

    void OnGUI()
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(
            Screen.width / ReferenceWidth,
            Screen.height / ReferenceHeight,
            1f));
        EnsureStyles();

        DrawMenuButton();
        DrawInformationButton();
        if (infoBoardOpen) DrawInformationBoard();
        DrawMiniMap();
        DrawCarrierButton();
        DrawFlightList();
        DrawAircraftList();
        DrawCriterionControls();

        GUI.matrix = previousMatrix;
    }

    void DrawMenuButton()
    {
        DrawCircle(MenuRect, new Color(0f, 0f, 0f, 0.35f), Color.white);
        if (GUI.Button(MenuRect, pausedByThisUI ? "▶" : "≡", centeredStyle)) TogglePause();
    }

    void TogglePause()
    {
        if (pausedByThisUI)
        {
            Time.timeScale = timeScaleBeforePause;
            pausedByThisUI = false;
            return;
        }

        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        pausedByThisUI = true;
    }

    void DrawInformationButton()
    {
        DrawCircle(InfoButtonRect, new Color(0.05f, 0.35f, 0.9f, 0.9f), Color.white);
        if (GUI.Button(InfoButtonRect, "i", centeredStyle)) infoBoardOpen = !infoBoardOpen;
    }

    void DrawInformationBoard()
    {
        DrawPanel(InfoBoardRect, new Color(1f, 1f, 1f, 0.3f));
        Rect viewport = new(InfoBoardRect.x + 10f, InfoBoardRect.y + 10f,
            InfoBoardRect.width - 20f, InfoBoardRect.height - 20f);
        int aircraftCount = CountActiveAircraft();
        float contentHeight = Mathf.Max(viewport.height, 110f + aircraftCount * 24f);
        infoScroll = GUI.BeginScrollView(viewport, infoScroll,
            new Rect(0f, 0f, viewport.width - 10f, contentHeight));

        GUI.Label(new Rect(0f, 0f, viewport.width - 20f, 28f), "戦況情報", centeredStyle);
        GUI.Label(new Rect(0f, 32f, viewport.width - 20f, 22f), $"稼働機数: {aircraftCount}", smallStyle);
        string targetName = observationManager != null && observationManager.ObservationTarget != null
            ? observationManager.ObservationTarget.name
            : "なし";
        GUI.Label(new Rect(0f, 56f, viewport.width - 20f, 22f), $"観戦対象: {targetName}", smallStyle);
        GUI.Label(new Rect(0f, 80f, viewport.width - 20f, 22f), $"直近指示: {lastOrderMessage}", smallStyle);

        float y = 108f;
        IReadOnlyList<AircraftFlightAI> aircraft = AircraftFlightAI.ActiveAircraft;
        for (int i = 0; i < aircraft.Count; i++)
        {
            AircraftFlightAI item = aircraft[i];
            if (item == null) continue;
            GUI.Label(new Rect(0f, y, viewport.width - 20f, 22f),
                $"{item.name}  {item.affiliation}  {CriterionLabel(item.targetSelectionCriterion)}", smallStyle);
            y += 24f;
        }
        GUI.EndScrollView();
    }

    void DrawMiniMap()
    {
        DrawPanel(MiniMapRect, new Color(0f, 0f, 0f, 0.45f));
        DrawMapGrid();
        DrawMapIcon(Vector3.zero, Color.white, "⌂", null);

        IReadOnlyList<AircraftFlightAI> aircraft = AircraftFlightAI.ActiveAircraft;
        for (int i = 0; i < aircraft.Count; i++)
        {
            AircraftFlightAI item = aircraft[i];
            if (item == null) continue;
            Color color = item.affiliation == playerAffiliation ? Color.cyan : Color.red;
            if (observationManager != null && observationManager.ObservationTarget == item)
                color = Color.yellow;
            DrawMapIcon(item.transform.position, color, "●", item);
        }
    }

    void DrawMapGrid()
    {
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(new Rect(MiniMapRect.center.x, MiniMapRect.y, 1f, MiniMapRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(MiniMapRect.x, MiniMapRect.center.y, MiniMapRect.width, 1f), Texture2D.whiteTexture);
        GUI.color = old;
    }

    void DrawMapIcon(Vector3 worldPosition, Color color, string label, AircraftFlightAI aircraft)
    {
        Vector2 normalized = new(
            Mathf.InverseLerp(-MapWorldHalfSize, MapWorldHalfSize, worldPosition.x),
            Mathf.InverseLerp(-MapWorldHalfSize, MapWorldHalfSize, worldPosition.z));
        Vector2 point = new(
            Mathf.Lerp(MiniMapRect.x + 5f, MiniMapRect.xMax - 5f, normalized.x),
            Mathf.Lerp(MiniMapRect.yMax - 5f, MiniMapRect.y + 5f, normalized.y));
        Rect iconRect = new(point.x - MapIconSize, point.y - MapIconSize, MapIconSize * 2f, MapIconSize * 2f);
        Color old = GUI.contentColor;
        GUI.contentColor = color;
        if (GUI.Button(iconRect, label, centeredStyle))
        {
            battleCamera?.FocusWorldPoint(worldPosition);
            if (aircraft != null) observationManager?.SetSelectedAircraft(aircraft);
        }
        GUI.contentColor = old;
    }

    void DrawCarrierButton()
    {
        DrawPanel(CarrierButtonRect, new Color(0f, 0f, 0f, 0.35f));
        if (GUI.Button(CarrierButtonRect, "⌂", centeredStyle))
            battleCamera?.FocusWorldPoint(carrier != null ? carrier.position : Vector3.zero);
    }

    void DrawFlightList()
    {
        DrawPanel(FlightListRect, new Color(0f, 0f, 0f, 0.35f));
        List<AircraftFlightAI> playerAircraft = GetPlayerAircraft();
        Rect viewport = new(FlightListRect.x + 2f, FlightListRect.y + 2f,
            FlightListRect.width - 4f, FlightListRect.height - 4f);
        float contentHeight = viewport.height;
        flightScroll = GUI.BeginScrollView(viewport, flightScroll,
            new Rect(0f, 0f, viewport.width - 10f, contentHeight));

        if (playerAircraft.Count > 0)
        {
            bool selected = selectedFlight.Count > 0;
            Color old = GUI.color;
            if (selected) GUI.color = new Color(0.4f, 0.8f, 1f, 1f);
            if (GUI.Button(new Rect(0f, 0f, viewport.width - 10f, 30f),
                    $"{playerAffiliation} 飛行隊 ({playerAircraft.Count})", smallStyle))
                SelectFlight(playerAircraft);
            GUI.color = old;
        }
        GUI.EndScrollView();
    }

    void SelectFlight(List<AircraftFlightAI> aircraft)
    {
        selectedFlight.Clear();
        selectedFlight.AddRange(aircraft);
        aircraftScroll = Vector2.zero;
    }

    void DrawAircraftList()
    {
        DrawPanel(AircraftListRect, new Color(0f, 0f, 0f, 0.35f));
        Rect viewport = new(AircraftListRect.x + 2f, AircraftListRect.y + 2f,
            AircraftListRect.width - 4f, AircraftListRect.height - 4f);
        float contentHeight = Mathf.Max(viewport.height, selectedFlight.Count * 30f);
        aircraftScroll = GUI.BeginScrollView(viewport, aircraftScroll,
            new Rect(0f, 0f, viewport.width - 10f, contentHeight));

        for (int i = 0; i < selectedFlight.Count; i++)
        {
            AircraftFlightAI aircraft = selectedFlight[i];
            if (aircraft == null) continue;
            bool selected = observationManager != null
                && observationManager.ObservationTarget == aircraft;
            Color old = GUI.color;
            if (selected) GUI.color = new Color(1f, 0.85f, 0.25f, 1f);
            const float firstPersonButtonWidth = 34f;
            float rowWidth = viewport.width - 10f;
            if (GUI.Button(new Rect(0f, i * 30f, rowWidth - firstPersonButtonWidth, 30f),
                    aircraft.name, smallStyle))
            {
                observationManager?.SetSelectedAircraft(aircraft);
                battleCamera?.FocusWorldPoint(aircraft.transform.position);
            }
            bool firstPerson = battleCamera != null
                && battleCamera.FirstPersonTarget == aircraft;
            if (firstPerson) GUI.color = new Color(0.35f, 1f, 0.6f, 1f);
            if (GUI.Button(new Rect(
                    rowWidth - firstPersonButtonWidth,
                    i * 30f,
                    firstPersonButtonWidth,
                    30f), "FP", centeredStyle))
            {
                observationManager?.SetSelectedAircraft(aircraft);
                battleCamera?.ToggleFirstPerson(aircraft);
            }
            GUI.color = old;
        }
        GUI.EndScrollView();
    }

    void DrawCriterionControls()
    {
        DrawPanel(CriterionStatusRect, new Color(0f, 0f, 0f, 0.35f));
        GUI.Label(CriterionStatusRect, "目標基準：" + CurrentCriterionLabel(), smallStyle);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = selectedFlight.Count > 0;
        DrawPanel(CriterionButtonsRect, new Color(0f, 0f, 0f, 0.35f));
        AircraftTargetSelectionCriterion[] criteria =
        {
            AircraftTargetSelectionCriterion.Front,
            AircraftTargetSelectionCriterion.Nearest,
            AircraftTargetSelectionCriterion.Farthest,
            AircraftTargetSelectionCriterion.Counter,
            AircraftTargetSelectionCriterion.Returning
        };
        float buttonWidth = CriterionButtonsRect.width / criteria.Length;
        for (int i = 0; i < criteria.Length; i++)
        {
            AircraftTargetSelectionCriterion criterion = criteria[i];
            Rect buttonRect = new(
                CriterionButtonsRect.x + buttonWidth * i,
                CriterionButtonsRect.y,
                buttonWidth,
                CriterionButtonsRect.height);
            bool selected = selectedFlight.Count > 0
                && selectedFlight[0] != null
                && selectedFlight[0].targetSelectionCriterion == criterion;
            DrawPanel(buttonRect, selected
                ? new Color(0.2f, 0.65f, 1f, 0.45f)
                : new Color(0f, 0f, 0f, 0.2f));
            if (GUI.Button(buttonRect, CriterionLabel(criterion), smallStyle))
            {
                for (int j = 0; j < selectedFlight.Count; j++)
                    if (selectedFlight[j] != null) selectedFlight[j].SetTargetSelectionCriterion(criterion);
                lastOrderMessage = "目標基準: " + CriterionLabel(criterion);
            }
        }
        GUI.enabled = previousEnabled;
    }

    string CurrentCriterionLabel()
    {
        return selectedFlight.Count > 0 && selectedFlight[0] != null
            ? CriterionLabel(selectedFlight[0].targetSelectionCriterion)
            : "未選択";
    }

    static string CriterionLabel(AircraftTargetSelectionCriterion criterion)
    {
        return criterion switch
        {
            AircraftTargetSelectionCriterion.Nearest => "対近距離",
            AircraftTargetSelectionCriterion.Farthest => "対遠距離",
            AircraftTargetSelectionCriterion.Counter => "カウンター",
            AircraftTargetSelectionCriterion.Returning => "帰還中",
            _ => "対正面"
        };
    }

    List<AircraftFlightAI> GetPlayerAircraft()
    {
        List<AircraftFlightAI> result = new();
        IReadOnlyList<AircraftFlightAI> aircraft = AircraftFlightAI.ActiveAircraft;
        for (int i = 0; i < aircraft.Count; i++)
            if (aircraft[i] != null && aircraft[i].affiliation == playerAffiliation)
                result.Add(aircraft[i]);
        return result;
    }

    static int CountActiveAircraft()
    {
        int count = 0;
        IReadOnlyList<AircraftFlightAI> aircraft = AircraftFlightAI.ActiveAircraft;
        for (int i = 0; i < aircraft.Count; i++) if (aircraft[i] != null) count++;
        return count;
    }

    void EnsureStyles()
    {
        centeredStyle ??= new GUIStyle(GUIStyle.none)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        smallStyle ??= new GUIStyle(GUIStyle.none)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 12,
            wordWrap = true,
            padding = new RectOffset(4, 4, 2, 2)
        };
        centeredStyle.normal.textColor = Color.white;
        centeredStyle.hover.textColor = Color.white;
        centeredStyle.active.textColor = Color.white;
        smallStyle.normal.textColor = Color.white;
        smallStyle.hover.textColor = Color.white;
        smallStyle.active.textColor = Color.white;
    }

    static void DrawPanel(Rect rect, Color fill)
    {
        Color old = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        const float border = 2f;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - border, rect.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, border, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - border, rect.y, border, rect.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void DrawCircle(Rect rect, Color fill, Color border)
    {
        Color old = GUI.color;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        for (int y = 0; y < Mathf.CeilToInt(rect.height); y++)
        {
            float dy = y + rect.y - center.y;
            float halfWidth = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
            float edge = radius - Mathf.Abs(dy) < 2f ? 0f : 2f;
            GUI.color = border;
            GUI.DrawTexture(new Rect(center.x - halfWidth, rect.y + y, halfWidth * 2f, 1f), Texture2D.whiteTexture);
            if (halfWidth > edge * 2f)
            {
                GUI.color = fill;
                GUI.DrawTexture(new Rect(center.x - halfWidth + edge, rect.y + y,
                    halfWidth * 2f - edge * 2f, 1f), Texture2D.whiteTexture);
            }
        }
        GUI.color = old;
    }
}
