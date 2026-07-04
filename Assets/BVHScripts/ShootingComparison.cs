using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public enum RaycastMode { BVH, BruteForce }

/// <summary>
/// Dispara um raio do centro da câmera usando o modo ativo (BVH ou força bruta)
/// e loga no console o tempo gasto. Aperte "toggleModeKey" para trocar de modo
/// em tempo real.
/// </summary>
public class ShootingComparison : MonoBehaviour
{
    public Camera fpsCamera;
    public KeyCode fireKey = KeyCode.Mouse0;
    public KeyCode toggleModeKey = KeyCode.Tab;

    [Tooltip("Modo ativo no momento. Pode ser trocado em runtime com a toggleModeKey.")]
    public RaycastMode currentMode = RaycastMode.BVH;

    [Tooltip("Repetições do mesmo raio por tiro, só para suavizar ruído da medição (1 = sem repetição, recomendado se estiver dando lag)")]
    public int samplesForAverage = 1;

    List<ObjectBVH> targets;

    void Start()
    {
        if (fpsCamera == null) fpsCamera = Camera.main;
        RefreshTargetList();
        Debug.Log($"<color=cyan>Modo inicial: {currentMode}</color> (aperte {toggleModeKey} para trocar)");
    }

    public void RefreshTargetList()
    {
        targets = new List<ObjectBVH>(FindObjectsOfType<ObjectBVH>());
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleModeKey))
        {
            currentMode = currentMode == RaycastMode.BVH ? RaycastMode.BruteForce : RaycastMode.BVH;
            Debug.Log($"<color=cyan>Modo alterado para: {currentMode}</color>");
        }

        if (Input.GetKeyDown(fireKey))
        {
            FireAndMeasure();
        }
    }

    void FireAndMeasure()
    {
        Ray ray = fpsCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Stopwatch sw = Stopwatch.StartNew();
        int totalTests = 0;
        bool hit = false;
        string hitName = "-";
        float closest = Mathf.Infinity;

        for (int s = 0; s < samplesForAverage; s++)
        {
            foreach (ObjectBVH obj in targets)
            {
                bool thisHit;
                BVHRayHit h;
                int tests;

                if (currentMode == RaycastMode.BVH)
                    thisHit = obj.RaycastBVH(ray, out h, out tests);
                else
                    thisHit = obj.RaycastBruteForce(ray, out h, out tests);

                totalTests += tests;

                if (thisHit && s == 0 && h.distance < closest)
                {
                    closest = h.distance;
                    hit = true;
                    hitName = h.gameObject.name;
                }
            }
        }
        sw.Stop();

        double ms = sw.Elapsed.TotalMilliseconds / samplesForAverage;
        int avgTests = totalTests / samplesForAverage;
        string modeTag = currentMode == RaycastMode.BVH ? "COM BVH" : "SEM BVH";

        Debug.Log($"[{modeTag}] {ms:F4} ms | testes de triângulo: {avgTests} | acerto: {hit} ({hitName})");
    }
}
