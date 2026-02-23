using TopdownShooter.Server.Networking;

namespace TopdownShooter.Server.Domain;

public sealed class GameWorld
{
    private readonly Dictionary<int, PlayerState> _players = new();
    private readonly List<ProjectileState> _projectiles = new();
    private readonly List<CoinStackState> _coinStacks = new();
    private readonly List<GameEvent> _pendingEvents = new();
    private readonly Queue<PendingShopPurchase> _pendingShopPurchases = new();
    private readonly Random _random;

    private readonly PortalState[] _portals;
    private readonly Aabb2[] _obstacles;
    private readonly ShopZoneState _shopZone;
    private readonly Vector2f[] _spawnPoints;

    private readonly Vector2f _shopSpawnPoint = new(0f, 28f);
    private readonly Vector2f _shopExitPoint = new(0f, 16f);

    private readonly int _maxPlayers;
    private readonly int _maxWorldCoins;

    private int _nextPlayerId = 1;
    private int _nextProjectileId = 1;
    private int _nextCoinStackId = 1;
    private int _nextSpawnIndex;
    private uint _serverTick;

    public GameWorld(int maxPlayers, int maxWorldCoins, int? randomSeed = null)
    {
        _maxPlayers = maxPlayers;
        _maxWorldCoins = maxWorldCoins;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();

        _shopZone = new ShopZoneState
        {
            MinX = -6f,
            MinY = 22f,
            MaxX = 6f,
            MaxY = 34f
        };

        _portals =
        [
            new PortalState { PortalId = 1, PortalType = PortalType.Entry, Position = new Vector2f(-18f, 0f), Radius = 1.2f },
            new PortalState { PortalId = 2, PortalType = PortalType.Entry, Position = new Vector2f(18f, 0f), Radius = 1.2f },
            new PortalState { PortalId = 3, PortalType = PortalType.Exit, Position = new Vector2f(0f, 23f), Radius = 1.2f }
        ];

        _obstacles =
        [
            new Aabb2(-2f, -2f, 2f, 2f),
            new Aabb2(8.5f, -1f, 11.5f, 1f),
            new Aabb2(-11.5f, -1f, -8.5f, 1f),
            new Aabb2(-1f, 8.5f, 1f, 11.5f),
            new Aabb2(-1f, -11.5f, 1f, -8.5f)
        ];

        _spawnPoints =
        [
            new Vector2f(-16f, -16f),
            new Vector2f(-16f, 16f),
            new Vector2f(16f, -16f),
            new Vector2f(16f, 16f),
            new Vector2f(0f, -16f),
            new Vector2f(0f, 16f),
            new Vector2f(-16f, 0f),
            new Vector2f(16f, 0f)
        ];
    }

    public uint ServerTick => _serverTick;
    public int PlayerCount => _players.Count;
    public int ProjectileCount => _projectiles.Count;
    public int CoinStackCount => _coinStacks.Count;
    public int WorldCoinTotal => _coinStacks.Sum(stack => stack.Amount);

    public int AddPlayer(string nickname, PlayerKind kind)
    {
        if (_players.Count >= _maxPlayers)
        {
            return -1;
        }

        var playerId = _nextPlayerId++;
        var spawn = NextSpawnPoint();

        _players[playerId] = new PlayerState
        {
            PlayerId = playerId,
            Nickname = nickname,
            Kind = kind,
            Position = spawn,
            AimDirection = new Vector2f(1f, 0f),
            Hp = GameRules.MaxHp,
            IsAlive = true,
            InShopZone = false,
            CarriedCoins = 0,
            SpeedBuffStacks = 0
        };

        return playerId;
    }

    public void RemovePlayer(int playerId)
    {
        _players.Remove(playerId);
        _projectiles.RemoveAll(projectile => projectile.OwnerPlayerId == playerId);
    }

    public bool ContainsPlayer(int playerId)
    {
        return _players.ContainsKey(playerId);
    }

    public void ApplyInput(int playerId, InputFrameMessage input)
    {
        if (!_players.TryGetValue(playerId, out var player))
        {
            return;
        }

        player.MoveX = input.MoveX;
        player.MoveY = input.MoveY;
        player.FireHeld = (input.Buttons & 0x1) != 0;
        player.LastInputSeq = input.InputSeq;

        var aim = new Vector2f(input.AimX, input.AimY);
        if (aim.LengthSquared > 0.00001f)
        {
            player.AimDirection = aim.Normalized();
        }
    }

    public void QueueShopPurchase(int playerId, byte itemId)
    {
        _pendingShopPurchases.Enqueue(new PendingShopPurchase(playerId, itemId));
    }

