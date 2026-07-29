// BakeMountainToTerrain.cs
// 将 map(MAP) 下 mountain 子物体中的所有方块(Cube)的形状，烘焙成 Unity Terrain 的高度图(heightmap)，
// 这样你就能在 Terrain 组件上直接用笔刷(brush)继续雕地形，而不用手动重刷。
// 用法：Unity 菜单 Tools > Bake Mountain To Terrain，按需调整窗口参数后点 Bake。

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BakeMountainToTerrain : EditorWindow
{
    // ---- 可调参数（也可直接在窗口里改）----
    private string terrainName      = "Terrain";   // 要写入的 Terrain 物体名
    private string mountainPath     = "MAP/mountain"; // mountain 的路径（父/子）
    private int    smoothingPasses  = 2;     // 平滑(blur)次数：让方块边缘更自然，便于后续笔刷
    private float  margin          = 3f;    // terrain 相对 mountain 包围盒外扩的余量
    private bool   flipX           = false; // 若烘焙结果沿 X 镜像，勾选后重跑
    private bool   flipZ           = false; // 若烘焙结果沿 Z 镜像，勾选后重跑
    private bool   overwriteExisting = true; // 若 Terrain 已有手刷高度，是否覆盖
    private bool   hideMountainAfterBake = false; // 烘焙后是否隐藏源方块(非破坏，可随时重新显示)

    private Vector2 _scroll;

    [MenuItem("Tools/Bake Mountain To Terrain")]
    static void Open()
    {
        var w = GetWindow<BakeMountainToTerrain>();
        w.titleContent = new GUIContent("Bake Mountain");
        w.Show();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.HelpBox(
            "把 MAP/mountain 下方所有方块的形状烘焙进场景里已有的 Terrain 高度图。\n" +
            "运行后 Terrain 即呈现 mountain 的轮廓，你可直接用笔刷继续编辑。", MessageType.Info);

        terrainName         = EditorGUILayout.TextField("Terrain 物体名", terrainName);
        mountainPath        = EditorGUILayout.TextField("mountain 路径", mountainPath);
        smoothingPasses     = EditorGUILayout.IntSlider("平滑次数", smoothingPasses, 0, 6);
        margin              = EditorGUILayout.FloatField("外扩余量", margin);
        flipX               = EditorGUILayout.Toggle("翻转 X(若镜像)", flipX);
        flipZ               = EditorGUILayout.Toggle("翻转 Z(若镜像)", flipZ);
        overwriteExisting   = EditorGUILayout.Toggle("覆盖已有高度", overwriteExisting);
        hideMountainAfterBake = EditorGUILayout.Toggle("烘焙后隐藏方块", hideMountainAfterBake);

        EditorGUILayout.Space();
        if (GUILayout.Button("Bake（开始烘焙）", GUILayout.Height(32)))
        {
            Bake();
        }
        EditorGUILayout.EndScrollView();
    }

    // 单个方块的包围信息（世界坐标）
    struct Box
    {
        public Vector3 center; // 世界中心 world center
        public Vector3 scale;  // 世界尺寸 world size (lossyScale)
    }

    private void Bake()
    {
        // 1) 找到 mountain 及其下方所有 Cube 方块
        Transform mountain = FindMountain(mountainPath);
        if (mountain == null) return;

        List<Box> boxes = new List<Box>();
        foreach (var t in mountain.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Cube", System.StringComparison.OrdinalIgnoreCase))
            {
                boxes.Add(new Box { center = t.position, scale = t.lossyScale });
            }
        }
        if (boxes.Count == 0) { Debug.LogError("[Bake] mountain 下没有找到名为 Cube 的子物体。"); return; }

        // 2) 计算 mountain 世界包围盒
        float minX = Mathf.Infinity, maxX = -Mathf.Infinity;
        float minZ = Mathf.Infinity, maxZ = -Mathf.Infinity;
        float minBot = Mathf.Infinity, maxTop = -Mathf.Infinity;
        foreach (var b in boxes)
        {
            minX = Mathf.Min(minX, b.center.x - b.scale.x * 0.5f);
            maxX = Mathf.Max(maxX, b.center.x + b.scale.x * 0.5f);
            minZ = Mathf.Min(minZ, b.center.z - b.scale.z * 0.5f);
            maxZ = Mathf.Max(maxZ, b.center.z + b.scale.z * 0.5f);
            minBot = Mathf.Min(minBot, b.center.y - b.scale.y * 0.5f);
            maxTop = Mathf.Max(maxTop, b.center.y + b.scale.y * 0.5f);
        }
        Debug.Log($"[Bake] mountain 方块数={boxes.Count}，X[{minX:F2},{maxX:F2}] Z[{minZ:F2},{maxZ:F2}] Y底{minBot:F2} 顶{maxTop:F2}");

        // 3) 找到目标 Terrain（优先按名字，否则取场景第一个）
        Terrain terrain = null;
        var tgo = GameObject.Find(terrainName);
        if (tgo != null) terrain = tgo.GetComponent<Terrain>();
        if (terrain == null) terrain = Object.FindObjectOfType<Terrain>();
        if (terrain == null)
        {
            // 没有就新建一个，并定位到覆盖 mountain
            var data = new TerrainData();
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = string.IsNullOrEmpty(terrainName) ? "Terrain" : terrainName;
            terrain = go.GetComponent<Terrain>();
            Debug.Log("[Bake] 场景中未找到 Terrain，已新建一个。");
        }

        var data2 = terrain.terrainData;
        int res = data2.heightmapResolution;
        Vector3 size = data2.size;
        Vector3 corner = terrain.transform.position;

        // 4) 若 Terrain 未覆盖 mountain，则重定位/缩放到覆盖（仅在必要时）
        float mMinX = minX - margin, mMaxX = maxX + margin;
        float mMinZ = minZ - margin, mMaxZ = maxZ + margin;
        bool covers = corner.x <= mMinX && (corner.x + size.x) >= mMaxX &&
                      corner.z <= mMinZ && (corner.z + size.z) >= mMaxZ;
        if (!covers)
        {
            float newW = (mMaxX - mMinX);
            float newD = (mMaxZ - mMinZ);
            corner = new Vector3(mMinX, corner.y, mMinZ);
            terrain.transform.position = corner;
            data2.size = new Vector3(newW, size.y, newD);
            size = data2.size;
            Debug.Log($"[Bake] Terrain 未覆盖 mountain，已重定位/缩放：pos={corner} size={size}");
        }

        // 5) 设定高度基准与范围，确保 mountain 完整且清晰地落在地形内
        // 关键修复：必须按 mountain 的真实高度来设置地形高度范围(size.y)。
        // 否则若原地形范围很大（如默认 600），mountain 只会被压成几乎看不见的平面。
        float baseY = Mathf.Min(minBot - 1f, terrain.transform.position.y);
        float heightRange = (maxTop - baseY) * 1.1f;

        // 检查是否已有手刷高度
        float[,] existing = data2.GetHeights(0, 0, res, res);
        float emin = 1f, emax = 0f;
        for (int i = 0; i < res; i++) for (int j = 0; j < res; j++)
        { emin = Mathf.Min(emin, existing[i, j]); emax = Mathf.Max(emax, existing[i, j]); }
        if (emax - emin > 0.02f)
        {
            if (!overwriteExisting)
            {
                Debug.LogError("[Bake] Terrain 已有手刷高度且未勾选覆盖，已中止。请勾选 overwriteExisting 或先备份。");
                return;
            }
            Debug.LogWarning("[Bake] Terrain 已有手刷高度，将被覆盖。");
        }

        // 总是把高度范围设为能清晰容纳 mountain 的值（即使原 size.y 更大也缩小，避免山体被压扁）
        terrain.transform.position = new Vector3(terrain.transform.position.x, baseY, terrain.transform.position.z);
        data2.size = new Vector3(data2.size.x, heightRange, data2.size.z);
        size = data2.size;
        Debug.Log($"[Bake] 地形高度范围已设为 {size.y:F2}（基准Y={baseY:F2}）；原 size.y 过大正是之前看不见的主因。");

        // 6) 逐格烘焙：把每个高度图单元格映射到世界 XZ，取覆盖它的方块顶面最高点
        Undo.RecordObject(terrain, "Bake Mountain To Terrain");
        Undo.RecordObject(data2, "Bake Mountain To Terrain");

        float[,] h = new float[res, res];
        for (int i = 0; i < res; i++)
        {
            for (int j = 0; j < res; j++)
            {
                int fi = flipX ? (res - 1 - i) : i;
                int fj = flipZ ? (res - 1 - j) : j;
                float wx = corner.x + ((float)fi / (res - 1)) * size.x;
                float wz = corner.z + ((float)fj / (res - 1)) * size.z;

                float top = 0f;
                foreach (var b in boxes)
                {
                    if (wx >= b.center.x - b.scale.x * 0.5f && wx <= b.center.x + b.scale.x * 0.5f &&
                        wz >= b.center.z - b.scale.z * 0.5f && wz <= b.center.z + b.scale.z * 0.5f)
                    {
                        top = Mathf.Max(top, b.center.y + b.scale.y * 0.5f);
                    }
                }
                float norm = (top - baseY) / size.y;
                h[i, j] = Mathf.Clamp(norm, 0f, 1f);
            }
        }

        // 7) 平滑（让方块边缘更自然，便于后续笔刷）
        for (int p = 0; p < smoothingPasses; p++)
            h = BoxBlur(h, res);

        data2.SetHeights(0, 0, h);
        data2.SyncHeightmap();

        // 诊断：确认高度图确实写入了内容（max 应明显 > 0）
        float[,] chk = data2.GetHeights(0, 0, res, res);
        float cmin = 1f, cmax = 0f;
        for (int i = 0; i < res; i++) for (int j = 0; j < res; j++)
        { cmin = Mathf.Min(cmin, chk[i, j]); cmax = Mathf.Max(cmax, chk[i, j]); }
        Debug.Log($"[Bake] 烘焙后高度图归一化范围 min={cmin:F4} max={cmax:F4}（max 应明显 > 0；若接近 0 说明采样未命中方块，需查坐标/翻转）。");

        if (hideMountainAfterBake)
            mountain.gameObject.SetActive(false);

        EditorUtility.SetDirty(data2);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Bake] 完成！Terrain 已呈现 mountain 形状（分辨率 {res}x{res}，基准Y={baseY:F2}，高度范围={size.y:F2}）。可在 Terrain 上直接用笔刷继续编辑。");
    }

    private static float[,] BoxBlur(float[,] src, int res)
    {
        float[,] dst = new float[res, res];
        for (int i = 0; i < res; i++)
        {
            for (int j = 0; j < res; j++)
            {
                float s = 0f; int c = 0;
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int ii = i + di, jj = j + dj;
                        if (ii >= 0 && ii < res && jj >= 0 && jj < res) { s += src[ii, jj]; c++; }
                    }
                dst[i, j] = s / c;
            }
        }
        return dst;
    }

    private static Transform FindMountain(string path)
    {
        // 支持 "父/子" 形式的路径，也兼容直接按名查找
        var go = GameObject.Find(path);
        if (go != null) return go.transform;

        // 退化处理：拆分路径逐级查找
        string[] parts = path.Split('/');
        if (parts.Length >= 2)
        {
            var parent = GameObject.Find(parts[0]);
            if (parent != null)
            {
                var child = parent.transform.Find(parts[1]);
                if (child != null) return child;
            }
        }
        var any = GameObject.Find("mountain");
        if (any != null) return any.transform;

        Debug.LogError($"[Bake] 未找到 mountain（路径={path}）。请确认 MAP 下存在名为 mountain 的子物体。");
        return null;
    }
}
#endif
