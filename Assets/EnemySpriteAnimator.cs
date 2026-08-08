using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Покадровая 2D-анимация врага поверх обычного enemyPortrait (Image) — своя ручная корутина, тот же
// стиль, что уже используется в GridManager (PopInAnimation/PlayDestroyAnimation) и FloatingDamageText:
// в проекте нет ни Animator, ни какой-либо другой анимационной инфраструктуры, это первая. Без кадров
// (EnemyData.idleFrames/attackFrames пустые) просто показывает статичный EnemyData.portrait, как раньше
// — полностью обратно совместимо со старыми EnemyData без анимации.
public class EnemySpriteAnimator : MonoBehaviour
{
    public Image target;

    private Sprite[] idleFrames;
    private Sprite[] attackFrames;
    private float frameDuration = 1f / 8f;
    private Coroutine playingRoutine;

    // Реакция на урон — процедурная (вспышка цвета + тряска), не завязана на кадры, поэтому работает
    // для ЛЮБОГО врага сразу, даже без спрайт-листа вообще. Идёт ОТДЕЛЬНОЙ корутиной от
    // playingRoutine (та трогает только .sprite, эта — .color/anchoredPosition), поэтому не прерывает
    // текущий Idle/Attack цикл кадров.
    private RectTransform rectTransform;
    private Vector2 basePosition;
    private bool basePositionCaptured;
    private Coroutine hitRoutine;

    public void SetEnemy(EnemyData enemy)
    {
        StopAnim();

        // Без этого разные враги растягивались до 400x700 без сохранения пропорций (Preserve Aspect был
        // выключен на самой сцене) — форсируем тут в коде, чтобы не зависеть от ручной настройки Inspector.
        // Сам размер/позиция картинки — Width/Height/Scale/Pos на EnemyPortrait в Inspector (Edit Mode),
        // код их больше не трогает и не перезатирает при входе в новый бой.
        if (target != null)
            target.preserveAspect = true;

        idleFrames = (enemy != null && enemy.idleFrames != null && enemy.idleFrames.Length > 0) ? enemy.idleFrames : null;
        attackFrames = (enemy != null && enemy.attackFrames != null && enemy.attackFrames.Length > 0) ? enemy.attackFrames : null;
        frameDuration = (enemy != null && enemy.animationFPS > 0f) ? 1f / enemy.animationFPS : 1f / 8f;

        if (target == null) return;

        if (idleFrames != null)
            PlayIdle();
        else if (enemy != null && enemy.portrait != null)
            target.sprite = enemy.portrait; // старое поведение — один статичный кадр
    }

    public void PlayIdle()
    {
        if (target == null || idleFrames == null) return;
        StopAnim();
        playingRoutine = StartCoroutine(LoopFrames(idleFrames));
    }

    // Проигрывает атаку один раз, потом сама возвращается на Idle (если он есть).
    public void PlayAttack()
    {
        if (target == null || attackFrames == null) return;
        StopAnim();
        playingRoutine = StartCoroutine(PlayOnceThenIdle(attackFrames));
    }

    // Красная вспышка + горизонтальная тряска на получение урона — вызывать из BattleManager.OnEnemyDamaged.
    public void PlayHitReaction()
    {
        if (target == null) return;

        if (hitRoutine != null) StopCoroutine(hitRoutine);
        hitRoutine = StartCoroutine(HitReactionRoutine());
    }

    private void StopAnim()
    {
        if (playingRoutine != null)
        {
            StopCoroutine(playingRoutine);
            playingRoutine = null;
        }
    }

    private IEnumerator LoopFrames(Sprite[] frames)
    {
        int i = 0;
        while (true)
        {
            target.sprite = frames[i];
            i = (i + 1) % frames.Length;
            yield return new WaitForSeconds(frameDuration);
        }
    }

    private IEnumerator PlayOnceThenIdle(Sprite[] frames)
    {
        foreach (var frame in frames)
        {
            target.sprite = frame;
            yield return new WaitForSeconds(frameDuration);
        }
        PlayIdle();
    }

    private IEnumerator HitReactionRoutine()
    {
        if (rectTransform == null) rectTransform = target.rectTransform;
        if (!basePositionCaptured)
        {
            basePosition = rectTransform.anchoredPosition;
            basePositionCaptured = true;
        }

        Color baseColor = target.color;
        Color flashColor = new Color(1f, 0.25f, 0.25f, baseColor.a);

        const float duration = 0.25f;
        const float shakeMagnitude = 14f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float damp = 1f - t; // тряска и вспышка затухают к концу

            target.color = Color.Lerp(flashColor, baseColor, t);
            rectTransform.anchoredPosition = basePosition + new Vector2(Random.Range(-1f, 1f) * shakeMagnitude * damp, 0f);

            yield return null;
        }

        target.color = baseColor;
        rectTransform.anchoredPosition = basePosition;
        hitRoutine = null;
    }

    private void OnDisable()
    {
        StopAnim();
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }
        if (basePositionCaptured && rectTransform != null)
            rectTransform.anchoredPosition = basePosition;
    }
}
