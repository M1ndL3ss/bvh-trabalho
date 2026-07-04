using UnityEngine;

/// <summary>
/// Um triângulo em coordenadas de mundo, extraído de uma mesh estática.
/// </summary>
public struct Triangle
{
    public Vector3 v0, v1, v2;

    public Vector3 Centroid => (v0 + v1 + v2) / 3f;

    public Bounds Bounds
    {
        get
        {
            Bounds b = new Bounds(v0, Vector3.zero);
            b.Encapsulate(v1);
            b.Encapsulate(v2);
            return b;
        }
    }
}

/// <summary>
/// Interseção raio-triângulo (Möller–Trumbore). Usado no teste "a nível de mesh"
/// dentro das folhas da BVH e também no modo força bruta.
/// </summary>
public static class RayTriangle
{
    const float EPS = 1e-7f;

    public static bool Intersect(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float distance, out Vector3 normal)
    {
        distance = 0f;
        normal = Vector3.zero;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(ray.direction, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -EPS && a < EPS) return false; // raio paralelo ao triângulo

        float f = 1f / a;
        Vector3 s = ray.origin - v0;
        float u = f * Vector3.Dot(s, h);
        if (u < 0f || u > 1f) return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(ray.direction, q);
        if (v < 0f || u + v > 1f) return false;

        float t = f * Vector3.Dot(edge2, q);
        if (t > EPS)
        {
            distance = t;
            normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
            return true;
        }
        return false;
    }
}
