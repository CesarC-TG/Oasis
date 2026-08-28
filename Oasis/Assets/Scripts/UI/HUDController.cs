using UnityEngine;
using UnityEngine.UI;
using Oasis.Combat;

public class HUDController : MonoBehaviour
{
    [Header("Fills (los hijos de cada background)")]
    public RectTransform hpFill;
    public RectTransform staminaFill;

    [Header("Imagen de stamina para cambiar color")]
    public Image staminaImage;

    [Header("Colores stamina")]
    public Color staminaNormal = new Color(0.2f, 0.8f, 0.3f);
    public Color staminaExhausted = new Color(0.8f, 0.3f, 0.2f);

    [Header("Feedback de daño")]
    public Image damageFlash;          // flash rojo en pantalla al recibir daño
    public float flashDuration = 0.15f;

    private PlayerStats _stats;

    void Awake()
    {
        _stats = FindFirstObjectByType<PlayerStats>();
    }

    void OnEnable()
    {
        CombatEventBus.OnDamageDealt += OnDamageDealt;
        CombatEventBus.OnEntityKilled += OnEntityKilled;
    }

    void OnDisable()
    {
        CombatEventBus.OnDamageDealt -= OnDamageDealt;
        CombatEventBus.OnEntityKilled -= OnEntityKilled;
    }

    void Update()
    {
        if (_stats == null) return;

        SetFill(hpFill, _stats.CurrentHP / _stats.maxHP);
        SetFill(staminaFill, _stats.CurrentStamina / _stats.maxStamina);

        if (staminaImage != null)
            staminaImage.color = _stats.IsExhausted ? staminaExhausted : staminaNormal;
    }

    void OnDamageDealt(DamageData data, float netDamage)
    {
        // Flash rojo si el jugador recibe daño
        if (data.Source != null && data.Source != _stats.gameObject && damageFlash != null)
        {
            StopAllCoroutines();
            StartCoroutine(DamageFlashRoutine());
        }
    }

    void OnEntityKilled(GameObject entity)
    {
        if (entity == null) return;
        Debug.Log($"[HUD] Entity killed: {entity.name}");
    }

    System.Collections.IEnumerator DamageFlashRoutine()
    {
        if (damageFlash == null) yield break;
        damageFlash.enabled = true;
        yield return new WaitForSeconds(flashDuration);
        damageFlash.enabled = false;
    }

    void SetFill(RectTransform rect, float amount)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
