using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamagePopupUI : MonoBehaviour
{
    TextMesh textMesh;
    Camera targetCamera;
    Action<DamagePopupUI> releaseAction;
    Vector3 viewportPosition;
    Color baseColor;
    float lifetime;
    float elapsed;
    float riseSpeed;
    float viewportTextHeight;
    bool active;

    public void Initialize(Font font, Action<DamagePopupUI> onRelease)
    {
        releaseAction = onRelease;
        textMesh = GetComponent<TextMesh>();
        if (textMesh == null) textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.font = font;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 64;
        textMesh.characterSize = 1f;
        textMesh.richText = false;

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (font != null) renderer.sharedMaterial = font.material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 110;
        gameObject.SetActive(false);
    }

    public void Show(
        Camera cameraToUse,
        Vector3 worldPosition,
        string text,
        Color color,
        Vector2 randomViewportOffset,
        float displayTime,
        float viewportRiseSpeed,
        float textViewportHeight)
    {
        targetCamera = cameraToUse;
        if (targetCamera == null)
        {
            releaseAction?.Invoke(this);
            return;
        }

        viewportPosition = targetCamera.WorldToViewportPoint(worldPosition);
        if (viewportPosition.z <= 0f)
        {
            releaseAction?.Invoke(this);
            return;
        }

        viewportPosition.x += randomViewportOffset.x;
        viewportPosition.y += randomViewportOffset.y;
        textMesh.text = text;
        baseColor = color;
        textMesh.color = color;
        lifetime = Mathf.Max(0.01f, displayTime);
        riseSpeed = viewportRiseSpeed;
        viewportTextHeight = Mathf.Max(0.001f, textViewportHeight);
        elapsed = 0f;
        active = true;
        gameObject.SetActive(true);
        UpdateTransform();
    }

    void Update()
    {
        if (!active) return;
        if (targetCamera == null)
        {
            Release();
            return;
        }

        float deltaTime = Time.deltaTime;
        elapsed += deltaTime;
        if (elapsed >= lifetime)
        {
            Release();
            return;
        }

        viewportPosition.y += riseSpeed * deltaTime;
        Color color = baseColor;
        color.a *= 1f - elapsed / lifetime;
        textMesh.color = color;
        UpdateTransform();
    }

    void UpdateTransform()
    {
        // Deliberately do not clamp: damage text may leave the screen.
        transform.position = targetCamera.ViewportToWorldPoint(viewportPosition);
        transform.rotation = Quaternion.LookRotation(
            targetCamera.transform.forward,
            targetCamera.transform.up);
        float worldHeight = targetCamera.orthographic
            ? targetCamera.orthographicSize * 2f * viewportTextHeight
            : 2f * viewportPosition.z
              * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
              * viewportTextHeight;
        transform.localScale = Vector3.one * worldHeight;
    }

    void Release()
    {
        if (!active) return;
        active = false;
        gameObject.SetActive(false);
        releaseAction?.Invoke(this);
    }
}
