using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Resultado de um raycast contra a BVH de um ObjectBVH (equivalente simplificado
/// de um RaycastHit, mas construído por nós mesmos a partir do teste raio-triângulo).
/// </summary>
public struct BVHRayHit
{
    public bool hit;
    public Vector3 point;
    public Vector3 normal;
    public float distance;
    public GameObject gameObject;
}

/// <summary>
/// Coloque este componente na raiz de cada prefab estático (o mesmo objeto que
/// tem o LODGroup). Ele extrai os triângulos da mesh de maior qualidade (LOD0),
/// constrói uma BVH sobre esses triângulos e expõe dois métodos de raycast:
///   - RaycastBVH: usa a hierarquia para podar testes de triângulo
///   - RaycastBruteForce: testa TODOS os triângulos (baseline "sem BVH")
///
/// Como o objeto é estático, a extração e a construção da BVH podem ser feitas
/// uma única vez (Awake) e reaproveitadas para sempre.
/// </summary>
public class ObjectBVH : MonoBehaviour
{
    [Tooltip("Opcional: force um MeshFilter específico. Se vazio, detecta automaticamente (usa o LOD0 se houver LODGroup).")]
    public MeshFilter meshFilterOverride;

    [Tooltip("Quantos triângulos no máximo por folha da BVH")]
    public int maxTrianglesPerLeaf = 4;

    [HideInInspector] public BVHNode root;
    [HideInInspector] public List<Triangle> allTriangles;

    [Header("Debug / Gizmos")]
    public bool drawGizmos = true;
    [Tooltip("Limite de profundidade desenhada, para não poluir a Scene View")]
    public int maxGizmoDepth = 6;
    public Color leafColor = new Color(0f, 1f, 0f, 0.5f);
    public Color internalColor = new Color(1f, 1f, 0f, 0.2f);

    void Awake()
    {
        BuildFromMesh();
    }

    public void BuildFromMesh()
    {
        MeshFilter mf = meshFilterOverride != null ? meshFilterOverride : GetHighestLODMeshFilter();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning($"ObjectBVH em '{name}': nenhum MeshFilter/mesh encontrado.");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        if (!mesh.isReadable)
        {
            Debug.LogError($"ObjectBVH em '{name}': a mesh '{mesh.name}' precisa estar com " +
                            "'Read/Write Enabled' habilitado nas configurações de importação do modelo.");
            return;
        }

        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        Transform t = mf.transform;

        allTriangles = new List<Triangle>(tris.Length / 3);
        for (int i = 0; i < tris.Length; i += 3)
        {
            allTriangles.Add(new Triangle
            {
                v0 = t.TransformPoint(verts[tris[i]]),
                v1 = t.TransformPoint(verts[tris[i + 1]]),
                v2 = t.TransformPoint(verts[tris[i + 2]])
            });
        }

        root = BVHBuilder.Build(allTriangles, maxTrianglesPerLeaf);
    }

    MeshFilter GetHighestLODMeshFilter()
    {
        LODGroup lodGroup = GetComponentInChildren<LODGroup>();
        if (lodGroup != null)
        {
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length > 0 && lods[0].renderers.Length > 0)
            {
                Renderer r = lods[0].renderers.FirstOrDefault(rr => rr != null);
                if (r != null)
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf != null) return mf;
                }
            }
        }
        // Sem LODGroup: pega o primeiro MeshFilter encontrado
        return GetComponentInChildren<MeshFilter>();
    }

    /// <summary>Raycast usando a BVH: poda ramos cujo AABB não é atravessado pelo raio.</summary>
    public bool RaycastBVH(Ray ray, out BVHRayHit hit, out int triTests)
    {
        triTests = 0;
        float closest = Mathf.Infinity;
        bool found = false;
        Vector3 point = default, normal = default;

        RaycastNode(root, ray, ref triTests, ref found, ref closest, ref point, ref normal);

        hit = new BVHRayHit
        {
            hit = found,
            point = point,
            normal = normal,
            distance = closest,
            gameObject = found ? gameObject : null
        };
        return found;
    }

    void RaycastNode(BVHNode node, Ray ray, ref int triTests, ref bool found, ref float closest, ref Vector3 point, ref Vector3 normal)
    {
        if (node == null) return;

        // Teste barato: raio contra a AABB do nó (raiz primeiro, depois filhos)
        if (!node.bounds.IntersectRay(ray)) return;

        if (node.IsLeaf)
        {
            foreach (Triangle tri in node.triangles)
            {
                triTests++;
                if (RayTriangle.Intersect(ray, tri.v0, tri.v1, tri.v2, out float dist, out Vector3 n))
                {
                    if (dist < closest)
                    {
                        closest = dist;
                        point = ray.GetPoint(dist);
                        normal = n;
                        found = true;
                    }
                }
            }
            return;
        }

        RaycastNode(node.left, ray, ref triTests, ref found, ref closest, ref point, ref normal);
        RaycastNode(node.right, ray, ref triTests, ref found, ref closest, ref point, ref normal);
    }

    /// <summary>Força bruta: testa TODOS os triângulos da mesh, sem usar a BVH.</summary>
    public bool RaycastBruteForce(Ray ray, out BVHRayHit hit, out int triTests)
    {
        triTests = 0;
        float closest = Mathf.Infinity;
        bool found = false;
        Vector3 point = default, normal = default;

        foreach (Triangle tri in allTriangles)
        {
            triTests++;
            if (RayTriangle.Intersect(ray, tri.v0, tri.v1, tri.v2, out float dist, out Vector3 n))
            {
                if (dist < closest)
                {
                    closest = dist;
                    point = ray.GetPoint(dist);
                    normal = n;
                    found = true;
                }
            }
        }

        hit = new BVHRayHit
        {
            hit = found,
            point = point,
            normal = normal,
            distance = closest,
            gameObject = found ? gameObject : null
        };
        return found;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Constrói sob demanda também fora do Play Mode, para poder depurar
        // os Gizmos direto na Scene View sem precisar dar Play.
        if (root == null) BuildFromMesh();
        if (root == null) return;

        DrawNode(root, 0);
    }

    void DrawNode(BVHNode node, int depth)
    {
        if (node == null || depth > maxGizmoDepth) return;
        Gizmos.color = node.IsLeaf ? leafColor : internalColor;
        Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
        DrawNode(node.left, depth + 1);
        DrawNode(node.right, depth + 1);
    }
}
