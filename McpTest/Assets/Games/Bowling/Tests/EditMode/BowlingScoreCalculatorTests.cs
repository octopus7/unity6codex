#nullable enable

using NUnit.Framework;

namespace McpTest.Bowling.Tests
{
    public sealed class BowlingScoreCalculatorTests
    {
        [Test]
        public void PerfectGameScoresThreeHundred()
        {
            var scorecard = BowlingScoreCalculator.BuildScorecard(new[]
            {
                10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10
            });

            Assert.That(scorecard.TotalScore, Is.EqualTo(300));
            Assert.That(scorecard.FrameTotals[9], Is.EqualTo(300));
        }

        [Test]
        public void AllSparesWithFiveScoreOneHundredFifty()
        {
            var scorecard = BowlingScoreCalculator.BuildScorecard(new[]
            {
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5
            });

            Assert.That(scorecard.TotalScore, Is.EqualTo(150));
            Assert.That(scorecard.FrameMarks[0], Is.EqualTo("5 /"));
            Assert.That(scorecard.FrameMarks[9], Is.EqualTo("5 / 5"));
        }

        [Test]
        public void MixedGameAccumulatesBonusesCorrectly()
        {
            var scorecard = BowlingScoreCalculator.BuildScorecard(new[]
            {
                10,
                7, 3,
                9, 0,
                10,
                0, 8,
                8, 2,
                0, 6,
                10,
                10,
                10, 8, 1
            });

            Assert.That(scorecard.TotalScore, Is.EqualTo(167));
            Assert.That(scorecard.FrameTotals[0], Is.EqualTo(20));
            Assert.That(scorecard.FrameTotals[5], Is.EqualTo(84));
            Assert.That(scorecard.FrameTotals[9], Is.EqualTo(167));
        }
    }
}
