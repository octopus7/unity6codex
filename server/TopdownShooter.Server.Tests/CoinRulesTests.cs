using TopdownShooter.Server.Domain;

namespace TopdownShooter.Server.Tests;

public sealed class CoinRulesTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(9, 9)]
    public void ComputeDropAmount_AppliesMinimumOneRule(int carriedCoins, int expectedDrop)
    {
        var dropAmount = CoinRules.ComputeDropAmount(carriedCoins);
        Assert.Equal(expectedDrop, dropAmount);
    }

    [Fact]
    public void SplitIntoStacks_MatchesTotalAndCountConstraints()
    {
        var random = new Random(123);
        var stacks = CoinRules.SplitIntoStacks(totalAmount: 37, maxStacks: 5, random);

        Assert.NotEmpty(stacks);
        Assert.True(stacks.Count <= 5);
        Assert.All(stacks, amount => Assert.True(amount >= 1));
        Assert.Equal(37, stacks.Sum());
    }
}
