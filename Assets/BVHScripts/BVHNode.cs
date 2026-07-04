using System.Collections.Generic;

/// <summary>
/// Nó de uma BVH construída sobre triângulos de uma mesh.
/// Nós internos têm left/right preenchidos; folhas têm "triangles" preenchido.
/// </summary>
public class BVHNode
{
    public UnityEngine.Bounds bounds;
    public BVHNode left;
    public BVHNode right;
    public List<Triangle> triangles; // só != null em folhas

    public bool IsLeaf => triangles != null;

    public BVHNode(UnityEngine.Bounds bounds)
    {
        this.bounds = bounds;
    }
}
