using System;
using System.Collections.Generic;
using System.Linq;
using CodexSix.TopdownShooter.Net;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class NetworkGameClient : MonoBehaviour
    {
        [Header("Runtime References")]
        public TcpGameTransport Transport;
        public Transform PlayerContainer;
        public Transform ProjectileContainer;
        public Transform CoinContainer;
        public Transform ItemDropContainer;
        public ItemDataManager ItemDataManager;
        public PlayerInventoryManager InventoryManager;

        [Header("View Colors")]
        public Color LocalPlayerColor = new Color(0.2f, 0.9f, 0.2f);
        public Color RemotePlayerColor = new Color(0.2f, 0.8f, 1f);
        public Color ShopPlayerColor = new Color(1f, 0.9f, 0.2f);

        [Header("View Smoothing")]
        public float LocalPlayerPositionSmoothing = 22f;
        public float RemotePlayerPositionSmoothing = 16f;
        public float PlayerRotationSmoothing = 18f;
        public float ProjectilePositionSmoothing = 28f;

        public int LocalPlayerId { get; private set; } = -1;
        public int LocalHp { get; private set; } = 100;
        public int LocalCoins { get; private set; }
        public bool LocalInShopZone { get; private set; }
        public int CurrentPlayerCount { get; private set; }
        public long LastPingMs { get; private set; }
        public string LeaderboardText { get; private set; } = "-";
        public uint LastServerTick { get; private set; }
        public int CoinDispenserStackId { get; private set; } = -1;
        public int CoinDispenserStackAmount { get; private set; }
        public float SecondsUntilCoinDispenserSpawn =>
            (CoinDispenserIntervalTicks - (LastServerTick % CoinDispenserIntervalTicks)) * ServerTickDeltaSeconds;
        public ConnectionState CurrentConnectionState => Transport != null ? Transport.CurrentState : ConnectionState.Disconnected;

        private readonly Dictionary<int, GameObject> _playerViews = new();
        private readonly Dictionary<int, short> _playerHpById = new();
        private readonly Dictionary<int, PlayerKind> _playerKindById = new();
        private readonly Dictionary<int, GameObject> _projectileViews = new();
        private readonly Dictionary<int, GameObject> _coinViews = new();
        private readonly Dictionary<int, GameObject> _itemDropViews = new();
        private readonly Dictionary<int, int> _itemDropItemIds = new();
        private readonly Dictionary<int, Vector3> _playerTargetPositions = new();
        private readonly Dictionary<int, Quaternion> _playerTargetRotations = new();
        private readonly Dictionary<int, Vector3> _projectileTargetPositions = new();

        private readonly HashSet<int> _scratchIds = new();
        private readonly Dictionary<int, Material> _itemDropMaterials = new();
        private uint _nextInputSeq;
        private uint _nextShopRequestSeq;

        private const uint CoinDispenserIntervalTicks = 150u;
        private const float ServerTickDeltaSeconds = 1f / 30f;

        private void Awake()
        {
            if (PlayerContainer == null)
            {
                PlayerContainer = CreateContainer("Players");
            }

            if (ProjectileContainer == null)
            {
                ProjectileContainer = CreateContainer("Projectiles");
            }

            if (CoinContainer == null)
            {
                CoinContainer = CreateContainer("CoinStacks");
            }

            if (ItemDropContainer == null)
            {
                ItemDropContainer = CreateContainer("ItemDrops");
            }

            if (ItemDataManager == null)
            {
                ItemDataManager = GetComponent<ItemDataManager>();
            }

            if (InventoryManager == null)
            {
                InventoryManager = GetComponent<PlayerInventoryManager>();
            }
        }

        private void OnEnable()
        {
            if (Transport == null)
            {
                return;
            }

            Transport.WelcomeReceived += OnWelcomeReceived;
            Transport.SnapshotReceived += OnSnapshotReceived;
            Transport.EventReceived += OnEventReceived;
            Transport.PongReceived += OnPongReceived;
            Transport.ErrorReceived += OnErrorReceived;
            Transport.ConnectionStateChanged += OnConnectionStateChanged;
        }

        private void OnDisable()
        {
            if (Transport == null)
            {
                return;
            }

            Transport.WelcomeReceived -= OnWelcomeReceived;
            Transport.SnapshotReceived -= OnSnapshotReceived;
            Transport.EventReceived -= OnEventReceived;
            Transport.PongReceived -= OnPongReceived;
            Transport.ErrorReceived -= OnErrorReceived;
            Transport.ConnectionStateChanged -= OnConnectionStateChanged;
        }

        private void OnDestroy()
        {
            DestroyItemDropMaterials();
        }

        private void LateUpdate()
        {
            if (CurrentConnectionState != ConnectionState.Connected)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            SmoothPlayers(deltaTime);
            SmoothProjectiles(deltaTime);
        }

        public async void Connect(string host, int port, string nickname)
        {
            if (Transport == null)
            {
                Debug.LogWarning("NetworkGameClient has no transport assigned.");
                return;
            }

            try
            {
                await Transport.ConnectAsync(host, port, nickname, default);
            }
            catch (Exception exception)
            {
                Debug.LogError("Connect failed: " + exception.Message);
            }
        }

        public void Disconnect()
        {
            Transport?.Disconnect();
            ResetWorld();
        }

        public void SendInputFrame(Vector2 move, Vector2 aimDirection, bool fireHeld)
        {
            if (Transport == null || Transport.CurrentState != ConnectionState.Connected)
            {
                return;
            }

            var input = new ClientInputFrame
            {
                InputSeq = ++_nextInputSeq,
                ClientTick = (uint)Time.frameCount,
                MoveX = ToAxisShort(move.x),
                MoveY = ToAxisShort(move.y),
                AimX = aimDirection.x,
                AimY = aimDirection.y,
                Buttons = (byte)(fireHeld ? 0x1 : 0x0)
            };

            Transport.SendInput(in input);
        }

        public void SendShopPurchase(byte itemId)
        {
            if (Transport == null || Transport.CurrentState != ConnectionState.Connected)
            {
                return;
            }

            var request = new ShopPurchaseRequest
            {
                ItemId = itemId,
                RequestSeq = ++_nextShopRequestSeq
            };

            Transport.SendShopPurchase(in request);
        }

        public bool TryGetLocalPlayerPosition(out Vector3 worldPosition)
        {
            worldPosition = default;
            if (LocalPlayerId <= 0 || !_playerViews.TryGetValue(LocalPlayerId, out var localPlayerView))
            {
                return false;
            }

            if (_playerTargetPositions.TryGetValue(LocalPlayerId, out var targetPosition))
            {
                worldPosition = targetPosition;
                return true;
            }

            worldPosition = localPlayerView.transform.position;
            return true;
        }

        public bool TryGetCoinStackWorldPosition(int coinStackId, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (coinStackId <= 0 || !_coinViews.TryGetValue(coinStackId, out var coinView) || coinView == null)
            {
                return false;
            }

            worldPosition = coinView.transform.position;
            return true;
        }

        public void FillPlayerHudEntries(List<PlayerHudEntry> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Clear();
            foreach (var pair in _playerViews)
            {
                var playerView = pair.Value;
                if (playerView == null)
                {
                    continue;
                }

                var hp = _playerHpById.TryGetValue(pair.Key, out var currentHp) ? currentHp : (short)0;
                var isBot = _playerKindById.TryGetValue(pair.Key, out var kind) && kind == PlayerKind.Bot;
                output.Add(new PlayerHudEntry(
                    pair.Key,
                    playerView.transform.position,
                    hp,
                    playerView.activeInHierarchy,
                    pair.Key == LocalPlayerId,
                    isBot));
            }
        }

        private void OnWelcomeReceived(ServerWelcome welcome)
        {
            LocalPlayerId = welcome.PlayerId;
            _nextInputSeq = 0;
            _nextShopRequestSeq = 0;
            if (LocalPlayerId > 0 && InventoryManager != null)
            {
                InventoryManager.GetOrCreateInventory(LocalPlayerId);
            }

            Debug.Log($"Connected. LocalPlayerId={LocalPlayerId}, tick={welcome.TickRateHz}, snapshot={welcome.SnapshotRateHz}");
        }

        private void OnSnapshotReceived(ServerSnapshot snapshot)
        {
            LastServerTick = snapshot.ServerTick;
            CurrentPlayerCount = snapshot.Players.Length;
            ApplyPlayers(snapshot.Players);
            ApplyProjectiles(snapshot.Projectiles);
            ApplyCoins(snapshot.CoinStacks);
            ApplyItemDrops(snapshot.ItemDrops);
            UpdateLocalHud(snapshot.Players);
            UpdateLeaderboard(snapshot.Players);
        }

        private void OnEventReceived(ServerEventBatch batch)
        {
            for (var i = 0; i < batch.Events.Length; i++)
            {
                var gameEvent = batch.Events[i];
                if (gameEvent.EventType == GameEventType.PurchaseRejected && gameEvent.ActorId == LocalPlayerId)
                {
                    Debug.Log($"Purchase rejected. item={gameEvent.Value} reason={gameEvent.ExtraId}");
                }

                if (gameEvent.EventType == GameEventType.ItemPicked && gameEvent.ActorId == LocalPlayerId)
                {
                    TryApplyLocalInventoryPickup(gameEvent.Value, Mathf.Max(1, gameEvent.ExtraId));
                }
            }
        }

        private void OnPongReceived(long pingMs)
        {
            LastPingMs = pingMs;
        }

        private void OnErrorReceived(ServerError error)
        {
            Debug.LogWarning($"Server error {error.ErrorCode}: {error.Message}");
        }

        private void OnConnectionStateChanged(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected)
            {
                ResetWorld();
            }
        }

        private void SmoothPlayers(float deltaTime)
        {
            foreach (var pair in _playerViews)
            {
                var playerView = pair.Value;
                if (playerView == null || !playerView.activeInHierarchy)
                {
                    continue;
                }

                if (_playerTargetPositions.TryGetValue(pair.Key, out var targetPosition))
                {
                    var speed = pair.Key == LocalPlayerId
                        ? LocalPlayerPositionSmoothing
                        : RemotePlayerPositionSmoothing;
                    var lerpFactor = SmoothingToLerp(speed, deltaTime);
                    playerView.transform.position = Vector3.Lerp(playerView.transform.position, targetPosition, lerpFactor);
                }

                if (_playerTargetRotations.TryGetValue(pair.Key, out var targetRotation))
                {
                    var rotationLerpFactor = SmoothingToLerp(PlayerRotationSmoothing, deltaTime);
                    playerView.transform.rotation =
                        Quaternion.Slerp(playerView.transform.rotation, targetRotation, rotationLerpFactor);
                }
            }
        }

        private void SmoothProjectiles(float deltaTime)
        {
            var lerpFactor = SmoothingToLerp(ProjectilePositionSmoothing, deltaTime);
            foreach (var pair in _projectileViews)
            {
                var projectileView = pair.Value;
                if (projectileView == null)
                {
                    continue;
                }

                if (!_projectileTargetPositions.TryGetValue(pair.Key, out var targetPosition))
                {
                    continue;
                }

                projectileView.transform.position = Vector3.Lerp(projectileView.transform.position, targetPosition, lerpFactor);
            }
        }

        private static float SmoothingToLerp(float smoothing, float deltaTime)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, smoothing) * deltaTime);
        }

        private void ApplyPlayers(PlayerSnapshot[] players)
        {
            _scratchIds.Clear();

            foreach (var player in players)
            {
                _scratchIds.Add(player.PlayerId);

                var snapshotPosition = new Vector3(player.PositionX, 0.55f, player.PositionY);
                var wasCreated = false;
                if (!_playerViews.TryGetValue(player.PlayerId, out var playerView))
                {
                    playerView = CreatePlayerView(player.PlayerId);
                    _playerViews.Add(player.PlayerId, playerView);
                    wasCreated = true;
                }

                _playerTargetPositions[player.PlayerId] = snapshotPosition;
                if (wasCreated)
                {
                    playerView.transform.position = snapshotPosition;
                }

                _playerHpById[player.PlayerId] = player.Hp;
                _playerKindById[player.PlayerId] = player.Kind;
                if (Mathf.Abs(player.AimX) > 0.001f || Mathf.Abs(player.AimY) > 0.001f)
                {
                    var snapshotRotation = Quaternion.LookRotation(new Vector3(player.AimX, 0f, player.AimY));
                    _playerTargetRotations[player.PlayerId] = snapshotRotation;
                    if (wasCreated)
                    {
                        playerView.transform.rotation = snapshotRotation;
                    }
                }

                playerView.SetActive(player.IsAlive);
                ApplyPlayerColor(playerView, player);
            }

            RemoveMissingViews(_playerViews, _scratchIds);
            RemoveMissingTargets(_playerTargetPositions, _scratchIds);
            RemoveMissingTargets(_playerTargetRotations, _scratchIds);
            RemoveMissingPlayerHp(_scratchIds);
            RemoveMissingTargets(_playerKindById, _scratchIds);
        }

        private void ApplyProjectiles(ProjectileSnapshot[] projectiles)
        {
            _scratchIds.Clear();

            foreach (var projectile in projectiles)
            {
                _scratchIds.Add(projectile.ProjectileId);

                var snapshotPosition = new Vector3(projectile.PositionX, 0.25f, projectile.PositionY);
                var wasCreated = false;
                if (!_projectileViews.TryGetValue(projectile.ProjectileId, out var projectileView))
                {
                    projectileView = CreateProjectileView(projectile.ProjectileId);
                    _projectileViews.Add(projectile.ProjectileId, projectileView);
                    wasCreated = true;
                }

                _projectileTargetPositions[projectile.ProjectileId] = snapshotPosition;
                if (wasCreated)
                {
                    projectileView.transform.position = snapshotPosition;
                }
            }

            RemoveMissingViews(_projectileViews, _scratchIds);
            RemoveMissingTargets(_projectileTargetPositions, _scratchIds);
        }

        private void ApplyCoins(CoinStackSnapshot[] coinStacks)
        {
            _scratchIds.Clear();
            var dispenserStackId = -1;
            var dispenserStackAmount = 0;

            foreach (var coin in coinStacks)
            {
                _scratchIds.Add(coin.CoinStackId);

                if (!_coinViews.TryGetValue(coin.CoinStackId, out var coinView))
                {
                    coinView = CreateCoinView(coin.CoinStackId);
                    _coinViews.Add(coin.CoinStackId, coinView);
                }

                coinView.transform.position = new Vector3(coin.PositionX, 0.1f, coin.PositionY);
                var scale = Mathf.Clamp(0.25f + (coin.Amount * 0.03f), 0.25f, 0.8f);
                coinView.transform.localScale = new Vector3(scale, 0.08f, scale);

                if (coin.IsDispenser)
                {
                    dispenserStackId = coin.CoinStackId;
                    dispenserStackAmount = coin.Amount;
                }
            }

            RemoveMissingViews(_coinViews, _scratchIds);
            CoinDispenserStackId = dispenserStackId;
            CoinDispenserStackAmount = dispenserStackAmount;
        }

        private void ApplyItemDrops(ItemDropSnapshot[] itemDrops)
        {
            _scratchIds.Clear();

            foreach (var itemDrop in itemDrops)
            {
                _scratchIds.Add(itemDrop.ItemDropId);

                if (!_itemDropViews.TryGetValue(itemDrop.ItemDropId, out var itemDropView))
                {
                    itemDropView = CreateItemDropView(itemDrop.ItemDropId, itemDrop.ItemId);
                    _itemDropViews.Add(itemDrop.ItemDropId, itemDropView);
                    _itemDropItemIds[itemDrop.ItemDropId] = itemDrop.ItemId;
                }

                if (_itemDropItemIds.TryGetValue(itemDrop.ItemDropId, out var previousItemId) && previousItemId != itemDrop.ItemId)
                {
                    if (itemDropView != null)
                    {
                        ApplyItemDropTexture(itemDropView, itemDrop.ItemId);
                    }

                    _itemDropItemIds[itemDrop.ItemDropId] = itemDrop.ItemId;
                }

                if (itemDropView != null)
                {
                    itemDropView.transform.position = new Vector3(itemDrop.PositionX, 0.04f, itemDrop.PositionY);
                }
            }

            RemoveMissingViews(_itemDropViews, _scratchIds);
            RemoveMissingTargets(_itemDropItemIds, _scratchIds);
        }

        private GameObject CreateItemDropView(int itemDropId, int itemId)
        {
            var root = new GameObject($"ItemDrop_{itemDropId}");
            root.transform.SetParent(ItemDropContainer, worldPositionStays: false);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Icon";
            quad.transform.SetParent(root.transform, worldPositionStays: false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * 1.1f;

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            ApplyItemDropTexture(quad, itemId);
            return root;
        }

        private void ApplyItemDropTexture(GameObject itemDropRoot, int itemId)
        {
            if (itemDropRoot == null)
            {
                return;
            }

            var renderer = itemDropRoot.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var iconTexture = ItemDataManager != null
                ? ItemDataManager.GetItemIconOrNull(itemId)
                : null;

            if (iconTexture == null)
            {
                iconTexture = GetFallbackItemIcon();
            }

            if (!_itemDropMaterials.TryGetValue(itemId, out var material) || material == null)
            {
                material = CreateItemDropMaterial(iconTexture);
                _itemDropMaterials[itemId] = material;
            }

            renderer.sharedMaterial = material;
        }

        private static Material CreateItemDropMaterial(Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");

            var material = new Material(shader)
            {
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
            };

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }

            return material;
        }

        private static Texture2D _fallbackItemIcon;

        private static Texture2D GetFallbackItemIcon()
        {
            if (_fallbackItemIcon != null)
            {
                return _fallbackItemIcon;
            }

            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[64 * 64];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var dx = x - 31.5f;
                    var dy = y - 31.5f;
                    var radius = Mathf.Sqrt((dx * dx) + (dy * dy));
                    if (radius < 27f && radius > 13f)
                    {
                        pixels[(y * 64) + x] = new Color32(100, 220, 255, 220);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            _fallbackItemIcon = texture;
            return _fallbackItemIcon;
        }

        private void TryApplyLocalInventoryPickup(int itemId, int quantity)
        {
            if (InventoryManager == null || LocalPlayerId <= 0 || quantity <= 0)
            {
                return;
            }

            if (!InventoryManager.TryAddItem(LocalPlayerId, itemId, quantity, out var remainingQuantity))
            {
                Debug.LogWarning($"Inventory add failed. item={itemId}, qty={quantity}");
                return;
            }

            if (remainingQuantity > 0)
            {
                Debug.LogWarning($"Inventory full. item={itemId}, accepted={quantity - remainingQuantity}, dropped={remainingQuantity}");
            }
        }

        private void UpdateLocalHud(PlayerSnapshot[] players)
        {
            LocalHp = 0;
            LocalCoins = 0;
            LocalInShopZone = false;

            if (LocalPlayerId <= 0)
            {
                return;
            }

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player.PlayerId != LocalPlayerId)
                {
                    continue;
                }

                LocalHp = player.Hp;
                LocalCoins = player.CarriedCoins;
                LocalInShopZone = player.InShopZone;
                return;
            }
        }

        private void UpdateLeaderboard(PlayerSnapshot[] players)
        {
            var topPlayers = players
                .OrderByDescending(player => player.CarriedCoins)
                .ThenBy(player => player.PlayerId)
                .Take(3)
                .ToArray();

            if (topPlayers.Length == 0)
            {
                LeaderboardText = "-";
                return;
            }

            LeaderboardText = string.Join(
                "\n",
                topPlayers.Select((player, index) => $"{index + 1}. P{player.PlayerId}: {player.CarriedCoins}c"));
        }

        private static short ToAxisShort(float value)
        {
            return (short)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 32767f);
        }

        private void ApplyPlayerColor(GameObject playerView, PlayerSnapshot player)
        {
            var renderer = playerView.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return;
            }

            if (player.InShopZone)
            {
                renderer.material.color = ShopPlayerColor;
                return;
            }

            renderer.material.color = player.PlayerId == LocalPlayerId ? LocalPlayerColor : RemotePlayerColor;
        }

        private GameObject CreatePlayerView(int playerId)
        {
            var view = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            view.name = $"Player_{playerId}";
            view.transform.SetParent(PlayerContainer, worldPositionStays: false);
            view.transform.localScale = new Vector3(0.8f, 1.1f, 0.8f);
            return view;
        }

        private GameObject CreateProjectileView(int projectileId)
        {
            var view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            view.name = $"Projectile_{projectileId}";
            view.transform.SetParent(ProjectileContainer, worldPositionStays: false);
            view.transform.localScale = Vector3.one * 0.2f;
            var renderer = view.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.45f, 0.1f);
            }

            return view;
        }

        private GameObject CreateCoinView(int coinStackId)
        {
            var view = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            view.name = $"Coin_{coinStackId}";
            view.transform.SetParent(CoinContainer, worldPositionStays: false);
            view.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);
            var renderer = view.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.84f, 0.2f);
            }

            return view;
        }

        private Transform CreateContainer(string name)
        {
            var container = new GameObject(name);
            container.transform.SetParent(transform, worldPositionStays: false);
            return container.transform;
        }

        private static void RemoveMissingViews(Dictionary<int, GameObject> viewsById, HashSet<int> aliveIds)
        {
            if (viewsById.Count == 0)
            {
                return;
            }

            var toRemove = ListPool<int>.Get();
            try
            {
                foreach (var pair in viewsById)
                {
                    if (aliveIds.Contains(pair.Key))
                    {
                        continue;
                    }

                    if (pair.Value != null)
                    {
                        Destroy(pair.Value);
                    }

                    toRemove.Add(pair.Key);
                }

                for (var i = 0; i < toRemove.Count; i++)
                {
                    viewsById.Remove(toRemove[i]);
                }
            }
            finally
            {
                ListPool<int>.Release(toRemove);
            }
        }

        private static void RemoveMissingTargets<T>(Dictionary<int, T> valuesById, HashSet<int> aliveIds)
        {
            if (valuesById.Count == 0)
            {
                return;
            }

            var toRemove = ListPool<int>.Get();
            try
            {
                foreach (var pair in valuesById)
                {
                    if (aliveIds.Contains(pair.Key))
                    {
                        continue;
                    }

                    toRemove.Add(pair.Key);
                }

                for (var i = 0; i < toRemove.Count; i++)
                {
                    valuesById.Remove(toRemove[i]);
                }
            }
            finally
            {
                ListPool<int>.Release(toRemove);
            }
        }

        private void RemoveMissingPlayerHp(HashSet<int> aliveIds)
        {
            if (_playerHpById.Count == 0)
            {
                return;
            }

            var toRemove = ListPool<int>.Get();
            try
            {
                foreach (var pair in _playerHpById)
                {
                    if (aliveIds.Contains(pair.Key))
                    {
                        continue;
                    }

                    toRemove.Add(pair.Key);
                }

                for (var i = 0; i < toRemove.Count; i++)
                {
                    _playerHpById.Remove(toRemove[i]);
                }
            }
            finally
            {
                ListPool<int>.Release(toRemove);
            }
        }

        private void ResetWorld()
        {
            if (LocalPlayerId > 0 && InventoryManager != null)
            {
                InventoryManager.RemoveInventory(LocalPlayerId);
            }

            LocalPlayerId = -1;
            LocalHp = 100;
            LocalCoins = 0;
            LocalInShopZone = false;
            CurrentPlayerCount = 0;
            LeaderboardText = "-";
            LastPingMs = 0;
            LastServerTick = 0;
            CoinDispenserStackId = -1;
            CoinDispenserStackAmount = 0;

            DestroyAllViews(_playerViews);
            _playerTargetPositions.Clear();
            _playerTargetRotations.Clear();
            _playerHpById.Clear();
            _playerKindById.Clear();
            DestroyAllViews(_projectileViews);
            _projectileTargetPositions.Clear();
            DestroyAllViews(_coinViews);
            DestroyAllViews(_itemDropViews);
            _itemDropItemIds.Clear();
            DestroyItemDropMaterials();
        }

        private void DestroyItemDropMaterials()
        {
            foreach (var pair in _itemDropMaterials)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            _itemDropMaterials.Clear();
        }

        private static void DestroyAllViews(Dictionary<int, GameObject> views)
        {
            foreach (var pair in views)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            views.Clear();
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }

        public readonly struct PlayerHudEntry
        {
            public readonly int PlayerId;
            public readonly Vector3 WorldPosition;
            public readonly short Hp;
            public readonly bool IsAlive;
            public readonly bool IsLocalPlayer;
            public readonly bool IsBot;

            public PlayerHudEntry(int playerId, Vector3 worldPosition, short hp, bool isAlive, bool isLocalPlayer, bool isBot)
            {
                PlayerId = playerId;
                WorldPosition = worldPosition;
                Hp = hp;
                IsAlive = isAlive;
                IsLocalPlayer = isLocalPlayer;
                IsBot = isBot;
            }
        }
    }
}
