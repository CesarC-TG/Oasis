using UnityEngine;

namespace Oasis.Enemy
{
    /// <summary>
    /// Procedural placeholder visuals for the Vaciado enemy.
    /// Builds a humanoid figure from Unity primitives with bioluminescent eyes.
    /// When a real 3D model is available, remove this component.
    /// </summary>
    [RequireComponent(typeof(EnemyDamageable))]
    public class EnemyVisuals : MonoBehaviour
    {
        [Header("Body Proportions")]
        public float TorsoHeight = 1.8f;
        public float TorsoRadius = 0.3f;
        public float HeadRadius = 0.35f;
        public float HeadYOffset = 1.2f;
        public float ArmLength = 0.8f;
        public float ArmRadius = 0.12f;
        public Vector3 ArmOffset = new Vector3(0.45f, 0.6f, 0f);
        public float LegLength = 0.9f;
        public float LegRadius = 0.15f;
        public Vector3 LegOffset = new Vector3(0.2f, -0.5f, 0f);

        [Header("Eye Glow")]
        public Color GlowColor = new Color(0.3f, 1f, 0.1f);
        public float EyeRadius = 0.07f;
        public float EyeOffsetX = 0.1f;
        public float EyeOffsetY = 0.07f;
        public float PulseSpeed = 3f;
        public float MinEmission = 0.5f;
        public float MaxEmission = 2f;

        [Header("Idle Animation")]
        public float BobAmplitude = 0.03f;
        public float BobSpeed = 1.5f;
        public float SwayAngle = 3f;
        public float SwaySpeed = 1f;

        [Header("Hit Flash")]
        public float FlashDuration = 0.1f;

        private Transform _torso;
        private Transform _head;
        private Renderer _eyeLeft, _eyeRight;
        private Material _eyeMatLeft, _eyeMatRight;
        private Material[] _bodyMaterials;
        private float _buildTime;
        private bool _isFlashing;

        void Awake()
        {
            // Guard: skip if artist already added real meshes (FBX with SkinnedMeshRenderer or static MeshFilter)
            bool hasRealMeshes = false;

            // Check root-level SkinnedMeshRenderer (FBX model may be on the root GameObject)
            if (GetComponentInChildren<SkinnedMeshRenderer>() != null)
                hasRealMeshes = true;

            if (!hasRealMeshes && transform.childCount > 0)
            {
                var firstChild = transform.GetChild(0);
                hasRealMeshes = firstChild.GetComponent<MeshFilter>() != null
                             || firstChild.GetComponent<SkinnedMeshRenderer>() != null;
                // Also check deeper for rigged FBX hierarchies
                if (!hasRealMeshes)
                    hasRealMeshes = firstChild.GetComponentInChildren<SkinnedMeshRenderer>() != null;
            }

            if (hasRealMeshes)
            {
                Debug.Log($"[EnemyVisuals] Real meshes detected on {gameObject.name}, skipping procedural build.");

                // Disable stray capsule MeshRenderer on the root that may have come
                // from an enemy prefab with a CapsuleCollider or procedural-remnant setup.
                var rootRenderer = GetComponent<MeshRenderer>();
                if (rootRenderer != null)
                    rootRenderer.enabled = false;

                return;
            }

            BuildVisuals();
            _buildTime = Time.time;
        }

        void Update()
        {
            if (_torso == null) return;

            AnimateIdle();
            PulseEyes();
        }