    public void Step()
    {
        _serverTick++;
        UpdatePlayerMovementAndPortals();
        ProcessFiring();
        UpdateProjectiles();
        ProcessRespawns();
        ProcessCoinPickups();
        ProcessShopPurchases();
    }

    public WorldSnapshot CaptureSnapshot()
    {
        var players = _players.Values
            .Select(player => new PlayerSnapshotState(
                player.PlayerId,
                player.Position,
                player.AimDirection,
                (short)player.Hp,
                player.CarriedCoins,
                (byte)player.SpeedBuffStacks,
                player.Kind,
                player.IsAlive,
                player.InShopZone,
                player.LastInputSeq))
            .ToList();

        var projectiles = _projectiles
            .Select(projectile => new ProjectileSnapshotState(
                projectile.ProjectileId,
                projectile.OwnerPlayerId,
                projectile.Position,
                projectile.Direction))
            .ToList();

        var coins = _coinStacks
            .Select(stack => new CoinSnapshotState(
                stack.CoinStackId,
                stack.Position,
                stack.Amount,
                stack.CreatedTick))
            .ToList();

        var portals = _portals
            .Select(portal => new PortalSnapshotState(
                portal.PortalId,
                portal.PortalType,
                portal.Position))
            .ToList();

        return new WorldSnapshot
        {
            ServerTick = _serverTick,
            Players = players,
            Projectiles = projectiles,
            CoinStacks = coins,
            Portals = portals,
            ShopZone = _shopZone
        };
    }

    public List<GameEvent> DrainPendingEvents()
    {
        if (_pendingEvents.Count == 0)
        {
            return new List<GameEvent>(capacity: 0);
        }

        var copy = new List<GameEvent>(_pendingEvents);
        _pendingEvents.Clear();
        return copy;
    }

