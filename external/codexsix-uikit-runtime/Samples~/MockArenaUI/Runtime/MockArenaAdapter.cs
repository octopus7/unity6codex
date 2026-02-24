using UnityEngine;

namespace CodexSix.UiKit.Runtime.Samples.MockArena
{
    public readonly record struct MockArenaHudState(int Hp, int Coins, bool InShop, int Combo);

    public readonly record struct MockAchievementUnlocked(string AchievementId, string Title, string Description);

    public sealed class MockArenaAdapter : MonoBehaviour
    {
        [SerializeField] private UiContext _context;
        [SerializeField] private float _tickIntervalSeconds = 0.8f;
        [SerializeField] private int _randomSeed = 20260224;

        private readonly UiStateChannel<MockArenaHudState> _hudStateChannel = new();

        private System.Random _random;
        private float _elapsed;
        private int _hp = 100;
        private int _coins;
        private int _combo;
        private bool _inShop;
        private bool _achievement50Issued;
        private bool _achievement100Issued;

        public IUiStateChannel<MockArenaHudState> HudState => _hudStateChannel;

        private void Awake()
        {
            if (_context == null)
            {
                _context = FindFirstObjectByType<UiContext>();
            }

            _random = new System.Random(_randomSeed);
            Publish();
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < _tickIntervalSeconds)
            {
                return;
            }

            _elapsed = 0f;
            SimulateStep();
            Publish();
        }

        private void SimulateStep()
        {
            var roll = _random.NextDouble();
            if (roll < 0.20)
            {
                _hp = Mathf.Max(0, _hp - _random.Next(4, 14));
                _combo = 0;
            }
            else if (roll < 0.45)
            {
                _hp = Mathf.Min(100, _hp + _random.Next(3, 9));
            }
            else
            {
                _coins += _random.Next(2, 11);
                _combo = Mathf.Min(20, _combo + 1);
            }

            if (_random.NextDouble() < 0.12)
            {
                _inShop = !_inShop;
            }

            EmitAchievements();
        }

        private void EmitAchievements()
        {
            if (_context == null)
            {
                return;
            }

            if (!_achievement50Issued && _coins >= 50)
            {
                _achievement50Issued = true;
                _context.SignalBus.Publish(new MockAchievementUnlocked(
                    "coin_50",
                    "Coin Runner",
                    "Collected 50 coins in mock arena."));
            }

            if (!_achievement100Issued && _coins >= 100)
            {
                _achievement100Issued = true;
                _context.SignalBus.Publish(new MockAchievementUnlocked(
                    "coin_100",
                    "Coin Hoarder",
                    "Collected 100 coins in mock arena."));
            }
        }

        private void Publish()
        {
            _hudStateChannel.Publish(new MockArenaHudState(_hp, _coins, _inShop, _combo));
        }
    }
}