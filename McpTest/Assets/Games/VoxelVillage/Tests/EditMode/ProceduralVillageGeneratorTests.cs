#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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
        public void Generate_FencesStayOpenWithoutBranches()
        {
            var layout = ProceduralVillageGenerator.Generate(24680, 72);

            Assert.Greater(layout.fences.Length, 0);

            for (var fenceIndex = 0; fenceIndex < layout.fences.Length; fenceIndex++)
            {
                var fence = layout.fences[fenceIndex];
                var lookup = new HashSet<Vector2Int>(fence.cells);

                Assert.AreEqual(fence.cells.Length, lookup.Count, $"Fence {fence.id} contains duplicate cells.");

                var endpoints = 0;
                for (var cellIndex = 0; cellIndex < fence.cells.Length; cellIndex++)
                {
                    var neighborCount = CountFenceNeighbors(lookup, fence.cells[cellIndex]);
                    Assert.That(neighborCount, Is.GreaterThan(0).And.LessThanOrEqualTo(2), $"Fence {fence.id} should remain a simple path.");
                    if (neighborCount == 1)
                    {
                        endpoints++;
                    }
                }

                Assert.AreEqual(2, endpoints, $"Fence {fence.id} should stay open.");
            }
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
        public void Grid_BuildingInteriorsStayOpenForPlayerMovement()
        {
            var layout = ProceduralVillageGenerator.Generate(13579, 72);
            var grid = VillageGrid.FromLayout(layout);

            Assert.Greater(layout.buildings.Length, 0);
            Assert.AreEqual(layout.buildings.Length, layout.doors.Length);

            for (var index = 0; index < layout.doors.Length; index++)
            {
                var door = layout.doors[index];
                var insideCell = door.cell - door.facing;

                Assert.AreEqual(VillageCellKind.Empty, grid.GetCellKind(insideCell), $"Interior cell behind {door.id} should stay empty.");
                Assert.IsTrue(grid.IsWalkable(insideCell, true), $"Player should be able to move inside {door.id} once the door opens.");
                Assert.IsFalse(grid.IsWalkable(insideCell, false), $"NPC routing should still avoid empty interior cells for {door.id}.");
            }
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

        [Test]
        public void Grid_FenceCellsRemainBlocked()
        {
            var layout = ProceduralVillageGenerator.Generate(54321, 72);
            var grid = VillageGrid.FromLayout(layout);

            Assert.Greater(layout.fences.Length, 0);

            for (var fenceIndex = 0; fenceIndex < layout.fences.Length; fenceIndex++)
            {
                var fence = layout.fences[fenceIndex];
                for (var cellIndex = 0; cellIndex < fence.cells.Length; cellIndex++)
                {
                    var cell = fence.cells[cellIndex];
                    Assert.AreEqual(VillageCellKind.Fence, grid.GetCellKind(cell));
                    Assert.IsFalse(grid.IsWalkable(cell), $"Fence cell {cell} in {fence.id} must block movement.");
                }
            }
        }

        static int CountFenceNeighbors(HashSet<Vector2Int> lookup, Vector2Int cell)
        {
            var count = 0;
            if (lookup.Contains(cell + Vector2Int.up))
            {
                count++;
            }

            if (lookup.Contains(cell + Vector2Int.right))
            {
                count++;
            }

            if (lookup.Contains(cell + Vector2Int.down))
            {
                count++;
            }

            if (lookup.Contains(cell + Vector2Int.left))
            {
                count++;
            }

            return count;
        }
    }
}
