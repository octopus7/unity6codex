#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class VillageGrassScatterGeneratorTests
    {
        [Test]
        public void Generate_UsesAllVariantsAndKeepsPlazaClear()
        {
            var layout = ProceduralVillageGenerator.Generate(24680, 72);
            var placements = VillageGrassScatterGenerator.Generate(layout);
            var variants = new HashSet<VillageGrassVariant>();

            Assert.Greater(placements.Length, 24);

            for (var index = 0; index < placements.Length; index++)
            {
                var placement = placements[index];
                variants.Add(placement.Variant);

                var delta = placement.Cell - layout.plazaCenter;
                Assert.Greater(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)), 6);
            }

            Assert.AreEqual(4, variants.Count);
        }

        [Test]
        public void Generate_IsDeterministicAndOnlyUsesEmptyCells()
        {
            var layout = ProceduralVillageGenerator.Generate(97531, 72);
            var firstGrid = VillageGrid.FromLayout(layout);
            var secondGrid = VillageGrid.FromLayout(layout);
            var first = VillageGrassScatterGenerator.Generate(layout, firstGrid);
            var second = VillageGrassScatterGenerator.Generate(layout, secondGrid);

            Assert.AreEqual(first.Length, second.Length);

            for (var index = 0; index < first.Length; index++)
            {
                Assert.AreEqual(VillageCellKind.Empty, firstGrid.GetCellKind(first[index].Cell));
                Assert.AreEqual(first[index].Cell, second[index].Cell);
                Assert.AreEqual(first[index].Variant, second[index].Variant);
                Assert.AreEqual(first[index].CellOffset, second[index].CellOffset);
                Assert.AreEqual(first[index].Yaw, second[index].Yaw);
            }
        }

        [Test]
        public void Generate_SpreadsPlacementsAcrossAllQuadrants()
        {
            var layout = ProceduralVillageGenerator.Generate(123456, 72);
            var placements = VillageGrassScatterGenerator.Generate(layout);
            var quadrants = new int[4];

            for (var index = 0; index < placements.Length; index++)
            {
                var cell = placements[index].Cell;
                var east = cell.x >= layout.plazaCenter.x ? 1 : 0;
                var north = cell.y >= layout.plazaCenter.y ? 1 : 0;
                quadrants[north * 2 + east]++;
            }

            var minimumPerQuadrant = Mathf.Max(4, placements.Length / 10);
            for (var index = 0; index < quadrants.Length; index++)
            {
                Assert.GreaterOrEqual(quadrants[index], minimumPerQuadrant);
            }
        }
    }
}
