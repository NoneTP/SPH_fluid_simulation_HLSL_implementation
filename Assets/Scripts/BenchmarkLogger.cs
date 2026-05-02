using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// Logs FPS and frame time to a CSV file.
/// Attach to any GameObject. Press B to start/stop a benchmark run.
/// Output goes to Assets/../experiments/results/runtime.csv
/// </summary>
public class BenchmarkLogger : MonoBehaviour
{
    [Tooltip("Label for this run — e.g. 'Baseline_10k' or 'GPU_30k'")]
    [SerializeField] private string runLabel = "GPU_12k";

    [Tooltip("How many seconds to record")]
    [SerializeField] private float   duration    = 10f;

    [Tooltip("How often to sample (seconds)")]
    [SerializeField] private float   sampleRate  = 0.1f;

    private bool      recording = false;
    private float     elapsed   = 0f;
    private float     nextSample = 0f;
    private StringBuilder csv;

    private void Update()
    {
        // Press B to toggle benchmark
        if (Keyboard.current.bKey.wasPressedThisFrame)        {
            if (!recording) StartBenchmark();
            else            StopBenchmark();
        }

        if (!recording) return;

        elapsed += Time.deltaTime;

        if (elapsed > duration)
        {
            StopBenchmark();
            return;
        }

        if (Time.time >= nextSample)
        {
            float fps       = 1f / Time.deltaTime;
            float msPerFrame = Time.deltaTime * 1000f;
            csv.AppendLine($"{runLabel},{elapsed:F2},{fps:F1},{msPerFrame:F2}");
            nextSample = Time.time + sampleRate;
        }
    }

    private void StartBenchmark()
    {
        recording   = true;
        elapsed     = 0f;
        nextSample  = Time.time;
        csv         = new StringBuilder();
        csv.AppendLine("Run,Time(s),FPS,ms_per_frame");
        Debug.Log($"[Benchmark] Started: {runLabel}");
    }

    private void StopBenchmark()
    {
        recording = false;

        // Write to experiments/results/
        string dir  = Path.Combine(Application.dataPath, "..", "experiments", "results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{runLabel}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllText(path, csv.ToString());

        Debug.Log($"[Benchmark] Saved to: {path}");
    }

    private void OnGUI()
    {
        string status = recording
            ? $"<color=red>● REC {runLabel}  {elapsed:F1}/{duration:F0}s</color>"
            : "Press <b>B</b> to benchmark";

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 16,
            richText  = true,
            alignment = TextAnchor.UpperLeft
        };
        GUI.Label(new Rect(10, 10, 400, 30), status, style);
        GUI.Label(new Rect(10, 35, 400, 30),
            $"{(1f/Time.deltaTime):F0} FPS  |  {Time.deltaTime*1000f:F1} ms", style);
    }
}
