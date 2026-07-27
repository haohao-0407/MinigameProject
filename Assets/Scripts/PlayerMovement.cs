using UnityEngine;
using UnityEngine.AI;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveBudget = 10f; // 每次点击允许的最大 cost（路径长度）

    private Camera cam;
    private NavMeshAgent agent;

    void Awake()
    {
        cam = Camera.main;
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.point, path)) return;
        if (path.status == NavMeshPathStatus.PathInvalid) return;

        Vector3 target = TruncateToBudget(path.corners, moveBudget);

        // 落点吸附回 NavMesh，避免截断点落在网格边缘外
        if (NavMesh.SamplePosition(target, out var navHit, 1f, agent.areaMask))
            agent.SetDestination(navHit.position);
    }

    // 沿折线累加长度，超预算就在该段中间返回落点；预算够则返回终点
    private Vector3 TruncateToBudget(Vector3[] corners, float budget)
    {
        if (corners.Length == 0) return transform.position;

        float remaining = budget;
        for (int i = 1; i < corners.Length; i++)
        {
            float seg = Vector3.Distance(corners[i - 1], corners[i]);
            if (seg <= remaining)
            {
                remaining -= seg;
                continue;
            }
            Vector3 dir = (corners[i] - corners[i - 1]).normalized;
            return corners[i - 1] + dir * remaining;
        }
        return corners[corners.Length - 1];
    }
}
