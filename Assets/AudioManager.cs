using UnityEngine;

// Первая в проекте общая система звука (до этого — только точечные AudioClip-поля на HeroCardUI/BattleUI,
// см. FloatingStatusIcon.Spawn). Бутстрапится сама через RuntimeInitializeOnLoadMethod — не нужно ничего
// класть в сцену руками (как остальные синглтоны — HeroCollectionManager и т.п.), поэтому и настраивать
// через Inspector нечего: клипы грузятся по пути из Resources, та же конвенция, что у всех спрайтов
// ConfirmationDialog (Resources.Load<Sprite>("UI/...")).
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
    }

    // Общий одноразовый звук — clip == null (файл ещё не положен/не нашёлся) тихо ничего не играет,
    // ровно так же как неназначенные AudioClip-поля HeroCardUI (shieldSound и т.д.) сегодня.
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null || source == null) return;
        source.PlayOneShot(clip, volume);
    }

    private AudioClip upgradeSfx;
    private bool upgradeSfxLoadAttempted;

    // "Сочный дорогой звук клика" прокачки (см. UX-бриф) — HeroInventoryUI.AnimateStatGain и
    // ItemSacrificeUI.ApplySacrifice оба зовут это на успешной прокачке. Положить файл в
    // Assets/Resources/Audio/UpgradeSuccess.(wav|mp3|ogg) — заработает само, без единой правки кода.
    public void PlayUpgradeSound()
    {
        if (!upgradeSfxLoadAttempted)
        {
            upgradeSfx = Resources.Load<AudioClip>("Audio/UpgradeSuccess");
            upgradeSfxLoadAttempted = true;
        }
        PlaySfx(upgradeSfx);
    }
}
