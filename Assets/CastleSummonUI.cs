using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime-built summon window shared by the Forge (items) and the Altar (heroes).
// Pay with Shards (no pity) or Premium Gems (with pity, per-pool counter from SummonService).
// x1 and x10 pulls available for both currencies.
public class CastleSummonUI : MonoBehaviour
{
    private const int MultiPullCount = 10;

    private Transform canvasRoot;
    private GameObject overlayRoot;
    private System.Action onClosed;

    private BuildingData building;

    private TMP_Text titleText;
    private TMP_Text infoText;
    private TMP_Text shardsX1Text;
    private TMP_Text shardsX10Text;
    private TMP_Text premiumX1Text;
    private TMP_Text premiumX10Text;

    public void Open(BuildingData building, Transform canvasRoot, System.Action onClosed)
    {
        this.building = building;
        this.canvasRoot = canvasRoot;
        this.onClosed = onClosed;

        EnsureOverlay();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        onClosed?.Invoke();
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("CastleSummonOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        var closeAreaObj = new GameObject("CloseArea", typeof(RectTransform));
        var closeAreaRect = (RectTransform)closeAreaObj.transform;
        closeAreaRect.SetParent(overlayRect, false);
        closeAreaRect.anchorMin = Vector2.zero;
        closeAreaRect.anchorMax = Vector2.one;
        closeAreaRect.offsetMin = Vector2.zero;
        closeAreaRect.offsetMax = Vector2.zero;
        var closeAreaImg = closeAreaObj.AddComponent<Image>();
        closeAreaImg.color = new Color(0, 0, 0, 0);
        var closeAreaBtn = closeAreaObj.AddComponent<Button>();
        closeAreaBtn.transition = Selectable.Transition.None;
        closeAreaBtn.onClick.AddListener(Close);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(480, 460);
        var windowBg = windowObj.AddComponent<Image>();
        windowBg.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 44);
        titleRect.anchoredPosition = new Vector2(0, -12);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 26;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        var infoObj = new GameObject("Info", typeof(RectTransform));
        var infoRect = (RectTransform)infoObj.transform;
        infoRect.SetParent(windowRect, false);
        infoRect.anchorMin = new Vector2(0, 0.62f);
        infoRect.anchorMax = new Vector2(1, 0.85f);
        infoRect.offsetMin = new Vector2(24, 0);
        infoRect.offsetMax = new Vector2(-24, 0);
        infoText = infoObj.AddComponent<TextMeshProUGUI>();
        infoText.fontSize = 17;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = new Color(1, 1, 1, 0.85f);

        // Ліва колонка — Shards (x1 зверху, x10 знизу); права колонка — Premium Gems
        shardsX1Text = CreateBigButton(windowRect, new Vector2(0.27f, 0.46f), () => Pull(false, 1), out _);
        shardsX10Text = CreateBigButton(windowRect, new Vector2(0.27f, 0.27f), () => Pull(false, MultiPullCount), out _);
        premiumX1Text = CreateBigButton(windowRect, new Vector2(0.73f, 0.46f), () => Pull(true, 1), out _);
        premiumX10Text = CreateBigButton(windowRect, new Vector2(0.73f, 0.27f), () => Pull(true, MultiPullCount), out _);

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(160, 34);
        closeBtnRect.anchoredPosition = new Vector2(0, 12);
        var closeBtnImg = closeBtnObj.AddComponent<Image>();
        closeBtnImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        var closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(Close);

        var closeTextObj = new GameObject("Text", typeof(RectTransform));
        var closeTextRect = (RectTransform)closeTextObj.transform;
        closeTextRect.SetParent(closeBtnRect, false);
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeText.text = "Close";
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = Color.white;

        overlayRoot.SetActive(false);
    }

