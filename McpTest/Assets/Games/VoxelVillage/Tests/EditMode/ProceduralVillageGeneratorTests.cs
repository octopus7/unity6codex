#nullable enable

using NUnit.Framework;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class ProceduralVillageGeneratorTests
    {
        [Test]
        public void Generate_ProducesBuildingsDoorsFoliageAndTwelveNpcSpawns()
        {
            var layout = ProceduralVillageGenerator.Generate(12345, 72);

            Assert.Greater(layout.buildings.Length, 0);
            Assert.AreEqual(layout.buildings.Length, layout.doors.Length);
            Assert.Greater(layout.foliage.Length, 0);
            Assert.AreEqual(12, layout.npcSpawnPoints.Length);
        }

        [Test]
        public void Generate_DifferentSeedsChangeLayout()
        {
            var first = ProceduralVillageGenerator.Generate(111, 72);
            var second = ProceduralVillageGenerator.Generate(222, 72);

            var sameBuildings = first.buildings.Length == second.buildings.Length;
            if (sameBuildings)
            {
                for (var index = 0; index < first.buildings.Length; index++)
                {
                    if (first.buildings[index].origin != second.buildings[index].origin ||
                        first.buildings[index].size != second.buildings[index].size)
                    {
                        sameBuildings = false;
                        break;
                    }
                }
            }

            var sameFoliage = first.foliage.Length == second.foliage.Length;
            if (sameFoliage)
            {
                for (var index = 0; index < first.foliage.Length; index++)
                {
                    if (first.foliage[index].cell != second.foliage[index].cell ||
                        first.foliage[index].kind != second.foliage[index].kind)
                    {
                        sameFoliage = false;
                        break;
                    }
                }
            }

            Assert.IsFalse(sameBuildings && sameFoliage);
        }

        [Test]
        public void Grid_DoorStateChangesDoorCellWalkability()
        {
            var layout = ProceduralVillageGenerator.Generate(9876, 72);
            var grid = VillageGrid.FromLayout(layout);
            var door = layout.doors[0];

            Assert.IsFalse(grid.IsWalkable(door.cell));
            Assert.IsTrue(grid.TrySetDoorState(door.cell, true));
            Assert.IsTrue(grid.IsWalkable(door.cell));
            Assert.IsTrue(grid.TrySetDoorState(door.cell, false));
            Assert.IsFalse(grid.IsWalkable(door.cell));
        }

        [Test]
        public void Grid_PathfindingCanExcludeEmptyTerrainForNpcRouting()
        {
            var grid = new VillageGrid(4, 3);
            grid.SetCellKind(new UnityEngine.Vector2Int(0, 1), VillageCellKind.Road);
            grid.SetCellKind(new UnityEngine.Vector2Int(1, 1), VillageCellKind.Road);
            grid.SetCellKind(new UnityEngine.Vector2Int(2, 1), VillageCellKind.Road);
            grid.SetCellKind(new UnityEngine.Vector2Int(3, 1), VillageCellKind.Road);

            var path = new System.Collections.Generic.List<UnityEngine.Vector2Int>();

            Assert.IsTrue(grid.TryFindPath(new UnityEngine.Vector2Int(0, 1), new UnityEngine.Vector2Int(3, 1), path, false));
            Assert.AreEqual(4, path.Count);
            Assert.IsFalse(grid.TryFindPath(new UnityEngine.Vector2Int(0, 0), new UnityEngine.Vector2Int(3, 0), path, false));
        }

        [Test]
        public void Grid_OutOfBoundsCellsAreNeverWalkable()
        {
            var grid = new VillageGrid(4, 4);

            Assert.IsFalse(grid.IsWalkable(new UnityEngine.Vector2Int(-1, 0), true));
            Assert.IsFalse(grid.IsWalkable(new UnityEngine.Vector2Int(4, 4), true));
        }
    }
}
