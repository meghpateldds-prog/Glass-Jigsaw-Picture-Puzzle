using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Automatically sets up the URP lighting and post-processing environment for the Glass Puzzle.
/// Attach this to a GameObject and click 'Setup Environment' in the inspector.
/// </summary>
public class GlassPuzzleEnvironmentSetup : MonoBehaviour
{
    [Header("Lighting Settings")]
    public Color ambientColor = new Color(0.15f, 0.15f, 0.2f);
    public float directionalIntensity = 1.2f;
    public Vector3 directionalRotation = new Vector3(50, -30, 0);

    [Header("Post Processing")]
    public float bloomIntensity = 4.0f;
    public float bloomThreshold = 1.0f;

    [Header("Reflection")]
    public float reflectionIntensity = 1.0f;

    public void SetupEnvironment()
    {
        // 1. Setup Directional Light
        Light dirLight = FindFirstObjectByType<Light>();
        if (dirLight == null || dirLight.type != LightType.Directional)
        {
            GameObject lightObj = new GameObject("Glass Puzzle Directional Light");
            dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }
        dirLight.transform.rotation = Quaternion.Euler(directionalRotation);
        dirLight.intensity = directionalIntensity;
        dirLight.color = Color.white;
        dirLight.shadows = LightShadows.Soft;

        // 2. Setup Ambient Light
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = ambientColor;

        // 3. Setup Reflection Probe
        ReflectionProbe probe = FindFirstObjectByType<ReflectionProbe>();
        if (probe == null)
        {
            GameObject probeObj = new GameObject("Glass Reflection Probe");
            probe = probeObj.AddComponent<ReflectionProbe>();
        }
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.importance = 1;
        probe.intensity = reflectionIntensity;
        probe.boxProjection = true;
        probe.size = new Vector3(50, 50, 50);

        // 4. Setup Post-Processing Volume (Bloom)
        Volume volume = FindFirstObjectByType<Volume>();
        if (volume == null)
        {
            GameObject volObj = new GameObject("Glass PostProcess Volume");
            volume = volObj.AddComponent<Volume>();
            volume.isGlobal = true;
        }

        VolumeProfile profile = volume.sharedProfile;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "GlassPuzzleProfile";
            volume.sharedProfile = profile;
        }

        if (!profile.Has<Bloom>())
        {
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(0.7f);
        }
        else
        {
            if (profile.TryGet<Bloom>(out Bloom bloom))
            {
                bloom.intensity.Override(bloomIntensity);
                bloom.threshold.Override(bloomThreshold);
            }
        }

        Debug.Log("Glass Puzzle Environment Setup Complete!");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GlassPuzzleEnvironmentSetup))]
public class GlassPuzzleEnvironmentSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GlassPuzzleEnvironmentSetup setup = (GlassPuzzleEnvironmentSetup)target;
        if (GUILayout.Button("Setup Environment"))
        {
            setup.SetupEnvironment();
        }
    }
}
#endif
