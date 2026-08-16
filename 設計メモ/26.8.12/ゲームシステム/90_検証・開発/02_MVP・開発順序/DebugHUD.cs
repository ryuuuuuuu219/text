using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using static WeaponSystem;
using System;
using System.Text;

public sealed class TargetHudContainer
{
    public GameObject obj;
    public RectTransform rect;
    public LineRenderer lr;
    public LineRenderer crossLrA;
    public LineRenderer crossLrB;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI tgtText;
    public Transform distanceTextTransform;
    public Transform tgtTextTransform;
    public GameObject boundTarget;
}

public static class TargetHudContainerPool
{
    static readonly Stack<TargetHudContainer> pool = new();
    const int HudSortingOrder = short.MaxValue;
    static Material lineMaterial;
    static Transform poolRoot;

    public static TargetHudContainer Get(Transform parent, Vector3[] vertexs)
    {
        TargetHudContainer container = null;
        while (pool.Count > 0 && container == null)
        {
            TargetHudContainer pooled = pool.Pop();
            if (IsUsable(pooled))
                container = pooled;
        }

        container ??= Create(vertexs);

        container.obj.transform.SetParent(parent, false);
        container.boundTarget = null;
        Reset(container);
        return container;
    }

    public static void Release(TargetHudContainer container)
    {
        if (!IsUsable(container)) return;

        Reset(container);
        container.boundTarget = null;
        container.obj.transform.SetParent(GetPoolRoot(), false);
        pool.Push(container);
    }

    public static void Reset(TargetHudContainer container)
    {
        if (!IsUsable(container)) return;

        if (container.distanceText != null)
            container.distanceText.text = "";
        if (container.tgtText != null)
            container.tgtText.text = "";

        SetLineEnabled(container.lr, false);
        SetLineEnabled(container.crossLrA, false);
        SetLineEnabled(container.crossLrB, false);

        if (container.obj != null && container.obj.activeSelf)
            container.obj.SetActive(false);
    }

    public static void Forget(TargetHudContainer container)
    {
        if (container == null) return;
        container.obj = null;
        container.rect = null;
        container.lr = null;
        container.crossLrA = null;
        container.crossLrB = null;
        container.distanceText = null;
        container.tgtText = null;
        container.distanceTextTransform = null;
        container.tgtTextTransform = null;
        container.boundTarget = null;
    }

    static TargetHudContainer Create(Vector3[] vertexs)
    {
        GameObject c = new GameObject("TargetInfo", typeof(RectTransform));
        c.tag = "HUDUI";
        c.layer = LayerMask.NameToLayer("UI");

        GameObject distanceTextObj = new GameObject("DistanceText");
        distanceTextObj.transform.SetParent(c.transform);
        distanceTextObj.layer = LayerMask.NameToLayer("UI");
        var rectT = distanceTextObj.AddComponent<RectTransform>();
        rectT.anchorMin = new Vector2(1f, 0.5f);
        rectT.anchorMax = new Vector2(1f, 0.5f);
        rectT.anchoredPosition = new Vector3(178f, 0f, 0);
        rectT.sizeDelta = new Vector2(400f, 150f);
        rectT.localPosition = new Vector3(rectT.localPosition.x, rectT.localPosition.y, 0);
        rectT.localScale = Vector3.one;

        var textobj = distanceTextObj.AddComponent<TextMeshProUGUI>();
        textobj.fontSize = 16;
        textobj.alignment = TextAlignmentOptions.Left;
        textobj.color = Color.green;

        GameObject tgtText = new GameObject("tgtText");
        tgtText.transform.SetParent(c.transform);
        tgtText.layer = LayerMask.NameToLayer("UI");
        var tgtRectT = tgtText.AddComponent<RectTransform>();
        tgtRectT.anchorMin = new Vector2(0, 0.5f);
        tgtRectT.anchorMax = new Vector2(0, 0.5f);
        tgtRectT.anchoredPosition = new Vector3(57f, 20f, 0);
        tgtRectT.sizeDelta = new Vector2(130f, 50f);
        tgtRectT.localPosition = new Vector3(tgtRectT.localPosition.x, tgtRectT.localPosition.y, 0);
        tgtRectT.localScale = Vector3.one;

        var tgttextobj = tgtText.AddComponent<TextMeshProUGUI>();
        tgttextobj.fontSize = 16;
        tgttextobj.alignment = TextAlignmentOptions.Left;
        tgttextobj.color = Color.red;

        LineRenderer renderer = CreateLine("TargetContainerline", true, Color.green, vertexs);
        LineRenderer crossRenderer = CreateLine("TargetContainerCrossline", false, Color.yellow, null);
        LineRenderer crossRendererB = CreateLine("TargetContainerCrosslineB", false, Color.yellow, null);

        return new TargetHudContainer
        {
            obj = c,
            rect = c.GetComponent<RectTransform>(),
            lr = renderer,
            crossLrA = crossRenderer,
            crossLrB = crossRendererB,
            distanceText = textobj,
            tgtText = tgttextobj,
            distanceTextTransform = distanceTextObj.transform,
            tgtTextTransform = tgtText.transform,
        };
    }

