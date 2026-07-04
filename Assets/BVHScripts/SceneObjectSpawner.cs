using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Instancia N cópias de cada prefab dentro de uma área retangular,
/// tentando evitar sobreposição e encaixando cada objeto no chão.
/// </summary>
public class SceneObjectSpawner : MonoBehaviour
{
    [Tooltip("Os prefabs a instanciar (cada um já deve ter o ObjectBVH configurado)")]
    public GameObject[] prefabs;

    [Tooltip("Quantas cópias de CADA prefab instanciar")]
    public int copiesPerPrefab = 10;

    [Tooltip("Centro da área de spawn (normalmente a posição deste próprio objeto)")]
    public Vector3 areaCenter = Vector3.zero;

    [Tooltip("Tamanho da área de spawn: X = largura, Z = profundidade")]
    public Vector2 areaSize = new Vector2(50f, 50f);

    [Tooltip("Camada(s) considerada(s) 'chão' para encaixar os objetos na altura certa")]
    public LayerMask groundMask = ~0;

    [Tooltip("Distância mínima entre dois objetos, para evitar sobreposição")]
    public float minDistanceBetweenObjects = 3f;

    [Tooltip("Rotação Y aleatória para cada instância")]
    public bool randomizeRotation = true;

    void Start()
    {
        SpawnAll();
    }

    [ContextMenu("Spawnar Objetos Agora")]
    public void SpawnAll()
    {
        // Remove instâncias anteriores
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(transform.GetChild(i).gameObject);
            else
#endif
                Destroy(transform.GetChild(i).gameObject);
        }

        List<Vector3> placed = new List<Vector3>();

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("SceneObjectSpawner: nenhum prefab atribuído.");
            return;
        }

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;

            for (int i = 0; i < copiesPerPrefab; i++)
            {
                Vector3 pos = FindValidPosition(placed, 30);
                placed.Add(pos);

                Quaternion rot = randomizeRotation
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : prefab.transform.rotation;

                GameObject instance = Instantiate(prefab, pos, rot, transform);
                instance.name = $"{prefab.name}_{i}";
            }
        }

        Debug.Log($"SceneObjectSpawner: {placed.Count} objetos instanciados ({prefabs.Length} tipos x {copiesPerPrefab} cópias).");
    }

    Vector3 FindValidPosition(List<Vector3> alreadyPlaced, int maxAttempts)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 candidate = RandomPointInArea();
            candidate = SnapToGround(candidate);

            if (!IsTooClose(candidate, alreadyPlaced))
                return candidate;
        }

        // não achou posição livre: usa a última tentativa mesmo assim
        return SnapToGround(RandomPointInArea());
    }

    Vector3 RandomPointInArea()
    {
        float x = areaCenter.x + Random.Range(-areaSize.x / 2f, areaSize.x / 2f);
        float z = areaCenter.z + Random.Range(-areaSize.y / 2f, areaSize.y / 2f);
        return new Vector3(x, areaCenter.y, z);
    }

    bool IsTooClose(Vector3 candidate, List<Vector3> alreadyPlaced)
    {
        foreach (Vector3 p in alreadyPlaced)
        {
            if (Vector3.Distance(p, candidate) < minDistanceBetweenObjects)
                return true;
        }
        return false;
    }

    Vector3 SnapToGround(Vector3 pos)
    {
        Vector3 rayStart = pos + Vector3.up * 200f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 1000f, groundMask))
        {
            return hit.point;
        }
        return pos; // sem chão detectado: mantém a altura original
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 size = new Vector3(areaSize.x, 0.1f, areaSize.y);
        Gizmos.DrawWireCube(areaCenter, size);
    }
}
