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
            Assert.AreEqual(4, layout.trafficSignals.Length);
        }

        [Test]
        public void Generate_PlacesTrafficSignalsOnFourPlazaApproaches()
        {
            var layout = ProceduralVillageGenerator.Generate(12345, 72);
            var grid = VillageGrid.FromLayout(layout);
            var signalIds = new HashSet<string>();
            var northSouthCount = 0;
            var eastWestCount = 0;

            Assert.AreEqual(4, layout.trafficSignals.Length);

            for (var index = 0; index < layout.trafficSignals.Length; index++)
            {
                var signal = layout.trafficSignals[index];
                var offset = signal.cell - layout.plazaCenter;

                Assert.IsTrue(signalIds.Add(signal.id), $"Traffic signal {signal.id} should be unique.");
                Assert.AreEqual(5, Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)), $"Traffic signal {signal.id} should sit on a plaza approach.");
                Assert.AreNotEqual(VillageCellKind.Building, grid.GetCellKind(signal.cell), $"Traffic signal {signal.id} must not overlap a building.");

                if (signal.phaseGroup == VillageTrafficSignalPhaseGroup.NorthSouth)
                {
                    northSouthCount++;
                    Assert.IsTrue(signal.facing == Vector2Int.up || signal.facing == Vector2Int.down, $"North/south signal {signal.id} should face along the vertical road.");
                    continue;
                }

                eastWestCount++;
                Assert.IsTrue(signal.facing == Vector2Int.left || signal.facing == Vector2Int.right, $"East/west signal {signal.id} should face along the horizontal road.");
            }

            Assert.AreEqual(2, northSouthCount);
            Assert.AreEqual(2, eastWestCount);
        }

        [Test]
        public void Generate_NpcSpawnsAreDistributedAcrossVillageDistricts()
        {
            var layout = ProceduralVillageGenerator.Generate(12345, 72);
            var grid = VillageGrid.FromLayout(layout);
            var northCount = 0;
            var southCount = 0;
            var eastCount = 0;
            var westCount = 0;
            var distantCount = 0;

            for (var index = 0; index < layout.npcSpawnPoints.Length; index++)
            {
                var spawn = layout.npcSpawnPoints[index];
                var centerOffset = spawn.patrolCenter - layout.plazaCenter;
                var plazaDistance = Mathf.Abs(spawn.cell.x - layout.plazaCenter.x) + Mathf.Abs(spawn.cell.y - layout.plazaCenter.y);

                Assert.IsTrue(grid.IsWalkable(spawn.patrolCenter, false), $"Patrol center for npc spawn {index} should stay on NPC walkable cells.");
                Assert.That(spawn.patrolRadius, Is.GreaterThanOrEqualTo(5), $"Patrol radius for npc spawn {index} should define a meaningful area.");

                if (centerOffset.y > 0)
                {
                    northCount++;
                }

                if (centerOffset.y < 0)
                {
                    southCount++;
                }

                if (centerOffset.x > 0)
                {
                    eastCount++;
                }

                if (centerOffset.x < 0)
                {
                    westCount++;
                }

                if (plazaDistance >= 6)
                {
                    distantCount++;
                }
            }

            Assert.Greater(northCount, 0);
            Assert.Greater(southCount, 0);
            Assert.Greater(eastCount, 0);
            Assert.Greater(westCount, 0);
            Assert.GreaterOrEqual(distantCount, 10, "Most villagers should start outside the plaza core.");
        }

        [Test]
        public void Generate_NpcSpawnsCanReachTheirAssignedPatrolCenters()
        {
            var layout = ProceduralVillageGenerator.Generate(24601, 72);
            var grid = VillageGrid.FromLayout(layout);
            var path = new List<Vector2Int>();
            var patrolCells = new List<Vector2Int>();

            for (var index = 0; index < layout.npcSpawnPoints.Length; index++)
            {
                var spawn = layout.npcSpawnPoints[index];

                Assert.IsTrue(grid.TryFindPath(spawn.cell, spawn.patrolCenter, path, false), $"Npc spawn {index} should connect to its patrol center.");

                grid.CollectReachableCells(spawn.patrolCenter, spawn.patrolRadius, patrolCells, false);
                Assert.Greater(patrolCells.Count, 3, $"Npc spawn {index} should have multiple patrol cells in its assigned area.");
                CollectionAssert.Contains(patrolCells, spawn.patrolCenter);
            }
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