    static LineRenderer CreateLine(string name, bool loop, Color color, Vector3[] positions)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(null);
        LineRenderer renderer = lineObj.AddComponent<LineRenderer>();
        renderer.material = GetLineMaterial();
        renderer.sortingOrder = HudSortingOrder;
        renderer.startWidth = 0.3f;
        renderer.endWidth = 0.3f;
        renderer.enabled = false;
        renderer.loop = loop;
        renderer.positionCount = positions != null ? positions.Length : 2;
        if (positions != null)
            renderer.SetPositions(positions);
        renderer.startColor = color;
        renderer.endColor = color;
        return renderer;
    }

    static Material GetLineMaterial()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("HUD/AlwaysOnTop");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            lineMaterial = new Material(shader);
            lineMaterial.renderQueue = 5000;
        }
        return lineMaterial;
    }

    static Transform GetPoolRoot()
    {
        if (poolRoot != null)
            return poolRoot;

        GameObject root = new GameObject("TargetHudContainerPool");
        root.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(root);
        poolRoot = root.transform;
        return poolRoot;
    }

    public static void SetLineEnabled(LineRenderer renderer, bool enabled)
    {
        if (renderer == null) return;
        renderer.enabled = enabled;
        if (renderer.gameObject.activeSelf != enabled)
            renderer.gameObject.SetActive(enabled);
    }

    static bool IsUsable(TargetHudContainer container)
    {
        return container != null &&
            container.obj != null &&
            container.rect != null &&
            container.lr != null &&
            container.crossLrA != null &&
            container.crossLrB != null;
    }
}

public class DebugHUD : MonoBehaviour
{
    const int HudSortingOrder = short.MaxValue;
    [SerializeField] Camera hudCam;
    Camera mainCam;
    [Header("Canvas References")]
    [SerializeField] Canvas cameraCanvas;   // MainCamera配下（Screen Space - Camera）
    [SerializeField] Canvas overlayCanvas;  // HUD固定用（Screen Space - Overlay）
    RectTransform cameraCanvasRect;
    Camera cameraCanvasUiCamera;

    [Header("Player & HUD")]
    public GameObject plane;         // プレイヤー機のスクリプト参照
    public TextMeshProUGUI hudText;          // HUDテキスト
    public RectTransform velocityMarker;     // フライトパスベクトル（進行方向）
    public RectTransform noseMarker;         // ウィスキーピーク（機首方向）

    [Header("Target Settings")]
    public float detectRange = 3000f;        // 探索範囲
    public float lockRange = 850f;           // ロック範囲
    public float SlockRange = 850f;           // ロック範囲
    public float LlockRange = 850f;           // ロック範囲
    public float gunRange = 500f;            // 機銃射程
    public float maxfov = 60f;               // 視界

    [Header("Target Locator")]
    public RectTransform targetLocator;      // ターゲットロケーターUI
    public float edgeOffset = 50f;           // 画面端からのオフセット

    private WeaponSystem weapon;
    private Rigidbody rb;

    private List<GameObject> arrys;
    private List<GameObject> targets;       // 敵機リスト
    public int LockedFrame = 1;             // ロック可能数
    public int multiLockCount = 1;
    public List<GameObject> detecttargets;  //コンテナ表示ターゲット
    public List<GameObject> markingtargets; //ターゲット切り替え用配列
    public List<GameObject> Lockedtargets;  //ロック条件を満たすターゲット配列

    List<(GameObject target, float fov)> detectPairs;
    readonly StringBuilder hudBuilder = new StringBuilder(128);
    const float HudTextRefreshInterval = 0.1f;
    float nextHudTextRefreshTime;

