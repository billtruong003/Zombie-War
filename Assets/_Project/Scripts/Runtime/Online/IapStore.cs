#if UNITY_ANDROID && !UNITY_EDITOR
#define HC_IAP
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
#if HC_IAP
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine.Purchasing;
#endif

namespace ZombieWar.Online
{
    /// <summary>
    /// Google Play Billing through Unity IAP 5 (06/10). A purchase is granted only after our server has
    /// checked it with Google (POST /v1/iap/verify) and only once per purchase token on this device;
    /// then it is confirmed with the store (consumables are consumed). When the server or Google cannot
    /// be reached the purchase stays pending and comes back on the next launch, so nothing paid is lost
    /// and nothing unpaid is granted. Until the server has its Play key it answers 501 and the game
    /// trusts the store. The products themselves are created in Play Console with the same ids.
    /// </summary>
    public static class IapStore
    {
        /// <summary>Product ids and whether each is used up when granted (gems) or owned for good.</summary>
        public static readonly (string id, bool consumable)[] Catalog =
        {
            (PassRewards.PremiumProductId, true),   // one season at a time: bought again every season
            ("pack.starter", false),
            ("pack.gems80", true),
            ("pack.gems440", true),
            ("pack.gems950", true),
            ("pack.gems2600", true),
            ("pack.noads", false),
            ("pack.legend", false),
        };

        const string GrantedKey = "hc.iap.granted";

        [Serializable] class VerifyRequest { public string productId, purchaseToken, orderId; }
        [Serializable] class VerifyReply { public bool valid, duplicate; }

        public enum Verdict { Grant, AlreadyGranted, Refuse, TryLater }

        /// <summary>What to do with a purchase given the server's answer (code 0 = no answer).</summary>
        public static Verdict Decide(long code, bool duplicate, bool grantedHere)
        {
            if (code == 200) return grantedHere ? Verdict.AlreadyGranted : Verdict.Grant;
            if (code == 501) return grantedHere ? Verdict.AlreadyGranted : Verdict.Grant;   // no Play key on the server yet
            if (code == 402 || code == 409) return Verdict.Refuse;                          // not paid, or another player's token
            return Verdict.TryLater;                                                        // offline, 401, 5xx
        }

        static HashSet<string> Granted()
        {
            var set = new HashSet<string>();
            foreach (var t in PlayerPrefs.GetString(GrantedKey, "").Split('\n'))
                if (t.Length > 0) set.Add(t);
            return set;
        }

        static void MarkGranted(string token)
        {
            var list = new List<string>(PlayerPrefs.GetString(GrantedKey, "").Split('\n'));
            list.RemoveAll(t => t.Length == 0);
            list.Add(token);
            if (list.Count > 200) list.RemoveRange(0, list.Count - 200);
            PlayerPrefs.SetString(GrantedKey, string.Join("\n", list));
            PlayerPrefs.Save();
        }

        static readonly Dictionary<string, Action<bool>> Waiting = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Waiting.Clear();

#if HC_IAP
        static StoreController _store;
        static bool _ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Boot()
        {
            try { await UnityServices.InitializeAsync(); }
            catch (Exception e) { Debug.LogWarning("[IAP] Unity Services: " + e.Message); }
            _store = UnityIAPServices.StoreController();
            _store.OnPurchasePending += order => _ = OnPending(order);
            _store.OnPurchaseFailed += OnFailed;
            _store.OnProductsFetched += _ => { _ready = true; _store.ProcessPendingOrdersOnPurchasesFetched(true); _store.FetchPurchases(); };
            _store.OnProductsFetchFailed += f => Debug.LogWarning("[IAP] Products: " + f.FailureReason);
            _store.OnPurchasesFetched += OnPurchasesFetched;
            try
            {
                await _store.Connect();
                var defs = new List<ProductDefinition>();
                foreach (var (id, consumable) in Catalog) defs.Add(new ProductDefinition(id, consumable ? ProductType.Consumable : ProductType.NonConsumable));
                _store.FetchProducts(defs);
            }
            catch (Exception e) { Debug.LogWarning("[IAP] Store: " + e.Message); }
        }