        void BuildVisuals()
        {
            // Torso
            _torso = CreatePrimitive(PrimitiveType.Capsule, "Torso", transform, Vector3.zero,
                new Vector3(TorsoRadius * 2f, TorsoHeight, TorsoRadius * 2f));

            // Head
            _head = CreatePrimitive(PrimitiveType.Sphere, "Head", _torso,
                new Vector3(0f, HeadYOffset, 0f), Vector3.one * HeadRadius * 2f);

            // Eyes
            _eyeLeft = CreatePrimitive(PrimitiveType.Sphere, "Eye_L", _head,
                new Vector3(-EyeOffsetX, EyeOffsetY, HeadRadius), Vector3.one * EyeRadius * 2f).GetComponent<Renderer>();
            _eyeRight = CreatePrimitive(PrimitiveType.Sphere, "Eye_R", _head,
                new Vector3(EyeOffsetX, EyeOffsetY, HeadRadius), Vector3.one * EyeRadius * 2f).GetComponent<Renderer>();

            _eyeMatLeft = CreateGlowMaterial();
            _eyeMatRight = CreateGlowMaterial();
            _eyeLeft.material = _eyeMatLeft;
            _eyeRight.material = _eyeMatRight;

            // Arms
            CreatePrimitive(PrimitiveType.Cylinder, "Arm_L", _torso,
                new Vector3(-ArmOffset.x, ArmOffset.y, 0f), new Vector3(ArmRadius * 2f, ArmLength, ArmRadius * 2f));
            CreatePrimitive(PrimitiveType.Cylinder, "Arm_R", _torso,
                new Vector3(ArmOffset.x, ArmOffset.y, 0f), new Vector3(ArmRadius * 2f, ArmLength, ArmRadius * 2f));

            // Legs
            CreatePrimitive(PrimitiveType.Cylinder, "Leg_L", _torso,
                new Vector3(-LegOffset.x, LegOffset.y, 0f), new Vector3(LegRadius * 2f, LegLength, LegRadius * 2f));
            CreatePrimitive(PrimitiveType.Cylinder, "Leg_R", _torso,
                new Vector3(LegOffset.x, LegOffset.y, 0f), new Vector3(LegRadius * 2f, LegLength, LegRadius * 2f));

            // Body material (dark gray)
            var bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMat.color = new Color(0.15f, 0.15f, 0.17f);
            ApplyMaterialToChildren(_torso, bodyMat);
        }

        Transform CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            // Remove collider (parent EnemyDamageable handles it)
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go.transform;
        }

        Material CreateGlowMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = GlowColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", GlowColor * MaxEmission);
            return mat;
        }

        void ApplyMaterialToChildren(Transform parent, Material mat)
        {
            foreach (var r in parent.GetComponentsInChildren<Renderer>())
            {
                // Don't override eyes
                if (r == _eyeLeft || r == _eyeRight) continue;
                r.material = mat;
            }
        }

        void AnimateIdle()
        {
            float t = Time.time - _buildTime;
            float bob = Mathf.Sin(t * BobSpeed) * BobAmplitude;
            float sway = Mathf.Sin(t * SwaySpeed) * SwayAngle;

            _torso.localPosition = new Vector3(0f, bob, 0f);
            _torso.localRotation = Quaternion.Euler(0f, 0f, sway);
        }

        void PulseEyes()
        {
            if (_eyeMatLeft == null || _eyeMatRight == null) return;
            float pulse = Mathf.Lerp(MinEmission, MaxEmission,
                (Mathf.Sin(Time.time * PulseSpeed) + 1f) / 2f);

            var color = GlowColor * pulse;
            _eyeMatLeft.SetColor("_EmissionColor", color);
            _eyeMatRight.SetColor("_EmissionColor", color);
        }

        public void FlashHit()
        {
            if (_isFlashing) return;
            StartCoroutine(FlashRoutine());
        }

        System.Collections.IEnumerator FlashRoutine()
        {
            _isFlashing = true;

            // Fallback to root when _torso is null (real meshes, no procedural build).
            // Uses Renderer base type to catch both MeshRenderer and SkinnedMeshRenderer.
            Renderer[] renderers = _torso != null
                ? _torso.GetComponentsInChildren<Renderer>()
                : GetComponentsInChildren<Renderer>();

            foreach (Renderer r in renderers)
                r.material.color = Color.red;

            yield return new WaitForSeconds(FlashDuration);

            renderers = _torso != null
                ? _torso.GetComponentsInChildren<Renderer>()
                : GetComponentsInChildren<Renderer>();

            foreach (Renderer r in renderers)
                r.material.color = new Color(0.15f, 0.15f, 0.17f);

            _isFlashing = false;
        }
    }
}