    bool isBlinking;
    float blinkInterval = 0.4f;
    float blinkTimer = 0f;

    AircraftController ac; 

    AugumentStatus status;
    EnemyNameConverterToUI enemyNameConverter;

    List<TargetHudContainer> conteiners;
    public bool deceived;
    bool hadLockedTargets;
    float nextLockOnAudioTime;


    void Start()
    {
        ConfigureHudCanvas(cameraCanvas);
        ConfigureHudCanvas(overlayCanvas);

        if (hudText != null && overlayCanvas != null)
        {
            hudText.transform.SetParent(overlayCanvas.transform, false);
        }
        if (targetLocator != null && overlayCanvas != null)
        {
            targetLocator.SetParent(overlayCanvas.transform, false);
        }
        if (velocityMarker != null && overlayCanvas != null)
        {
            velocityMarker.SetParent(overlayCanvas.transform, false);
        }
        if (noseMarker != null && overlayCanvas != null)
        {
            noseMarker.SetParent(overlayCanvas.transform, false);
        }

        if (plane != null)
        {
            rb = plane.GetComponent<Rigidbody>();
            weapon = plane.GetComponent<WeaponSystem>();
            ac = plane.GetComponent<PlayerAircraft>();
        }

        mainCam = Camera.main;

        if (cameraCanvas != null && cameraCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            cameraCanvas.worldCamera = mainCam;
        }

        if (cameraCanvas != null)
        {
            cameraCanvasRect = cameraCanvas.GetComponent<RectTransform>();
            cameraCanvasUiCamera = cameraCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : cameraCanvas.worldCamera;
        }

        conteiners = new ();
        detecttargets = new ();
        markingtargets.Clear();
        Lockedtargets = new ();
        detectPairs = new ();

        status = plane.GetComponent<AugumentStatus>();
        enemyNameConverter = EnsureEnemyNameConverter();

        if (status.IsInitialized)
        {
            InitFromStatus();
        }
        else
        {
            status.OnInitialized += InitFromStatus;
        }
    }

    void ConfigureHudCanvas(Canvas canvas)
    {
        if (canvas == null) return;

        canvas.overrideSorting = true;
        canvas.sortingOrder = HudSortingOrder;
    }

    void InitFromStatus()
    {

        status.altGetVar("ミサイル：射程（ロック可能距離）", out SlockRange);
        status.altGetVar("長射程マルチロックミサイル：射程（ロック可能距離）", out LlockRange);
        status.altGetVar("長射程マルチロックミサイル：マルチロック数", out float lockCount);
        multiLockCount = Mathf.Max(1, Mathf.RoundToInt(lockCount));
    }

    void OnDestroy()
    {
        if (conteiners == null) return;

        for (int i = 0; i < conteiners.Count; i++)
        {
            TargetHudContainerPool.Forget(conteiners[i]);
        }

        conteiners.Clear();
    }

