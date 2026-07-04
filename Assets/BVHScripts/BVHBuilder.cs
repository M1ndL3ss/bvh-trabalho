using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Constrói uma BVH binária a partir de uma lista de triângulos (em coordenadas
/// de mundo), dividindo top-down pelo eixo mais longo (median split), até que
/// cada folha tenha no máximo "maxLeafSize" triângulos.
/// </summary>
public static class BVHBuilder
{
    public static BVHNode Build(List<Triangle> tris, int maxLeafSize = 4)
    {
        if (tris == null || tris.Count == 0)
            return null;

        Bounds combined = tris[0].Bounds;
        for (int i = 1; i < tris.Count; i++)
            combined.Encapsulate(tris[i].Bounds);

        if (tris.Count <= maxLeafSize)
        {
            BVHNode leaf = new BVHNode(combined);
            leaf.triangles = tris;
            return leaf;
        }

        Vector3 size = combined.size;
        int axis = 0;
        if (size.y > size.x && size.y >= size.z) axis = 1;
        else if (size.z > size.x && size.z >= size.y) axis = 2;

        List<Triangle> sorted = tris.OrderBy(t => t.Centroid[axis]).ToList();
        int mid = sorted.Count / 2;

        BVHNode node = new BVHNode(combined);
        node.left = Build(sorted.GetRange(0, mid), maxLeafSize);
        node.right = Build(sorted.GetRange(mid, sorted.Count - mid), maxLeafSize);
        return node;
    }
}
