using UnityEngine;
using UnityEngine.UI;

namespace Oasis.UI
{
    /// <summary>
    /// Object-pooled floating damage numbers.
    /// Spawn above hit points, rise and fade over ~1 second.
    /// Usage: DamageNumber.Spawn(worldPosition, damage, isCrit)
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        public Text Label;
        public CanvasGroup CanvasGroup;
        public RectTransform Rect;

        [Header("Animation")]
        public float RiseHeight = 80f;
        public float Duration = 1f;
        public AnimationCurve RiseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Colors")]
        public Color NormalColor = Color.white;
        public Color CritColor = Color.yellow;
        public Color EnemyColor = Color.red;

        private Vector3 _worldStart;
        private Camera _camera;
        private float _elapsed;
        private Vector2 _screenOffset;

        // === Pool ===
        private static DamageNumber[] _pool;
        private static int _poolSize = 20;
        private static int _nextIndex;
        private static Transform _poolRoot;

        public static void Spawn(Vector3 worldPosition, float damage, bool isCrit = false, bool isEnemy = false)
        {
            InitPool();
            var instance = _pool[_nextIndex];
            _nextIndex = (_nextIndex + 1) % _poolSize;

            instance.gameObject.SetActive(true);
            instance._worldStart = worldPosition;
            instance._elapsed = 0f;
            instance._screenOffset = Vector2.zero;

            instance.Label.text = Mathf.RoundToInt(damage).ToString();
            instance.Label.color = isCrit ? instance.CritColor
                                 : isEnemy ? instance.EnemyColor
                                 : instance.NormalColor;
            instance.Label.fontSize = isCrit ? 32 : 24;

            instance.CanvasGroup.alpha = 1f;
        }

        static void InitPool()
        {
            if (_pool != null) return;

            _pool = new DamageNumber[_poolSize];
            _poolRoot = new GameObject("DamageNumberPool").transform;
            _poolRoot.SetParent(null);

            // Load prefab from Resources or create at runtime
            var prefab = Resources.Load<GameObject>("DamageNumber");
            for (int i = 0; i < _poolSize; i++)
            {
                GameObject go;
                if (prefab != null)
                    go = Instantiate(prefab, _poolRoot);
                else
                    go = CreateDefault(_poolRoot);

                go.name = $"DamageNumber_{i}";
                go.SetActive(false);
                _pool[i] = go.GetComponent<DamageNumber>();
            }
        }

        static GameObject CreateDefault(Transform parent)
        {
            var go = new GameObject("DamageNumber", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(CanvasGroup), typeof(DamageNumber));
            go.transform.SetParent(parent);

            // Add to a canvas
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) go.transform.SetParent(canvas.transform);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 40);

            var txt = go.GetComponent<Text>();
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 24;
            txt.color = Color.white;

            var cg = go.GetComponent<CanvasGroup>();

            var dn = go.GetComponent<DamageNumber>();
            dn.Label = txt;
            dn.CanvasGroup = cg;
            dn.Rect = rt;

            return go;
        }

        void Awake()
        {
            _camera = Camera.main;
        if (_camera == null)
            _camera = FindFirstObjectByType<Camera>();
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;

            _elapsed += Time.deltaTime;
            float t = _elapsed / Duration;

            // Rise
            float rise = RiseCurve.Evaluate(t) * RiseHeight;
            _screenOffset.y = rise;

            // Fade
            if (CanvasGroup != null)
                CanvasGroup.alpha = 1f - t;

            // World to screen position
            if (_camera != null && Rect != null)
            {
                Vector3 screenPos = _camera.WorldToScreenPoint(_worldStart);
                Rect.anchoredPosition = new Vector2(screenPos.x, screenPos.y) + _screenOffset;
            }

            // Recycle
            if (t >= 1f)
                gameObject.SetActive(false);
        }
    }
}