    void LateUpdate()
    {
        targets = ObjectManager.Instance.Enemies as List<GameObject>;
        arrys = ObjectManager.Instance.allies;

        // -------- 目標探索 --------
        detecttargets.Clear();

        bool stdm = weapon?.mode == WeaponMode.MSL;

        lockRange = stdm ? SlockRange : LlockRange;
        LockedFrame = stdm ? 1 : multiLockCount;

        if (targets.Count > 1)
        {
            detectPairs.Clear();

            foreach (var t in targets)
            {
                if (t == null) continue; 
                if (t.TryGetComponent(out AugumentStatus s))
                {
                    if (!s.isVisible) continue; //フラグを確認
                }
                float fov = ToTargetFov(t.transform.position);
                float sqrdist = (plane.transform.position - t.transform.position).sqrMagnitude;
                if (sqrdist > detectRange * detectRange) continue;
                detectPairs.Add((t, fov));
            }

            detectPairs.Sort((a, b) => a.fov.CompareTo(b.fov)); // 昇順

            detecttargets.Clear();
            for (int i = 0; i < detectPairs.Count; i++)
            {
                detecttargets.Add(detectPairs[i].target);
            }

            var input=InputManager.Instance;

            //ターゲット切り替えボタン押下時
            if (input.targetChange)
            {
                if (detecttargets.Count >= 1)
                {
                    if (markingtargets.Count >= 1)
                    {
                        if (markingtargets[0] == detecttargets[0])
                        {
                            //優先ロック中の目標を保持しつつマーキング配列を更新
                            if (detecttargets.Count >= 2)
                            {
                                GameObject target0 = markingtargets[0];
                                GameObject target1 = detecttargets[1];
                                markingtargets.Clear();
                                markingtargets.Add(target1);
                                markingtargets.Add(target0);
                                foreach (var t in detecttargets)
                                {
                                    if (!markingtargets.Contains(t))
                                    {
                                        markingtargets.Add(t);
                                    }
                                }
                            }
                            else
                            {
                                GameObject target0 = markingtargets[0];
                                markingtargets.Clear();
                                markingtargets.Add(target0);
                            }
                        }
                        else
                        {
                            GameObject target0 = markingtargets[0];
                            GameObject target1 = detecttargets[0];
                            markingtargets.Clear();
                            markingtargets.Add(target1);
                            markingtargets.Add(target0);
                            foreach (var t in detecttargets)
                            {
                                if (!markingtargets.Contains(t))
                                {
                                    markingtargets.Add(t);
                                }
                            }
                        }
                    }
                    else
                    {
                        markingtargets.Clear();
                        markingtargets.AddRange(detecttargets);
                    }
                }
            }
            else
            {
                if (markingtargets.Count >= 1)
                {
                    GameObject target0 = markingtargets[0];
                    markingtargets.Clear();
                    markingtargets.Add(target0);
                    foreach (var t in detecttargets)
                    {
                        if (!markingtargets.Contains(t))
                        {
                            markingtargets.Add(t);
                        }
                    }
                }
                else
                {
                    markingtargets.Clear();
                    markingtargets.AddRange(detecttargets);
                }
            }
            //優先目標消滅時
            if(markingtargets.Count == 0)
            {
                markingtargets.AddRange(detecttargets);
            }
            else if (markingtargets.Count > 0)
            {
                if (markingtargets[0] == null)
                {
                    markingtargets.Remove(markingtargets[0]);
                }
            }

            //優先目標までの距離が遠い場合はマーキング目標を更新
            if(markingtargets.Count > 0)
            {
                float sqrdist = (plane.transform.position - markingtargets[0].transform.position).sqrMagnitude;
                if (sqrdist > detectRange * detectRange)
                {
                    markingtargets.Clear();
                    markingtargets.AddRange(detecttargets);
                }
            }

            //マーキング目標が視野外なら配列から削除
            for (int i = markingtargets.Count - 1; i >= 1/*優先目標は除く*/; i--)
            {
                var t = markingtargets[i];
                float fov = ToTargetFov(t.transform.position);
                if (fov > maxfov)
                {
                    markingtargets.Remove(t);
                }
            }

            PromoteNonEcmTarget();
        }
        else if (targets.Count == 1)
        {
            markingtargets.Clear();
            markingtargets.Add(targets[0]);
        }
        else
        {
            markingtargets.Clear();
        }


        // ロック条件
        if (markingtargets.Count > 0)
        {
            if (markingtargets[0] == null)
            {
                markingtargets.Clear();
                Lockedtargets.Clear();
                hadLockedTargets = false;
                goto skip;

            }
            float sqrdist = (plane.transform.position - markingtargets[0].transform.position).sqrMagnitude;
            if (sqrdist < lockRange * lockRange &&
                ToTargetFov(markingtargets[0].transform.position) < maxfov &&
                !IsEcmTarget(markingtargets[0]))
            {
                Lockedtargets.Clear();
                for (int i = 0; i < markingtargets.Count; i++)
                {
                    if (markingtargets[i] == null) continue;
                    if (IsEcmTarget(markingtargets[i])) continue;

                    float sqrdisti = (plane.transform.position - markingtargets[i].transform.position).sqrMagnitude;
                    if (sqrdisti < lockRange * lockRange &&
                    ToTargetFov(markingtargets[i].transform.position) < maxfov)
                    {
                        if (!Lockedtargets.Contains(markingtargets[i]))
                        {
                            Lockedtargets.Add(markingtargets[i]);
                        }
                    }
                    else
                    {
                        if (Lockedtargets.Contains(markingtargets[i]))
                        {
                            Lockedtargets.Remove(markingtargets[i]);
                        }
                    }
                }
            }
            else
            {
                Lockedtargets.Clear();
            }

            //保険　マーキング目標配列とロック目標配列の同期
            if (Lockedtargets.Count > 0)
            {
                for (int i = Lockedtargets.Count - 1; i >= 0; i--)
                {
                    var t = Lockedtargets[i];
                    if (t == null) continue;
                    if (!markingtargets.Contains(t))
                    {
                        Lockedtargets.Remove(t);
                    }
                }

                if (Lockedtargets.Count > LockedFrame)
                {
                    Lockedtargets.RemoveRange(LockedFrame, Lockedtargets.Count - LockedFrame);
                }
            }

            deceived = Lockedtargets.Count == 0 &&
                markingtargets.Count > 0 &&
                IsEcmTarget(markingtargets[0]) &&
                sqrdist < lockRange * lockRange &&
                ToTargetFov(markingtargets[0].transform.position) < maxfov;

            // ロック解除条件
            if (Lockedtargets.Count == 0)
            {
            }
        }
        else if (markingtargets.Count == 0)
        {
            deceived = false;
            Lockedtargets.Clear();
        }

        bool hasLockedTargets = Lockedtargets.Count > 0;
        if (hasLockedTargets && Time.time >= nextLockOnAudioTime)
        {
            GeneratedAudioManager.Play(GeneratedAudioCue.LockOn, null, 0.75f);
            nextLockOnAudioTime = Time.time + 0.55f;
        }
        else if (!hasLockedTargets && hadLockedTargets)
        {
            nextLockOnAudioTime = 0f;
        }
        hadLockedTargets = hasLockedTargets;

        // -------- コンテナ更新 --------
        blinkTimer += Time.deltaTime;
        if (blinkTimer >= blinkInterval)
        {
            blinkTimer = 0f;
            isBlinking = !isBlinking;
        }
        UpdateContainers();

        // -------- HUD表示 --------
        if (Time.time >= nextHudTextRefreshTime)
        {
            nextHudTextRefreshTime = Time.time + HudTextRefreshInterval;
            UpdateHUD();
        }

        // -------- ターゲットロケーター --------
        UpdateTargetLocator();

    skip:

        // -------- フライトパスベクター --------
        if (velocityMarker != null && rb != null)
        {
            Vector3 worldPos = plane.transform.position + rb.linearVelocity.normalized * 100f;
            velocityMarker.position = mainCam.WorldToScreenPoint(worldPos);
        }

        // -------- 機首方向（ウィスキーピーク） --------
        if (noseMarker != null)
        {
            Vector3 noseWorld = plane.transform.position + plane.transform.forward * 100f;
            noseMarker.position = mainCam.WorldToScreenPoint(noseWorld);
        }
    }

