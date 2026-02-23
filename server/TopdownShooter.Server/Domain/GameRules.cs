namespace TopdownShooter.Server.Domain;

public static class GameRules
{
    public const int MaxNicknameBytes = 16;

    public const float TickDelta = 1f / 30f;
    public const int MaxHp = 100;
    public const int BulletDamage = 25;
    public const float BaseMoveSpeed = 6f;
    public const float SpeedBuffMultiplierPerStack = 0.2f;
    public const int MaxSpeedBuffStacks = 2;

    public const float ProjectileSpeed = 22f;
    public const float ProjectileSpawnOffset = 0.9f;
    public const int ProjectileLifetimeTicks = 45; // 1.5s at 30Hz
    public const float ProjectileHitRadius = 0.8f;
    public const float SpreadMinAngleDegrees = 0.5f;
    public const float SpreadMaxAngleDegrees = 10f;
    public const float SpreadIncreasePerShotDegrees = 1.2f;
    public const float SpreadRecoveryPerSecondDegrees = 7.5f;

    public const int FireCooldownTicks = 6; // 0.2s at 30Hz
    public const int RespawnDelayTicks = 150; // 5s at 30Hz
    public const float CoinPickupRadius = 1.2f;
    public const float ItemPickupRadius = 1.1f;
    public const int CoinDispenserIntervalTicks = 150; // 5s at 30Hz
    public const int CoinDispenserSpawnAmount = 1;

    public const int HealItemId = 1;
    public const int SpeedItemId = 2;
    public const int HealItemCost = 5;
    public const int SpeedItemCost = 8;
    public const int HealItemAmount = 50;

    public const int MaxCoinDropStacks = 5;
}