        public static string PriceOf(string productId) =>
            _ready ? _store.GetProductById(productId)?.metadata?.localizedPriceString : null;

        public static bool Buy(string productId, Action<bool> done)
        {
            if (!_ready || _store.GetProductById(productId) is not { availableToPurchase: true }) return false;
            // A second tap while the store sheet opens would start a second purchase (07/10).
            if (Waiting.ContainsKey(productId)) return true;
            Waiting[productId] = done;
            GameAnalytics.Log("purchase_start", ("product", productId));
            _store.PurchaseProduct(productId);
            return true;
        }

        static string ProductOf(Order order)
        {
            var items = order.CartOrdered.Items();
            return items.Count > 0 ? items[0].Product.definition.id : null;
        }

        static async Task OnPending(PendingOrder order)
        {
            string productId = ProductOf(order);
            string token = order.Info.Google?.PurchaseToken ?? order.Info.TransactionID;
            if (productId == null || string.IsNullOrEmpty(token)) return;
            var r = await ApiClient.Call("POST", "/v1/iap/verify", JsonUtility.ToJson(new VerifyRequest
            {
                productId = productId, purchaseToken = token, orderId = order.Info.Google?.OrderId,
            }));
            bool duplicate = r.Ok && JsonUtility.FromJson<VerifyReply>(r.Body) is { duplicate: true };
            var verdict = Decide(r.Code, duplicate, Granted().Contains(token));
            switch (verdict)
            {
                case Verdict.Grant:
                    MarkGranted(token);   // first: a throwing UI handler must not lead to a second grant
                    try { Purchases.Grant(productId); }
                    catch (Exception e) { Debug.LogException(e); }
                    GameAnalytics.Log("purchase", ("product", productId));
                    _store.ConfirmPurchase(order);
                    Finish(productId, true);
                    break;
                case Verdict.AlreadyGranted:
                    _store.ConfirmPurchase(order);
                    Finish(productId, true);
                    break;
                case Verdict.Refuse:
                    GameAnalytics.Log("purchase_refused", ("product", productId), ("code", r.Code));
                    _store.ConfirmPurchase(order);
                    Finish(productId, false);
                    break;
                default:
                    // Left pending on purpose: the store hands it back on the next launch.
                    UI.Toast.Show("Purchase received - it will arrive once you are online");
                    Finish(productId, false, quiet: true);
                    break;
            }
        }

        static void OnFailed(FailedOrder order)
        {
            string productId = ProductOf(order);
            if (order.FailureReason != PurchaseFailureReason.UserCancelled)
                GameAnalytics.Log("purchase_failed", ("product", productId ?? ""), ("reason", order.FailureReason.ToString()));
            if (productId != null) Finish(productId, false, quiet: order.FailureReason == PurchaseFailureReason.UserCancelled);
        }

        /// <summary>Owned items come back after a reinstall: grant what this install has not granted.</summary>
        static void OnPurchasesFetched(Orders orders)
        {
            var granted = Granted();
            foreach (var order in orders.ConfirmedOrders)
            {
                string productId = ProductOf(order);
                string token = order.Info.Google?.PurchaseToken ?? order.Info.TransactionID;
                if (productId == null || string.IsNullOrEmpty(token) || granted.Contains(token)) continue;
                if (Array.Exists(Catalog, c => c.id == productId && !c.consumable))
                {
                    MarkGranted(token);
                    try { Purchases.Grant(productId); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        static void Finish(string productId, bool ok, bool quiet = false)
        {
            if (Waiting.Remove(productId, out var done)) done?.Invoke(ok);
            // Screens only refresh on success, so the failure is said here whoever waited (07/10:
            // a network or server failure was silent).
            if (!ok && !quiet) UI.Toast.Show("The purchase did not go through");
        }
#else
        public static string PriceOf(string productId) => null;
        public static bool Buy(string productId, Action<bool> done) => false;
#endif
    }
}