    #region 座標系変換関数 

    float ToTargetFov(Vector3 worldPos)
    {
        if (rb == null) return -1f;

        Vector3 forward = rb.transform.forward;
        Vector3 dirToTarget = (worldPos - plane.transform.position).normalized;

        // 0〜180°の角度をそのまま返す
        float angle = Vector3.Angle(forward, dirToTarget);
        return angle; // ← 0なら正面、180なら真後ろ
    }

    void PromoteNonEcmTarget()
    {
        if (markingtargets.Count <= 1 || !IsEcmTarget(markingtargets[0]))
            return;

        for (int i = 1; i < markingtargets.Count; i++)
        {
            var candidate = markingtargets[i];
            if (candidate == null || IsEcmTarget(candidate))
                continue;

            markingtargets.RemoveAt(i);
            markingtargets.Insert(0, candidate);
            return;
        }
    }

    bool IsEcmTarget(GameObject target)
    {
        return target != null &&
            target.TryGetComponent(out AugumentStatus targetStatus) &&
            targetStatus.ECM;
    }

    float GetTargetAngle(Transform target, Camera cam, out bool isOutsideView)
    {
        if (target == null || cam == null)
        {
            isOutsideView = false;
            return 0f;
        }

        Vector3 toTarget = (target.position - cam.transform.position).normalized;
        Vector3 camForward = cam.transform.forward;

        // カメラ前方との角度（0° = 正面, 180° = 真後ろ）
        float angleFromCenter = Vector3.Angle(camForward, toTarget);

        // FOVの半分以内なら視野内、それ以外は視野外
        float halfFOV = cam.fieldOfView * 0.5f;
        isOutsideView = angleFromCenter >= halfFOV;

        return angleFromCenter;
    }

