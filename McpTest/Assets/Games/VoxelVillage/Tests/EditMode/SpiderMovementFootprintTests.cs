#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class SpiderMovementFootprintTests
    {
        [Test]
        public void Grid_Spider2x2RequiresClearanceWhileSqueezedSpiderCanUseSingleRoad()
        {
            var grid = new VillageGrid(6, 6);

            grid.SetCellKind(new Vector2Int(1, 1), VillageCellKind.Plaza);
            grid.SetCellKind(new Vector2Int(2, 1), VillageCellKind.Plaza);
            grid.SetCellKind(new Vector2Int(1, 2), VillageCellKind.Plaza);
            grid.SetCellKind(new Vector2Int(2, 2), VillageCellKind.Plaza);
            grid.SetCellKind(new Vector2Int(0, 4), VillageCellKind.Road);
            grid.SetCellKind(new Vector2Int(1, 4), VillageCellKind.Road);
            grid.SetCellKind(new Vector2Int(2, 4), VillageCellKind.Road);

            Assert.IsTrue(grid.IsWalkable(new Vector2Int(1, 1), false, MovementFootprint.Spider2x2));
            Assert.IsFalse(grid.IsWalkable(new Vector2Int(0, 4), false, MovementFootprint.Spider2x2));
            Assert.IsTrue(grid.IsWalkable(new Vector2Int(0, 4), false, MovementFootprint.SqueezedSpider1x1));
        }

        [Test]
        public void Generate_CreatesThreatAnchorsForAmbientSpiderRouting()
        {
            var layout = ProceduralVillageGenerator.Generate(424242, 72);
            var grid = VillageGrid.FromLayout(layout);

            Assert.That(layout.threatAnchors.Length, Is.GreaterThanOrEqualTo(4));

            for (var index = 0; index < layout.threatAnchors.Length; index++)
            {
                Assert.IsTrue(
                    grid.IsWalkable(layout.threatAnchors[index].cell, false, MovementFootprint.SqueezedSpider1x1),
                    $"Threat anchor {layout.threatAnchors[index].id} should be spider-walkable.");
            }
        }
    }
}
