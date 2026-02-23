namespace TopdownShooter.Server.Domain;

public static class CoinRules
{
    public static int ComputeDropAmount(int carriedCoins)
    {
        return Math.Max(1, carriedCoins);
    }

    public static List<int> SplitIntoStacks(int totalAmount, int maxStacks, Random random)
    {
        if (totalAmount <= 0)
        {
            return new List<int> { 1 };
        }

        var stackCount = Math.Max(1, Math.Min(maxStacks, totalAmount));
        var stacks = new List<int>(capacity: stackCount);
        for (var i = 0; i < stackCount; i++)
        {
            stacks.Add(1);
        }

        var remaining = totalAmount - stackCount;
        for (var i = 0; i < remaining; i++)
        {
            var index = random.Next(stackCount);
            stacks[index]++;
        }

        return stacks;
    }
}