    private TMP_Text CreateBigButton(RectTransform parent, Vector2 anchorPos, UnityEngine.Events.UnityAction onClick, out Button button)
    {
        var btnObj = new GameObject("PullButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = anchorPos;
        btnRect.anchorMax = anchorPos;
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(170, 56);

        var img = btnObj.AddComponent<Image>();
        img.color = ConfirmationDialog.ButtonColor;
        button = btnObj.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 15;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;

        return text;
    }

    private bool IsAltar => building != null && building.buildingType == BuildingType.Altar;

    private void Refresh()
    {
        if (building == null) return;

        titleText.text = building.buildingName;

        string poolId;
        int shardCost, premiumCost, pityThreshold;
        bool hasPity;

        if (IsAltar)
        {
            var pool = building.heroSummonPool;
            if (pool == null) { infoText.text = "No summon pool assigned."; return; }
            poolId = pool.poolId; shardCost = pool.shardCost; premiumCost = pool.premiumCost;
            hasPity = pool.hasPity; pityThreshold = pool.pityThreshold;
        }
        else
        {
            var pool = building.itemSummonPool;
            if (pool == null) { infoText.text = "No summon pool assigned."; return; }
            poolId = pool.poolId; shardCost = pool.shardCost; premiumCost = pool.premiumCost;
            hasPity = pool.hasPity; pityThreshold = pool.pityThreshold;
        }

        int pityCount = SummonService.Instance != null
            ? SummonService.Instance.pityStates.FirstOrDefault(p => p.poolId == poolId)?.pullsSincePity ?? 0
            : 0;

        int shardBalance = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.SummonShards) : 0;
        int gemBalance = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.PremiumGems) : 0;

        string pityLine = hasPity ? $"\nPremium pity: {pityCount}/{pityThreshold}" : "";
        infoText.text = $"Your balance: {shardBalance} Shards, {gemBalance} Gems{pityLine}";

        shardsX1Text.text = $"Pull x1\n({shardCost} Shards)";
        shardsX10Text.text = $"Pull x{MultiPullCount}\n({shardCost * MultiPullCount} Shards)";
        premiumX1Text.text = $"Pull x1\n({premiumCost} Gems)";
        premiumX10Text.text = $"Pull x{MultiPullCount}\n({premiumCost * MultiPullCount} Gems)";
    }

    private void Pull(bool premium, int count)
    {
        if (SummonService.Instance == null) return;

        if (IsAltar)
        {
            var results = count == 1
                ? SinglePullAsList(SummonService.Instance.PullHero(building.heroSummonPool, premium))
                : SummonService.Instance.PullHeroMultiple(building.heroSummonPool, premium, count);

            ShowResult(results.Count, count, results.Select(h => (h.heroName, h.rarity.ToString())));
        }
        else
        {
            var results = count == 1
                ? SinglePullAsList(SummonService.Instance.PullItem(building.itemSummonPool, premium))
                : SummonService.Instance.PullItemMultiple(building.itemSummonPool, premium, count);

            ShowResult(results.Count, count, results.Select(i => (i.itemName, i.rarity.ToString())));
        }

        Refresh();
    }

    private static List<T> SinglePullAsList<T>(T result) where T : class
    {
        var list = new List<T>();
        if (result != null) list.Add(result);
        return list;
    }

    private void ShowResult(int obtainedCount, int requestedCount, IEnumerable<(string name, string rarity)> items)
    {
        if (obtainedCount == 0)
        {
            ConfirmationDialog.ShowInfo(canvasRoot, "Not enough currency for this pull.");
            return;
        }

        var grouped = items
            .GroupBy(i => (i.name, i.rarity))
            .Select(g => $"{g.Key.name} ({g.Key.rarity}) x{g.Count()}")
            .ToList();

        var sb = new StringBuilder("You got:\n");
        sb.Append(string.Join("\n", grouped));

        if (obtainedCount < requestedCount)
            sb.Append($"\n\nRan out of currency after {obtainedCount}/{requestedCount} pulls.");

        float height = Mathf.Max(170, 110 + grouped.Count * 24);
        ConfirmationDialog.ShowInfo(canvasRoot, sb.ToString(), height);
    }
}