    #endregion
    #region コンテナ表示

    void UpdateTargetLocator()
    {
        if (markingtargets.Count == 0 || targetLocator == null || hudCam == null)
        {
            targetLocator?.gameObject.SetActive(false);
            return;
        }

        // 角度計算と視野判定を共通関数で取得
        float angleFromCenter = GetTargetAngle(markingtargets[0].transform, mainCam, out bool outsideView);

        // 視野内なら非表示
        if (!outsideView)
        {
            targetLocator.gameObject.SetActive(false);
            return;
        }

        // スクリーン座標
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        Vector3 screenPos = mainCam.WorldToScreenPoint(markingtargets[0].transform.position);

        // スクリーン中心からの方向
        Vector3 dir = (new Vector3(screenPos.x, screenPos.y, 0f) - screenCenter).normalized;

        if (screenPos.z < 0)
        {
            // 後方の場合は方向を反転
            dir = -dir;
        }

        // スクリーン端の位置
        Vector3 edgePos = screenCenter + dir * (Mathf.Min(Screen.width, Screen.height) / 2f - edgeOffset);

        // 角度に比例して細長く
        float stretch = 1f + (angleFromCenter / 90f);

        // 矢印を表示・変形
        targetLocator.gameObject.SetActive(true);
        targetLocator.position = edgePos;
        targetLocator.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        targetLocator.localScale = new Vector3(1f, stretch, 1f);
    }

    Vector3[] vertexs = new Vector3[]
    {
        new Vector3() { x = -1, y = -1, z = 0 },
        new Vector3() { x = 1, y = -1, z = 0 },
        new Vector3() { x = 1, y = 1, z = 0 },
        new Vector3() { x = -1, y = 1, z = 0 }
    };


    void UpdateContainers()
    {
        int needed = targets.Count + arrys.Count;

        if (needed == 0)
        {
            ClearAllContainers();
            return;
        }

        // 足りない分は生成
        while (conteiners.Count < needed)
        {
            conteiners.Add(TargetHudContainerPool.Get(cameraCanvas.transform, vertexs));
        }

        // 必要な分だけアクティブ化して位置更新
        int idx = 0;
        var conteninerobj = conteiners[0].obj;

        if(markingtargets.Count == 0)
        {
            HideAllContainers();
            return;
        }

        foreach (var obj in targets)
        {
            if (obj == null)
            {
                ClearContainer(idx);
                idx++;
                continue;
            }

            if (obj.TryGetComponent(out AugumentStatus s))
            {
                if (!s.isVisible)
                {
                    ClearContainer(idx);
                    idx++;
                    continue;
                }
            }

            var entry = conteiners[idx];

            if (entry.boundTarget != obj)
            {
                ClearContainer(idx);
                entry.boundTarget = obj;
            }


            conteninerobj = conteiners[idx].obj;
            bool isLocked = Lockedtargets.Contains(obj);
            int targetidx = -1;
            if (obj ==markingtargets[0])
            {
                targetidx = 0;
            }
            else if (markingtargets.Count > 1 && obj == markingtargets[1])
            {
                targetidx = 1;
            }

            int nextidx = markingtargets.Count > 0 ? 1 : 0;
            bool isNext = (targetidx == nextidx);

            if (obj == null)
            {
                ClearContainer(idx);
                continue;
            }

            if (targetidx == 0)
            {
                bool isEcmTarget = IsEcmTarget(obj);
                bool useCrossContainer = isEcmTarget || deceived;
                string hptext = "";
                if (obj.TryGetComponent(out AugumentStatus status) &&
                    status.TryGetHP(out float hp, out float max))
                {
                    hptext = $"HP:{hp:F0}/{max:F0}\n";
                }
                else
                {
                    Debug.LogError("DebugHUD: Target AugumentStatus or HP not found.");
                }
                UpdateContainer(idx, obj,
                        isEcmTarget ? Color.green : deceived ? Color.yellow : isLocked ? Color.red : Color.green,
                        ConvertEnemyName(obj) + "\n" +
                        $"{Vector3.Distance(plane.transform.position, obj.transform.position):F1}m" + "\n" +
                        hptext,
                        useCrossContainer);

                if (!isLocked || useCrossContainer)
                {
                    if (!isBlinking)
                    {
                        TargetHudContainerPool.SetLineEnabled(conteiners[idx].lr, false);
                        TargetHudContainerPool.SetLineEnabled(conteiners[idx].crossLrA, useCrossContainer);
                        TargetHudContainerPool.SetLineEnabled(conteiners[idx].crossLrB, useCrossContainer);
                    }
                }
                else
                {
                    TargetHudContainerPool.SetLineEnabled(conteiners[idx].lr, true);
                    TargetHudContainerPool.SetLineEnabled(conteiners[idx].crossLrA, false);
                    TargetHudContainerPool.SetLineEnabled(conteiners[idx].crossLrB, false);
                }

            }
            else
            {
                bool isEcmTarget = IsEcmTarget(obj);
                UpdateContainer(idx, obj,
                    isLocked ? Color.red : Color.green,
                    (isNext ? "Next" : ""),
                    isEcmTarget);

            }
            idx++;
        }
        foreach (var obj in arrys)
        {
            conteninerobj = conteiners[idx].obj;
            if (obj == null)
            {
                ClearContainer(idx);
                continue;
            }
            if (obj.name == "Player") continue;
            UpdateContainer(idx, obj, Color.cyan, "Arry", false);
            idx++;
        }

        // 余った分は非表示
        for (int i = conteiners.Count - 1; i >= idx; i--)
        {
            ReleaseContainerToPool(i);
        }
    }
    void ClearContainer(int idx)
    {
        HideContainer(idx, false);
    }

