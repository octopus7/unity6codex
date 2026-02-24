using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    [DisallowMultipleComponent]
    public sealed class SimpleDebugHud : MonoBehaviour
    {
        public NetworkGameClient Client;
        public int HealItemId = 1;
        public int SpeedItemId = 2;
        public float UiScale = 2f;

        private void Awake()
        {
            InstallPanels();
            enabled = false;
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            InstallPanels();
        }

        private void InstallPanels()
        {
            var connection = GetOrAdd<ConnectionPanelHud>();
            connection.Client = Client;
            connection.UiScale = UiScale;
            connection.HideWhenConnected = true;

            var shop = GetOrAdd<ShopPanelHud>();
            shop.Client = Client;
            shop.HealItemId = HealItemId;
            shop.SpeedItemId = SpeedItemId;
            shop.UiScale = UiScale;

            var status = GetOrAdd<StatusPanelHud>();
            status.Client = Client;
            status.UiScale = UiScale;

            var leaderboard = GetOrAdd<LeaderboardPanelHud>();
            leaderboard.Client = Client;
            leaderboard.UiScale = UiScale;

            var inventory = GetOrAdd<InventoryPanelHud>();
            inventory.Client = Client;
            inventory.InventoryManager = GetComponent<PlayerInventoryManager>();
            inventory.ItemDataManager = GetComponent<ItemDataManager>();
            inventory.UiScale = UiScale;

            var growthTreeData = GetOrAdd<GrowthTreeDataManager>();
            growthTreeData.ResourcesTreePath = "Progression/growth_tree";
            growthTreeData.LoadOnAwake = true;

            var growthProgression = GetOrAdd<GrowthProgressionManager>();
            growthProgression.Client = Client;
            growthProgression.TreeDataManager = growthTreeData;
            growthProgression.DefaultGemBalance = 12;
            growthProgression.LoadOnAwake = true;

            var growthPanel = GetOrAdd<GrowthPanelHud>();
            growthPanel.Client = Client;
            growthPanel.TreeDataManager = growthTreeData;
            growthPanel.ProgressionManager = growthProgression;
            growthPanel.InputSender = FindFirstObjectByType<LocalInputSender>();
            growthPanel.UiScale = UiScale;

            status.GrowthPanel = growthPanel;

            var coinDispenser = GetOrAdd<CoinDispenserHud>();
            coinDispenser.Client = Client;
            coinDispenser.WorldCamera = Camera.main;
            coinDispenser.UiScale = UiScale;

            var overheadHp = GetOrAdd<PlayerOverheadHealthHud>();
            overheadHp.Client = Client;
            overheadHp.UiScale = UiScale;
        }

        private T GetOrAdd<T>() where T : Component
        {
            var component = GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return gameObject.AddComponent<T>();
        }
    }
}