    private void UpdatePlayerMovementAndPortals()
    {
        foreach (var player in _players.Values)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var move = new Vector2f(player.MoveX / 32767f, player.MoveY / 32767f).Normalized();
            var speed = GameRules.BaseMoveSpeed * (1f + (player.SpeedBuffStacks * GameRules.SpeedBuffMultiplierPerStack));
            var nextPosition = player.Position + (move * speed * GameRules.TickDelta);

            if (player.InShopZone)
            {
                nextPosition = ClampToShopBounds(nextPosition);
            }
            else
            {
                nextPosition = ClampToBattleBounds(nextPosition);
                if (IsInsideObstacle(nextPosition))
                {
                    nextPosition = player.Position;
                }
            }

            player.Position = nextPosition;
            HandlePortalTransition(player);
        }
    }

    private void HandlePortalTransition(PlayerState player)
    {
        if (!player.InShopZone)
        {
            foreach (var portal in _portals.Where(portal => portal.PortalType == PortalType.Entry))
            {
                if (Vector2f.DistanceSquared(player.Position, portal.Position) <= (portal.Radius * portal.Radius))
                {
                    player.Position = _shopSpawnPoint;
                    player.InShopZone = true;
                    return;
                }
            }

            return;
        }

        foreach (var portal in _portals.Where(portal => portal.PortalType == PortalType.Exit))
        {
            if (Vector2f.DistanceSquared(player.Position, portal.Position) <= (portal.Radius * portal.Radius))
            {
                player.Position = _shopExitPoint;
                player.InShopZone = false;
                return;
            }
        }
    }

    private void ProcessFiring()
    {
        foreach (var player in _players.Values)
        {
            if (!player.IsAlive || player.InShopZone || !player.FireHeld)
            {
                continue;
            }

            if (_serverTick < player.NextFireAllowedTick)
            {
                continue;
            }

            var aim = player.AimDirection.LengthSquared <= 0.00001f
                ? new Vector2f(1f, 0f)
                : player.AimDirection.Normalized();

            _projectiles.Add(new ProjectileState
            {
                ProjectileId = _nextProjectileId++,
                OwnerPlayerId = player.PlayerId,
                Position = player.Position + (aim * GameRules.ProjectileSpawnOffset),
                Direction = aim,
                SpawnTick = _serverTick
            });

            player.NextFireAllowedTick = _serverTick + GameRules.FireCooldownTicks;
            _pendingEvents.Add(new GameEvent(GameEventType.ShotFired, player.PlayerId, 0, 0, 0, player.Position));
        }
    }

    private void UpdateProjectiles()
    {
        var hitRadiusSquared = GameRules.ProjectileHitRadius * GameRules.ProjectileHitRadius;
        for (var index = _projectiles.Count - 1; index >= 0; index--)
        {
            var projectile = _projectiles[index];
            projectile.Position += projectile.Direction * GameRules.ProjectileSpeed * GameRules.TickDelta;

            var removeProjectile = false;

            if ((_serverTick - projectile.SpawnTick) > GameRules.ProjectileLifetimeTicks)
            {
                removeProjectile = true;
            }
            else if (!IsWithinBattleBounds(projectile.Position))
            {
                removeProjectile = true;
            }
            else if (IsInsideShopZone(projectile.Position))
            {
                removeProjectile = true;
            }
            else
            {
                foreach (var target in _players.Values)
                {
                    if (!target.IsAlive || target.InShopZone)
                    {
                        continue;
                    }

                    if (target.PlayerId == projectile.OwnerPlayerId)
                    {
                        continue;
                    }

                    if (Vector2f.DistanceSquared(target.Position, projectile.Position) > hitRadiusSquared)
                    {
                        continue;
                    }

                    ApplyProjectileHit(projectile.OwnerPlayerId, target);
                    removeProjectile = true;
                    break;
                }
            }

            if (removeProjectile)
            {
                _projectiles.RemoveAt(index);
            }
        }
    }

    private void ApplyProjectileHit(int attackerPlayerId, PlayerState victim)
    {
        victim.Hp -= GameRules.BulletDamage;
        _pendingEvents.Add(new GameEvent(
            GameEventType.Damage,
            attackerPlayerId,
            victim.PlayerId,
            GameRules.BulletDamage,
            0,
            victim.Position));

        if (victim.Hp > 0)
        {
            return;
        }

        HandlePlayerDeath(attackerPlayerId, victim);
    }

    private void HandlePlayerDeath(int killerPlayerId, PlayerState victim)
    {
        victim.IsAlive = false;
        victim.Hp = 0;
        victim.RespawnAtTick = _serverTick + GameRules.RespawnDelayTicks;
        victim.SpeedBuffStacks = 0;
        victim.InShopZone = false;
        victim.FireHeld = false;

        _pendingEvents.Add(new GameEvent(
            GameEventType.Death,
            killerPlayerId,
            victim.PlayerId,
            0,
            0,
            victim.Position));

        var dropAmount = CoinRules.ComputeDropAmount(victim.CarriedCoins);
        victim.CarriedCoins = 0;
        DropCoins(victim.Position, dropAmount);
    }

    private void DropCoins(Vector2f center, int totalAmount)
    {
        var stacks = CoinRules.SplitIntoStacks(totalAmount, GameRules.MaxCoinDropStacks, _random);
        foreach (var amount in stacks)
        {
            var angle = (float)(_random.NextDouble() * (Math.PI * 2.0));
            var radius = (float)(_random.NextDouble() * 1.25);
            var offset = new Vector2f(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            var position = ClampToBattleBounds(center + offset);

            var stack = new CoinStackState
            {
                CoinStackId = _nextCoinStackId++,
                Position = position,
                Amount = amount,
                CreatedTick = _serverTick
            };

            _coinStacks.Add(stack);
            _pendingEvents.Add(new GameEvent(
                GameEventType.CoinDropped,
                0,
                0,
                amount,
                stack.CoinStackId,
                position));
        }

        EnforceWorldCoinCap();
    }

    private void EnforceWorldCoinCap()
    {
        var total = _coinStacks.Sum(stack => stack.Amount);
        if (total <= _maxWorldCoins)
        {
            return;
        }

        while (total > _maxWorldCoins && _coinStacks.Count > 0)
        {
            var oldest = _coinStacks
                .OrderBy(stack => stack.CreatedTick)
                .ThenBy(stack => stack.CoinStackId)
                .First();

            total -= oldest.Amount;
            _coinStacks.Remove(oldest);
        }
    }

    private void ProcessRespawns()
    {
        foreach (var player in _players.Values)
        {
            if (player.IsAlive || _serverTick < player.RespawnAtTick)
            {
                continue;
            }

            player.IsAlive = true;
            player.Hp = GameRules.MaxHp;
            player.Position = NextSpawnPoint();
            player.AimDirection = new Vector2f(1f, 0f);
            player.SpeedBuffStacks = 0;
            player.InShopZone = false;
            player.FireHeld = false;

            _pendingEvents.Add(new GameEvent(
                GameEventType.Respawn,
                player.PlayerId,
                0,
                0,
                0,
                player.Position));
        }
    }

    private void ProcessCoinPickups()
    {
        var pickupRadiusSquared = GameRules.CoinPickupRadius * GameRules.CoinPickupRadius;

        foreach (var player in _players.Values)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            for (var index = _coinStacks.Count - 1; index >= 0; index--)
            {
                var coin = _coinStacks[index];
                if (Vector2f.DistanceSquared(player.Position, coin.Position) > pickupRadiusSquared)
                {
                    continue;
                }

                player.CarriedCoins += coin.Amount;
                _coinStacks.RemoveAt(index);

                _pendingEvents.Add(new GameEvent(
                    GameEventType.CoinPicked,
                    player.PlayerId,
                    0,
                    coin.Amount,
                    coin.CoinStackId,
                    coin.Position));
            }
        }
    }

    private void ProcessShopPurchases()
    {
        while (_pendingShopPurchases.TryDequeue(out var request))
        {
            if (!_players.TryGetValue(request.PlayerId, out var player))
            {
                continue;
            }

            if (!player.IsAlive)
            {
                EmitPurchaseRejected(player.PlayerId, request.ItemId, PurchaseRejectReason.Dead);
                continue;
            }

            if (!player.InShopZone)
            {
                EmitPurchaseRejected(player.PlayerId, request.ItemId, PurchaseRejectReason.NotInShop);
                continue;
            }

            switch (request.ItemId)
            {
                case GameRules.HealItemId:
                    HandleHealPurchase(player);
                    break;
                case GameRules.SpeedItemId:
                    HandleSpeedPurchase(player);
                    break;
                default:
                    EmitPurchaseRejected(player.PlayerId, request.ItemId, PurchaseRejectReason.UnknownItem);
                    break;
            }
        }
    }

    private void HandleHealPurchase(PlayerState player)
    {
        if (player.CarriedCoins < GameRules.HealItemCost)
        {
            EmitPurchaseRejected(player.PlayerId, GameRules.HealItemId, PurchaseRejectReason.NotEnoughCoins);
            return;
        }

        player.CarriedCoins -= GameRules.HealItemCost;
        player.Hp = Math.Min(GameRules.MaxHp, player.Hp + GameRules.HealItemAmount);

        _pendingEvents.Add(new GameEvent(
            GameEventType.ShopPurchased,
            player.PlayerId,
            0,
            GameRules.HealItemId,
            GameRules.HealItemCost,
            player.Position));
    }

    private void HandleSpeedPurchase(PlayerState player)
    {
        if (player.SpeedBuffStacks >= GameRules.MaxSpeedBuffStacks)
        {
            EmitPurchaseRejected(player.PlayerId, GameRules.SpeedItemId, PurchaseRejectReason.MaxStacksReached);
            return;
        }

        if (player.CarriedCoins < GameRules.SpeedItemCost)
        {
            EmitPurchaseRejected(player.PlayerId, GameRules.SpeedItemId, PurchaseRejectReason.NotEnoughCoins);
            return;
        }

        player.CarriedCoins -= GameRules.SpeedItemCost;
        player.SpeedBuffStacks++;

        _pendingEvents.Add(new GameEvent(
            GameEventType.ShopPurchased,
            player.PlayerId,
            0,
            GameRules.SpeedItemId,
            player.SpeedBuffStacks,
            player.Position));
    }

    private void EmitPurchaseRejected(int playerId, int itemId, PurchaseRejectReason reason)
    {
        _pendingEvents.Add(new GameEvent(
            GameEventType.PurchaseRejected,
            playerId,
            0,
            itemId,
            (int)reason,
            default));
    }

    private Vector2f NextSpawnPoint()
    {
        var spawn = _spawnPoints[_nextSpawnIndex % _spawnPoints.Length];
        _nextSpawnIndex++;
        return spawn;
    }

    private static bool IsWithinBattleBounds(Vector2f position)
    {
        return position.X is >= -20f and <= 20f &&
               position.Y is >= -20f and <= 20f;
    }

    private Vector2f ClampToBattleBounds(Vector2f position)
    {
        return new Vector2f(
            Math.Clamp(position.X, -20f, 20f),
            Math.Clamp(position.Y, -20f, 20f));
    }

    private Vector2f ClampToShopBounds(Vector2f position)
    {
        return new Vector2f(
            Math.Clamp(position.X, _shopZone.MinX, _shopZone.MaxX),
            Math.Clamp(position.Y, _shopZone.MinY, _shopZone.MaxY));
    }

    private bool IsInsideShopZone(Vector2f position)
    {
        return position.X >= _shopZone.MinX &&
               position.X <= _shopZone.MaxX &&
               position.Y >= _shopZone.MinY &&
               position.Y <= _shopZone.MaxY;
    }

    private bool IsInsideObstacle(Vector2f position)
    {
        foreach (var obstacle in _obstacles)
        {
            if (obstacle.Contains(position))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct PendingShopPurchase(int PlayerId, byte ItemId);

    private readonly record struct Aabb2(float MinX, float MinY, float MaxX, float MaxY)
    {
        public bool Contains(Vector2f position)
        {
            return position.X >= MinX &&
                   position.X <= MaxX &&
                   position.Y >= MinY &&
                   position.Y <= MaxY;
        }
    }
}
