using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CodexSix.TopdownShooter.Tests
{
    public sealed class GrowthSystemEditModeTests
    {
        [Test]
        public void EvaluateNode_RequiresBothCurrencies_WhenNodeHasCoinAndGemCost()
        {
            var node = CreateNode(
                id: "atk_01",
                tier: 1,
                coinCost: 30,
                gemCost: 2,
                requiredNodeIds: Array.Empty<string>(),
                requiredOpenedFromRequiredNodes: 0,
                minOpenedNodesInLowerTiers: 0);

            var tree = CreateTree(new[] { node });
            var unlocked = new HashSet<string>();

            var blockedByGem = EvaluateNode(tree, node, unlocked, availableCoins: 30, gemBalance: 1);
            Assert.False(GetBool(blockedByGem, "CanUnlock"));
            Assert.False(GetBool(blockedByGem, "MeetsCost"));
            Assert.AreEqual("LockedCost", GetEnumName(blockedByGem, "VisualState"));

            var unlockable = EvaluateNode(tree, node, unlocked, availableCoins: 30, gemBalance: 2);
            Assert.True(GetBool(unlockable, "CanUnlock"));
            Assert.True(GetBool(unlockable, "MeetsCost"));
            Assert.AreEqual("Unlockable", GetEnumName(unlockable, "VisualState"));
        }

        [Test]
        public void EvaluateNode_FailsWhenRequiredOpenedCountNotMet()
        {
            var prereqA = CreateNode("a", tier: 0);
            var prereqB = CreateNode("b", tier: 0);
            var target = CreateNode(
                id: "target",
                tier: 1,
                coinCost: 0,
                gemCost: 0,
                requiredNodeIds: new[] { "a", "b" },
                requiredOpenedFromRequiredNodes: 2,
                minOpenedNodesInLowerTiers: 0);

            var tree = CreateTree(new[] { prereqA, prereqB, target });
            var unlocked = new HashSet<string> { "a" };
            var evaluation = EvaluateNode(tree, target, unlocked, availableCoins: 999, gemBalance: 999);

            Assert.False(GetBool(evaluation, "MeetsPrerequisite"));
            Assert.False(GetBool(evaluation, "CanUnlock"));
            Assert.AreEqual(1, GetInt(evaluation, "OpenedRequiredCount"));
            Assert.AreEqual(2, GetInt(evaluation, "RequiredNodeCount"));
        }

        [Test]
        public void EvaluateNode_UsesTreeWideLowerTierOpenedCount()
        {
            var lowA = CreateNode("low_a", tier: 0);
            var lowB = CreateNode("low_b", tier: 1);
            var high = CreateNode(
                id: "high",
                tier: 3,
                coinCost: 0,
                gemCost: 0,
                requiredNodeIds: Array.Empty<string>(),
                requiredOpenedFromRequiredNodes: 0,
                minOpenedNodesInLowerTiers: 2);

            var tree = CreateTree(new[] { lowA, lowB, high });

            var unlockedOnlyOne = new HashSet<string> { "low_a" };
            var blocked = EvaluateNode(tree, high, unlockedOnlyOne, availableCoins: 0, gemBalance: 0);
            Assert.False(GetBool(blocked, "MeetsPrerequisite"));

            var unlockedTwo = new HashSet<string> { "low_a", "low_b" };
            var unlocked = EvaluateNode(tree, high, unlockedTwo, availableCoins: 0, gemBalance: 0);
            Assert.True(GetBool(unlocked, "MeetsPrerequisite"));
            Assert.True(GetBool(unlocked, "CanUnlock"));
        }

        [Test]
        public void ParseTree_FailsWhenRequiredNodeIsMissing()
        {
            const string invalidJson = "{" +
                                       "\"treeId\":\"default\"," +
                                       "\"version\":1," +
                                       "\"nodes\":[{" +
                                       "\"id\":\"n1\"," +
                                       "\"name\":\"Node 1\"," +
                                       "\"description\":\"\"," +
                                       "\"tier\":1," +
                                       "\"branchId\":\"combat\"," +
                                       "\"x\":0," +
                                       "\"y\":0," +
                                       "\"coinCost\":0," +
                                       "\"gemCost\":0," +
                                       "\"requiredNodeIds\":[\"unknown\"]," +
                                       "\"requiredOpenedFromRequiredNodes\":1," +
                                       "\"minOpenedNodesInLowerTiers\":0" +
                                       "}]}";

            var managerType = GetRuntimeType("CodexSix.TopdownShooter.Game.GrowthTreeDataManager");
            var parseMethod = managerType.GetMethod("TryParseTree", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(parseMethod);

            var args = new object[] { invalidJson, null, null };
            var success = (bool)parseMethod.Invoke(null, args);
            var errors = args[2] as IList;

            Assert.False(success);
            Assert.NotNull(errors);
            Assert.Greater(errors.Count, 0);
        }

        private static object CreateNode(
            string id,
            int tier,
            int coinCost = 0,
            int gemCost = 0,
            string[] requiredNodeIds = null,
            int requiredOpenedFromRequiredNodes = 0,
            int minOpenedNodesInLowerTiers = 0)
        {
            var nodeType = GetRuntimeType("CodexSix.TopdownShooter.Game.GrowthNodeDefinition");
            var constructor = nodeType.GetConstructors().Single();
            return constructor.Invoke(new object[]
            {
                id,
                id,
                string.Empty,
                tier,
                "default",
                0f,
                0f,
                coinCost,
                gemCost,
                requiredNodeIds ?? Array.Empty<string>(),
                requiredOpenedFromRequiredNodes,
                minOpenedNodesInLowerTiers
            });
        }

        private static object CreateTree(IReadOnlyList<object> nodes)
        {
            var nodeType = GetRuntimeType("CodexSix.TopdownShooter.Game.GrowthNodeDefinition");
            var listType = typeof(List<>).MakeGenericType(nodeType);
            var typedList = (IList)Activator.CreateInstance(listType);
            for (var i = 0; i < nodes.Count; i++)
            {
                typedList.Add(nodes[i]);
            }

            var treeType = GetRuntimeType("CodexSix.TopdownShooter.Game.GrowthTreeDefinition");
            var constructor = treeType.GetConstructors().Single();
            return constructor.Invoke(new object[] { "default", 1, typedList });
        }

        private static object EvaluateNode(object tree, object node, HashSet<string> unlocked, int availableCoins, int gemBalance)
        {
            var evaluatorType = GetRuntimeType("CodexSix.TopdownShooter.Game.GrowthUnlockEvaluator");
            var evaluateMethod = evaluatorType.GetMethod("EvaluateNode", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(evaluateMethod);
            return evaluateMethod.Invoke(null, new object[] { tree, node, unlocked, availableCoins, gemBalance });
        }

        private static Type GetRuntimeType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            if (type != null)
            {
                return type;
            }

            var runtimeAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));

            type = runtimeAssembly?.GetType(fullName);
            Assert.NotNull(type, $"Runtime type not found: {fullName}");
            return type;
        }

        private static bool GetBool(object instance, string propertyName)
        {
            return (bool)instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
        }

        private static int GetInt(object instance, string propertyName)
        {
            return (int)instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
        }

        private static string GetEnumName(object instance, string propertyName)
        {
            var value = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
            return value != null ? value.ToString() : string.Empty;
        }
    }
}
