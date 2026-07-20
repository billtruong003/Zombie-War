using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Nguồn số dư tiền tệ cho UI. WalletService (E1) sẽ implement; trước đó dùng
    /// PlayerPrefsCurrencyProvider để screen dựng trước với data thật-persist.
    /// </summary>
    public interface ICurrencyProvider
    {
        long Coin { get; }
        long Gold { get; }
        long Gem { get; }
        /// <summary>Bắn khi bất kỳ số dư nào đổi.</summary>
        event System.Action Changed;
    }

    /// <summary>Mock provider persist bằng PlayerPrefs — thay bằng WalletService ở E1.</summary>
    public sealed class PlayerPrefsCurrencyProvider : ICurrencyProvider
    {
        public event System.Action Changed;
        public long Coin => PlayerPrefs.GetInt("wallet_coin", 0);
        public long Gold => PlayerPrefs.GetInt("wallet_gold", 0);
        public long Gem => PlayerPrefs.GetInt("wallet_gem", 0);

        public void Add(string key, int amount)
        {
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + amount);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Cluster Coin/Gold/Gem góc phải trên (wireframe màn 01/04-08).
    /// Gắn sẵn 3 cặp icon+label; tự refresh khi provider bắn Changed.
    /// </summary>
    public sealed class CurrencyClusterWidget : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text gemLabel;

        private ICurrencyProvider _provider;

        /// <summary>Static default để mọi widget dùng chung khi chưa có DI (E1 sẽ thay).</summary>
        public static ICurrencyProvider DefaultProvider { get; set; } = new PlayerPrefsCurrencyProvider();

        private void OnEnable()
        {
            _provider ??= DefaultProvider;
            _provider.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_provider != null) _provider.Changed -= Refresh;
        }

        public void Bind(ICurrencyProvider provider)
        {
            if (_provider != null) _provider.Changed -= Refresh;
            _provider = provider;
            if (isActiveAndEnabled) { _provider.Changed += Refresh; Refresh(); }
        }

        private void Refresh()
        {
            if (coinLabel != null) coinLabel.text = Format(_provider.Coin);
            if (goldLabel != null) goldLabel.text = Format(_provider.Gold);
            if (gemLabel != null) gemLabel.text = Format(_provider.Gem);
        }

        /// <summary>1234567 → "1.23M", 12345 → "12.3K" — khớp wireframe compact.</summary>
        public static string Format(long v) => v switch
        {
            >= 1_000_000 => (v / 1_000_000f).ToString("0.##") + "M",
            >= 10_000 => (v / 1_000f).ToString("0.#") + "K",
            _ => v.ToString("N0"),
        };
    }
}
