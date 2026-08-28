using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a fly-through camera route over the Oasis map.
/// Hides the player/HUD, dims the sun to night mood, then restores everything.
/// </summary>
public class OasisCinematic : MonoBehaviour
{
    [Header("Route")]
    public Vector3[] shotPositions = System.Array.Empty<Vector3>();
    public string[] shotLookTargets = System.Array.Empty<string>();
    public Vector3[] shotLookOffsets = System.Array.Empty<Vector3>();
    public float perShot = 4.5f;
    public float endHold = 2.5f;
    public float swayAmp = 0.5f;

    [Header("Setup (automatically bound)")]
    public Camera boundCamera;

    private readonly List<Vector3> _looks = new List<Vector3>();
    private int _idx;
    private float _t;
    private float _holdT;
    private bool _done;
    private Vector3 _lastPos;
    private Vector3 _lastLook;
    private GameObject _playerRoot;
    private GameObject _hud;
    private Light _sun;
    private float _sunIntensity;
    private Color _sunColor;

    void Awake()
    {
        if (boundCamera == null) boundCamera = GetComponentInChildren<Camera>();
        _looks.Clear();
        for (int i = 0; i < shotPositions.Length; i++)
        {
            Transform target = null;
            if (i < shotLookTargets.Length && !string.IsNullOrEmpty(shotLookTargets[i]))
            {
                GameObject go = GameObject.Find(shotLookTargets[i]);
                if (go != null) target = go.transform;
            }
            Vector3 look = target != null ? target.position : Vector3.zero;
            if (i < shotLookOffsets.Length) look += shotLookOffsets[i];
            _looks.Add(look);
        }
    }

    void Start()
    {
        if (boundCamera == null)
        {
            Debug.LogWarning("[Oasis] Cinematic has no camera bound.");
            return;
        }

        _playerRoot = GameObject.Find("Player");
        _hud = GameObject.Find("HUD");
        if (_playerRoot != null) _playerRoot.SetActive(false);
        if (_hud != null) _hud.SetActive(false);

        Camera pc = GameObject.Find("PlayerCamera")?.GetComponent<Camera>();
        if (pc != null) pc.enabled = false;

        _sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (_sun != null)
        {
            _sunIntensity = _sun.intensity;
            _sunColor = _sun.color;
            _sun.intensity = 0.15f;
            _sun.color = new Color(0.45f, 0.5f, 0.7f);
        }

        AudioListener al = boundCamera.GetComponent<AudioListener>();
        if (al != null) al.enabled = true;
        boundCamera.enabled = true;

        _idx = 0;
        _t = 0f;
        _holdT = 0f;
        _done = false;
    }

    void Update()
    {
        if (boundCamera == null || _done) return;
        _t += Time.unscaledDeltaTime;

        if (shotPositions.Length == 0) { Finish(); return; }

        if (_idx < shotPositions.Length - 1)
        {
            Advance();
        }
        else
        {
            // Hold last shot, then restore controls
            ApplyPose(_lastPos, _lastLook);
            _holdT += Time.unscaledDeltaTime;
            if (_holdT >= endHold) Finish();
        }
    }

    void Advance()
    {
        int seg = _idx;
        Vector3[] pts = ShotPoints(seg);

        float u = Mathf.Clamp01(_t / perShot);
        u = u * u * (3f - 2f * u);
        Vector3 pos = Catmull(pts[0], pts[1], pts[2], pts[3], u);
        pos.y += Mathf.Sin(_t * 2.4f) * swayAmp * 0.4f;

        Vector3 look = LookAt(seg);
        look.x += Mathf.Sin(_t * 0.21f) * 6f;
        look.y += Mathf.Sin(_t * 0.31f) * 2f;

        ApplyPose(pos, look);

        if (_t >= perShot)
        {
            _t -= perShot;
            _idx++;
        }
    }

    Vector3 LookAt(int seg)
    {
        int lookIdx = Mathf.Clamp(seg, 0, _looks.Count - 1);
        return _looks.Count > 0 ? _looks[lookIdx] : Vector3.zero;
    }

    Vector3[] ShotPoints(int seg)
    {
        var outp = new Vector3[4];
        for (int k = -1; k <= 2; k++)
        {
            int c = Mathf.Clamp(seg + k, 0, shotPositions.Length - 1);
            outp[k + 1] = shotPositions[c];
        }
        return outp;
    }

    static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void ApplyPose(Vector3 pos, Vector3 look)
    {
        if (pos.sqrMagnitude < 0.001f && shotPositions.Length > 0) pos = shotPositions[0];
        boundCamera.transform.position = pos;
        boundCamera.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        _lastPos = pos;
        _lastLook = look;
    }

    void Finish()
    {
        if (_done) return;
        _done = true;

        if (_playerRoot != null) _playerRoot.SetActive(true);
        if (_hud != null) _hud.SetActive(true);
        Camera pc = GameObject.Find("PlayerCamera")?.GetComponent<Camera>();
        if (pc != null) pc.enabled = true;
        if (_sun != null)
        {
            _sun.intensity = _sunIntensity;
            _sun.color = _sunColor;
        }
        AudioListener al = boundCamera.GetComponent<AudioListener>();
        if (al != null) al.enabled = false;
        boundCamera.enabled = false;
        Debug.Log("[Oasis] Cinematic finished. Controls restored.");
    }
}