    void ReleaseContainer(int idx)
    {
        HideContainer(idx, true);
    }

    void ReleaseContainerToPool(int idx)
    {
        if (idx < 0 || idx >= conteiners.Count)
            return;

        TargetHudContainerPool.Release(conteiners[idx]);
        conteiners.RemoveAt(idx);
    }

    void ClearAllContainers()
    {
        for (int i = conteiners.Count - 1; i >= 0; i--)
        {
            ReleaseContainerToPool(i);
        }
    }

    void HideAllContainers()
    {
        for (int i = 0; i < conteiners.Count; i++)
        {
            ClearContainer(i);
        }
    }

    void HideContainer(int idx, bool releaseBinding)
    {
        TargetHudContainerPool.Reset(conteiners[idx]);

        if (releaseBinding)
        {
            conteiners[idx].boundTarget = null;
        }
    }


    void UpdateContainer(int idx, GameObject target, Color color, string text, bool useCrossContainer)
    {
        TargetHudContainer entry = conteiners[idx];
        var container = conteiners[idx].obj;
        var renderer = entry.lr;
        var crossRendererA = entry.crossLrA;
        var crossRendererB = entry.crossLrB;
        var containerRect = entry.rect;
        if (renderer == null || crossRendererA == null || crossRendererB == null || containerRect == null) 
        { 
            Debug.LogError("DebugHUD: Missing components in container.");
            return; 
        }

        if (!container.activeSelf)
            container.SetActive(true);

        Vector3 viewportPos = mainCam.WorldToViewportPoint(target.transform.position);
        Vector3 screenPos = mainCam.ViewportToScreenPoint(viewportPos);
        float dist = Vector3.Distance(plane.transform.position, target.transform.position);

        if (dist > detectRange || !IsValidViewportPoint(viewportPos))
        {
            ClearContainer(idx);
            return;
        }

        // ===== UI（RectTransform）=====
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            cameraCanvasRect,
            screenPos,
            cameraCanvasUiCamera,
            out Vector2 localPos
        ))
        {
            ClearContainer(idx);
            return;
        }

        containerRect.localPosition = localPos;

        // ===== LineRenderer（ワールド）=====
        Vector3 dir = (target.transform.position - mainCam.transform.position).normalized;
        Vector3 basePos = mainCam.transform.position + dir * 100f;

        Vector3 baseScreen = mainCam.WorldToScreenPoint(basePos);

        for (int i = 0; i < vertexs.Length; i++)
        {
            Vector3 worldPos = mainCam.ScreenToWorldPoint(
                new Vector3(
                    baseScreen.x + vertexs[i].x * 20f,
                    baseScreen.y + vertexs[i].y * 20f,
                    baseScreen.z));
            renderer.SetPosition(i, worldPos);
        }

        Vector3 crossA = mainCam.ScreenToWorldPoint(new Vector3(baseScreen.x - 20f, baseScreen.y - 20f, baseScreen.z));
        Vector3 crossB = mainCam.ScreenToWorldPoint(new Vector3(baseScreen.x + 20f, baseScreen.y + 20f, baseScreen.z));
        Vector3 crossC = mainCam.ScreenToWorldPoint(new Vector3(baseScreen.x - 20f, baseScreen.y + 20f, baseScreen.z));
        Vector3 crossD = mainCam.ScreenToWorldPoint(new Vector3(baseScreen.x + 20f, baseScreen.y - 20f, baseScreen.z));
        crossRendererA.SetPosition(0, crossA);
        crossRendererA.SetPosition(1, crossB);
        crossRendererB.SetPosition(0, crossC);
        crossRendererB.SetPosition(1, crossD);

        TargetHudContainerPool.SetLineEnabled(renderer, !useCrossContainer);
        TargetHudContainerPool.SetLineEnabled(crossRendererA, useCrossContainer);
        TargetHudContainerPool.SetLineEnabled(crossRendererB, useCrossContainer);
        renderer.startColor = color;
        renderer.endColor = color;
        crossRendererA.startColor = color;
        crossRendererA.endColor = color;
        crossRendererB.startColor = color;
        crossRendererB.endColor = color;

        // ===== Text =====
        SetTexts(conteiners[idx], target, text);
    }

    string ConvertEnemyName(GameObject obj)
    {
        if (enemyNameConverter == null)
        {
            enemyNameConverter = EnsureEnemyNameConverter();
        }

        return enemyNameConverter.converter(obj);
    }

    EnemyNameConverterToUI EnsureEnemyNameConverter()
    {
        EnemyNameConverterToUI converter = GetComponent<EnemyNameConverterToUI>();
        if (converter != null)
        {
            return converter;
        }

        converter = FindFirstObjectByType<EnemyNameConverterToUI>();
        if (converter != null)
        {
            return converter;
        }

        return gameObject.AddComponent<EnemyNameConverterToUI>();
    }

    bool IsValidViewportPoint(Vector3 viewportPos)
    {
        if (float.IsNaN(viewportPos.x) ||
            float.IsNaN(viewportPos.y) ||
            float.IsNaN(viewportPos.z) ||
            float.IsInfinity(viewportPos.x) ||
            float.IsInfinity(viewportPos.y) ||
            float.IsInfinity(viewportPos.z))
        {
            return false;
        }

        return viewportPos.z > mainCam.nearClipPlane &&
               viewportPos.x >= 0f &&
               viewportPos.x <= 1f &&
               viewportPos.y >= 0f &&
               viewportPos.y <= 1f;
    }

    void SetTexts(TargetHudContainer container, GameObject target, string text)
    {
        if (container.distanceText != null)
        { 
            container.distanceText.text = text;
            container.distanceTextTransform.localRotation = Quaternion.identity;
        }

        if (container.tgtText != null)
        { 
            container.tgtText.text = target.GetComponent<AugumentStatus>().missionObjective ? "TGT" : "";
            container.tgtTextTransform.localRotation = Quaternion.identity;
        }
    }
    void UpdateHUD()
    {
        if (false) 
        {
        float speed = rb.linearVelocity.magnitude;
        float altitude = plane.transform.position.y;
        float pitch = plane.transform.eulerAngles.x;
        float roll = plane.transform.eulerAngles.z;
        float thr = ac.throttle;

        hudBuilder.Clear();
        hudBuilder.Append("SPD: ");
        hudBuilder.Append(speed.ToString("F1"));
        hudBuilder.Append(" m/s\nALT: ");
        hudBuilder.Append(altitude.ToString("F1"));
        hudBuilder.Append(" m\nTHR: ");
        hudBuilder.Append(thr.ToString("F2"));
        hudBuilder.Append("\nPITCH: ");
        hudBuilder.Append(pitch.ToString("F1"));
        hudBuilder.Append("°\nROLL: ");
        hudBuilder.Append(roll.ToString("F1"));
        hudBuilder.Append('°');
        if (deceived)
            hudBuilder.Append("\nDECEIVED");
        hudText.text = hudBuilder.ToString();
        }
        else
        {
            // デバッグ用  オブジェクト数監視処理
            hudText.text = $"Enemies: {targets.Count}\nAllies: {arrys.Count}\nMarking: {markingtargets.Count}\nLocked: {Lockedtargets.Count}";
        }
    }
    #endregion
}
