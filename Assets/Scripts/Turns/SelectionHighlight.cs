using UnityEngine;

namespace Vampire.Turns
{
// 当前行动单位脚下的地面光环。运行时程序化生成一个扁平圆环网格，
// 通过 SetTarget 挂到目标单位下方，随单位移动自动跟随。
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SelectionHighlight : MonoBehaviour
{
    [SerializeField] private float innerRadius = 0.55f;
    [SerializeField] private float outerRadius = 0.8f;
    [SerializeField] private int segments = 48;
    [SerializeField] private float groundOffset = 0.05f; // 略微抬离地面，避免 z-fighting
    [SerializeField] private Color color = new Color(0.2f, 0.9f, 1f, 1f);

    private Transform target;
    private Mesh ringMesh;
    private Material ringMaterial;

    // 运行时创建一个带 SelectionHighlight 的物体
    public static SelectionHighlight Create()
    {
        var go = new GameObject("SelectionHighlight");
        return go.AddComponent<SelectionHighlight>();
    }

    // 创建一个指定颜色和尺寸的光环。可用于敌方标记等常驻提示。
    public static SelectionHighlight Create(Color ringColor, string objectName,
        float innerRadius, float outerRadius, float groundOffset)
    {
        var go = new GameObject(objectName);
        var highlight = go.AddComponent<SelectionHighlight>();
        highlight.Configure(ringColor, innerRadius, outerRadius, groundOffset);
        return highlight;
    }

    void Awake()
    {
        RebuildMesh();

        // URP 下用无光照 + 自发光材质，保证颜色鲜亮且不受场景光影响
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        ringMaterial = new Material(shader);
        ApplyColor();
        GetComponent<MeshRenderer>().sharedMaterial = ringMaterial;
        GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void Configure(Color ringColor, float newInnerRadius, float newOuterRadius,
        float newGroundOffset)
    {
        color = ringColor;
        innerRadius = Mathf.Max(0f, newInnerRadius);
        outerRadius = Mathf.Max(innerRadius, newOuterRadius);
        groundOffset = newGroundOffset;
        RebuildMesh();
        ApplyColor();
    }

    private void ApplyColor()
    {
        if (ringMaterial == null) return;
        if (ringMaterial.HasProperty("_BaseColor")) ringMaterial.SetColor("_BaseColor", color);
        if (ringMaterial.HasProperty("_Color")) ringMaterial.SetColor("_Color", color);
    }

    private void RebuildMesh()
    {
        if (ringMesh != null) Destroy(ringMesh);
        ringMesh = BuildRingMesh();
        GetComponent<MeshFilter>().sharedMesh = ringMesh;
    }

    // 把光环挂到目标单位脚下；target 为 null 则隐藏
    public void SetTarget(Transform t)
    {
        target = t;
        if (target == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        transform.SetParent(target, false);
        // 单位原点通常在中心，胶囊半高约 1，用碰撞体把光环压到脚底
        float footY = -GetTargetHalfHeight(target) + groundOffset;
        transform.localPosition = new Vector3(0f, footY, 0f);
        transform.localRotation = Quaternion.identity;
    }

    private float GetTargetHalfHeight(Transform t)
    {
        var col = t.GetComponent<Collider>();
        if (col != null) return col.bounds.extents.y;
        return 1f;
    }

    // 在 XZ 平面生成一个环形（三角带）网格
    private Mesh BuildRingMesh()
    {
        var mesh = new Mesh { name = "Ring" };
        int vCount = segments * 2;
        var verts = new Vector3[vCount];
        var tris = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            verts[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
            verts[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
        }

        for (int i = 0; i < segments; i++)
        {
            int i0 = i * 2;
            int i1 = i * 2 + 1;
            int i2 = (i * 2 + 2) % vCount;
            int i3 = (i * 2 + 3) % vCount;

            int t = i * 6;
            tris[t] = i0; tris[t + 1] = i2; tris[t + 2] = i1;
            tris[t + 3] = i1; tris[t + 4] = i2; tris[t + 5] = i3;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (ringMesh != null) Destroy(ringMesh);
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
}
