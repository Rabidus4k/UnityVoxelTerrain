using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Инструмент деформации в стиле Deep Rock Galactic. Вешается на камеру:
/// ЛКМ (удерживать) — копать породу, ПКМ — наращивать, колесо — радиус.
/// </summary>
public class TerrainDeformer : MonoBehaviour
{
    [Tooltip("Дальность инструмента в метрах.")]
    public float range = 12f;
    [Tooltip("Радиус сферы деформации в метрах.")]
    public float radius = 2.5f;
    public float minRadius = 1f;
    public float maxRadius = 6f;
    [Tooltip("Сколько плотности снимается в секунду в центре сферы (1 = мгновенно).")]
    public float digSpeed = 4f;
    public float buildSpeed = 3f;
    [Tooltip("Скорость кисти-сглаживания (СКМ), долей размытия в секунду.")]
    public float smoothSpeed = 6f;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            radius = Mathf.Clamp(radius + Mathf.Sign(scroll) * 0.5f, minRadius, maxRadius);

        bool dig = mouse.leftButton.isPressed;
        bool build = mouse.rightButton.isPressed;
        bool smooth = mouse.middleButton.isPressed;
        if (!dig && !build && !smooth) return;

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range)) return;

        var terrain = hit.collider.GetComponentInParent<VoxelTerrain>();
        if (terrain == null) return;

        if (dig)
            terrain.Dig(hit.point, radius, digSpeed * Time.deltaTime);
        else if (build)
            // Точку слегка утапливаем в породу, чтобы нарост цеплялся за поверхность.
            terrain.Build(hit.point - ray.direction * radius * 0.3f, radius, buildSpeed * Time.deltaTime);
        else
            terrain.SmoothArea(hit.point, radius, smoothSpeed * Time.deltaTime);
    }

    void OnGUI()
    {
        var center = new Rect(Screen.width * 0.5f - 8, Screen.height * 0.5f - 12, 16, 24);
        GUI.Label(center, "+");
        GUI.Label(new Rect(12, 12, 520, 24), $"ЛКМ — копать | ПКМ — строить | СКМ — сгладить | колесо — радиус: {radius:0.0} м");
    }
}
