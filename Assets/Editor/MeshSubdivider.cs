#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ferramenta de Editor para subdividir a mesh de um objeto selecionado,
/// multiplicando por 4 a contagem de triângulos a cada execução
/// (cada triângulo vira 4 triângulos menores, preservando o formato).
///
/// Uso: selecione o objeto com o MeshFilter na Hierarchy ou no Project,
/// depois vá em Tools > BVH Project > Subdividir Mesh Selecionada.
/// Pode rodar várias vezes seguidas no mesmo objeto para multiplicar mais
/// (1x = x4, 2x = x16, 3x = x64...).
/// </summary>
public static class MeshSubdivider
{
    [MenuItem("Tools/BVH Project/Subdividir Mesh Selecionada")]
    static void SubdivideSelected()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("Selecione um GameObject com MeshFilter (ou MeshFilter dentro dele) antes de rodar.");
            return;
        }

        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning($"'{go.name}': nenhuma mesh encontrada.");
            return;
        }

        Mesh original = mf.sharedMesh;
        int trisBefore = original.triangles.Length / 3;

        Mesh subdivided = Subdivide(original);
        int trisAfter = subdivided.triangles.Length / 3;

        string defaultName = original.name.Replace("_subdivided", "") + "_subdivided";
        string path = EditorUtility.SaveFilePanelInProject(
            "Salvar mesh subdividida",
            defaultName,
            "asset",
            "Escolha onde salvar a nova mesh (não sobrescreve o modelo original)");

        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(subdivided, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(mf, "Subdividir mesh");
        mf.sharedMesh = subdivided;

        MeshCollider mc = mf.GetComponent<MeshCollider>();
        if (mc != null)
        {
            Undo.RecordObject(mc, "Subdividir mesh (collider)");
            mc.sharedMesh = subdivided;
        }

        EditorUtility.SetDirty(go);

        Debug.Log($"'{go.name}': {trisBefore} → {trisAfter} triângulos. Nova mesh salva em: {path}");
    }

    static Mesh Subdivide(Mesh mesh)
    {
        Vector3[] srcVerts = mesh.vertices;
        Vector3[] srcNormals = mesh.normals;
        Vector2[] srcUVs = mesh.uv;
        int[] srcTris = mesh.triangles;

        bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
        bool hasUVs = srcUVs != null && srcUVs.Length == srcVerts.Length;

        List<Vector3> verts = new List<Vector3>(srcVerts);
        List<Vector3> normals = hasNormals ? new List<Vector3>(srcNormals) : null;
        List<Vector2> uvs = hasUVs ? new List<Vector2>(srcUVs) : null;
        List<int> tris = new List<int>(srcTris.Length * 4);

        Dictionary<long, int> midpointCache = new Dictionary<long, int>();

        int Midpoint(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midpointCache.TryGetValue(key, out int idx)) return idx;

            verts.Add((verts[a] + verts[b]) * 0.5f);
            if (hasNormals) normals.Add(((normals[a] + normals[b]) * 0.5f).normalized);
            if (hasUVs) uvs.Add((uvs[a] + uvs[b]) * 0.5f);

            idx = verts.Count - 1;
            midpointCache[key] = idx;
            return idx;
        }

        for (int i = 0; i < srcTris.Length; i += 3)
        {
            int a = srcTris[i], b = srcTris[i + 1], c = srcTris[i + 2];
            int ab = Midpoint(a, b);
            int bc = Midpoint(b, c);
            int ca = Midpoint(c, a);

            // 4 triângulos menores no lugar de 1
            tris.Add(a); tris.Add(ab); tris.Add(ca);
            tris.Add(b); tris.Add(bc); tris.Add(ab);
            tris.Add(c); tris.Add(ca); tris.Add(bc);
            tris.Add(ab); tris.Add(bc); tris.Add(ca);
        }

        Mesh result = new Mesh();
        result.name = mesh.name + "_subdivided";
        result.indexFormat = verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        result.SetVertices(verts);
        if (hasUVs) result.SetUVs(0, uvs);
        result.SetTriangles(tris, 0);

        if (hasNormals) result.SetNormals(normals);
        else result.RecalculateNormals();

        result.RecalculateBounds();
        result.RecalculateTangents();

        return result;
    }
}
#endif
