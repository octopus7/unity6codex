#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace McpTest.VoxelVillage
{
    public static class ProceduralVillageGenerator
    {
        public static VillageLayoutData Generate(int seed, int townSize)
        {
            townSize = Mathf.Max(24, townSize);

            var builder = new VillageLayoutBuilder(seed, townSize);
            return builder.Build();
        }

        sealed class VillageLayoutBuilder
        {
            readonly System.Random _rng;
            readonly VillageLayoutData _layout;
            readonly VillageGrid _grid;
            readonly List<VillageRoadPath> _roads = new List<VillageRoadPath>();
            readonly List<VillageBuildingLayout> _buildings = new List<VillageBuildingLayout>();
            readonly List<VillageDoorLayout> _doors = new List<VillageDoorLayout>();
            readonly List<VillageFencePath> _fences = new List<VillageFencePath>();
            readonly List<VillageFoliagePlacement> _foliage = new List<VillageFoliagePlacement>();
            readonly List<VillageNpcSpawnPoint> _npcSpawns = new List<VillageNpcSpawnPoint>();

            public VillageLayoutBuilder(int seed, int townSize)
            {
                _rng = new System.Random(seed);
                _layout = new VillageLayoutData
                {
                    seed = seed,
                    townSize = townSize,
                    plazaCenter = new Vector2Int(townSize / 2, townSize / 2)
                };
                _grid = new VillageGrid(townSize, townSize);
                BuildCoreRoads();
                PlaceBuildings();
                PlaceFences();
                PlaceFoliage();
                PlaceNpcSpawns();
                _layout.roads = _roads.ToArray();
                _layout.buildings = _buildings.ToArray();
                _layout.doors = _doors.ToArray();
                _layout.fences = _fences.ToArray();
                _layout.foliage = _foliage.ToArray();
                _layout.npcSpawnPoints = _npcSpawns.ToArray();
                _grid.ApplyLayout(_layout);
            }

            public VillageLayoutData Build()
            {
                return _layout;
            }

            void BuildCoreRoads()
            {
                var center = _layout.plazaCenter;
                AddRoad("road_center_h", Line(center + new Vector2Int(-_layout.townSize / 2 + 2, 0), center + new Vector2Int(_layout.townSize / 2 - 2, 0)));
                AddRoad("road_center_v", Line(center + new Vector2Int(0, -_layout.townSize / 2 + 2), center + new Vector2Int(0, _layout.townSize / 2 - 2)));
                AddRoad("road_plaza_ring_n", Line(center + new Vector2Int(-4, 4), center + new Vector2Int(4, 4)));
                AddRoad("road_plaza_ring_s", Line(center + new Vector2Int(-4, -4), center + new Vector2Int(4, -4)));
                AddRoad("road_plaza_ring_w", Line(center + new Vector2Int(-4, -4), center + new Vector2Int(-4, 4)));
                AddRoad("road_plaza_ring_e", Line(center + new Vector2Int(4, -4), center + new Vector2Int(4, 4)));
            }

            void PlaceBuildings()
            {
                var lateralOffset = Mathf.Clamp(_layout.townSize / 8, 2, 14);
                var edgeOffset = Mathf.Clamp(_layout.townSize / 4, 6, 22);
                var sideSlots = new[] 
                {
                    new BuildingSlot("north_1", new Vector2Int(-lateralOffset, edgeOffset), new Vector2Int(0, -1)),
                    new BuildingSlot("north_2", new Vector2Int(lateralOffset, edgeOffset + 2), new Vector2Int(0, -1)),
                    new BuildingSlot("south_1", new Vector2Int(-lateralOffset, -edgeOffset), new Vector2Int(0, 1)),
                    new BuildingSlot("south_2", new Vector2Int(lateralOffset, -edgeOffset - 2), new Vector2Int(0, 1)),
                    new BuildingSlot("west_1", new Vector2Int(-edgeOffset, lateralOffset), new Vector2Int(1, 0)),
                    new BuildingSlot("west_2", new Vector2Int(-edgeOffset, -lateralOffset), new Vector2Int(1, 0)),
                    new BuildingSlot("east_1", new Vector2Int(edgeOffset, lateralOffset), new Vector2Int(-1, 0)),
                    new BuildingSlot("east_2", new Vector2Int(edgeOffset, -lateralOffset), new Vector2Int(-1, 0))
                };

                for (var slotIndex = 0; slotIndex < sideSlots.Length; slotIndex++)
                {
                    TryPlaceBuilding(sideSlots[slotIndex]);
                }
            }

            void PlaceFoliage()
            {
                var targetCount = Mathf.Clamp(_layout.townSize / 10, 10, 24);
                var attempts = 0;
                while (_foliage.Count < targetCount && attempts < targetCount * 8)
                {
                    attempts++;
                    var cell = RandomCell(margin: 3);
                    if (_grid.GetCellKind(cell) != VillageCellKind.Empty)
                    {
                        continue;
                    }

                    var kind = (VillageFoliageKind)_rng.Next(0, 4);
                    var placement = new VillageFoliagePlacement
                    {
                        id = "foliage_" + _foliage.Count,
                        kind = kind,
                        cell = cell,
                        scale = _rng.Next(1, 4)
                    };

                    _foliage.Add(placement);
                    _grid.SetCellKind(cell, VillageCellKind.Foliage);
                }
            }

            void PlaceNpcSpawns()
            {
                var anchors = BuildNpcDistrictAnchors();
                var usedCells = new HashSet<Vector2Int>();
                var npcCount = 12;
                for (var index = 0; index < npcCount; index++)
                {
                    var anchor = anchors[index % anchors.Count];
                    var cell = FindUniqueNpcSpawnCell(anchor.SpawnCell, usedCells);
                    usedCells.Add(cell);
                    var patrolCenter = FindNearestWalkable(anchor.PatrolCenter, false);
                    _npcSpawns.Add(new VillageNpcSpawnPoint
                    {
                        npcId = "npc_" + index,
                        cell = cell,
                        facing = anchor.Facing,
                        patrolCenter = patrolCenter,
                        patrolRadius = anchor.PatrolRadius
                    });
                    _grid.SetCellKind(cell, VillageCellKind.NpcSpawn);
                }
            }

            List<NpcDistrictAnchor> BuildNpcDistrictAnchors()
            {
                var anchors = new List<NpcDistrictAnchor>();
                for (var index = 0; index < _doors.Count; index++)
                {
                    var door = _doors[index];
                    anchors.Add(new NpcDistrictAnchor(
                        door.cell + (door.facing * 2),
                        door.cell + (door.facing * 4),
                        door.facing,
                        8));
                }

                var center = _layout.plazaCenter;
                var farOffset = Mathf.Clamp(_layout.townSize / 6, 6, 12);
                var farCenterOffset = Mathf.Min(farOffset + 2, (_layout.townSize / 2) - 3);
                var midOffset = Mathf.Clamp(farOffset - 4, 4, 8);
                var midCenterOffset = Mathf.Min(midOffset + 2, farOffset);
                var roadAnchors = new[]
                {
                    new NpcDistrictAnchor(center + new Vector2Int(0, farOffset), center + new Vector2Int(0, farCenterOffset), Vector2Int.down, 6),
                    new NpcDistrictAnchor(center + new Vector2Int(0, -farOffset), center + new Vector2Int(0, -farCenterOffset), Vector2Int.up, 6),
                    new NpcDistrictAnchor(center + new Vector2Int(-farOffset, 0), center + new Vector2Int(-farCenterOffset, 0), Vector2Int.right, 6),
                    new NpcDistrictAnchor(center + new Vector2Int(farOffset, 0), center + new Vector2Int(farCenterOffset, 0), Vector2Int.left, 6),
                    new NpcDistrictAnchor(center + new Vector2Int(0, midOffset), center + new Vector2Int(0, midCenterOffset), Vector2Int.down, 5),
                    new NpcDistrictAnchor(center + new Vector2Int(0, -midOffset), center + new Vector2Int(0, -midCenterOffset), Vector2Int.up, 5),
                    new NpcDistrictAnchor(center + new Vector2Int(-midOffset, 0), center + new Vector2Int(-midCenterOffset, 0), Vector2Int.right, 5),
                    new NpcDistrictAnchor(center + new Vector2Int(midOffset, 0), center + new Vector2Int(midCenterOffset, 0), Vector2Int.left, 5),
                    new NpcDistrictAnchor(center + new Vector2Int(-4, 4), center + new Vector2Int(-4, 4), Vector2Int.right, 4),
                    new NpcDistrictAnchor(center + new Vector2Int(4, 4), center + new Vector2Int(4, 4), Vector2Int.left, 4),
                    new NpcDistrictAnchor(center + new Vector2Int(-4, -4), center + new Vector2Int(-4, -4), Vector2Int.right, 4),
                    new NpcDistrictAnchor(center + new Vector2Int(4, -4), center + new Vector2Int(4, -4), Vector2Int.left, 4)
                };

                for (var index = 0; index < roadAnchors.Length; index++)
                {
                    anchors.Add(roadAnchors[index]);
                }

                return anchors;
            }

            bool TryPlaceBuilding(BuildingSlot slot)
            {
                var width = _rng.Next(5, 9);
                var height = _rng.Next(5, 8);
                var origin = ResolveOrigin(slot, width, height);
                var rect = new RectInt(origin.x, origin.y, width, height);
                if (!IsInBounds(rect) || !_grid.IsRectClear(rect))
                {
                    return false;
                }

                var building = new VillageBuildingLayout
                {
                    id = slot.Id,
                    origin = origin,
                    size = new Vector2Int(width, height),
                    height = _rng.Next(4, 8)
                };
                _buildings.Add(building);

                for (var y = origin.y; y < origin.y + height; y++)
                {
                    for (var x = origin.x; x < origin.x + width; x++)
                    {
                        _grid.SetCellKind(new Vector2Int(x, y), VillageCellKind.Building);
                    }
                }

                var door = CreateDoorForBuilding(building, slot.Facing);
                _doors.Add(door);
                _grid.SetCellKind(door.cell, VillageCellKind.DoorClosed);
                AddRoad("spur_" + slot.Id, BuildDoorSpur(door, slot.Facing));
                return true;
            }

            void PlaceFences()
            {
                for (var index = 0; index < _buildings.Count; index++)
                {
                    var building = _buildings[index];
                    var door = FindDoorForBuilding(building.id);
                    if (door == null || !TryCreateFencePath(building, door, out var fenceCells))
                    {
                        continue;
                    }

                    _fences.Add(new VillageFencePath
                    {
                        id = "fence_" + building.id,
                        cells = fenceCells
                    });

                    for (var cellIndex = 0; cellIndex < fenceCells.Length; cellIndex++)
                    {
                        _grid.SetCellKind(fenceCells[cellIndex], VillageCellKind.Fence);
                    }
                }
            }

            VillageDoorLayout CreateDoorForBuilding(VillageBuildingLayout building, Vector2Int facing)
            {
                var centerX = building.origin.x + (building.size.x / 2);
                var centerY = building.origin.y + (building.size.y / 2);
                var cell = facing switch
                {
                    { x: 0, y: -1 } => new Vector2Int(centerX, building.origin.y),
                    { x: 0, y: 1 } => new Vector2Int(centerX, building.origin.y + building.size.y - 1),
                    { x: -1, y: 0 } => new Vector2Int(building.origin.x, centerY),
                    _ => new Vector2Int(building.origin.x + building.size.x - 1, centerY),
                };

                return new VillageDoorLayout
                {
                    id = building.id + "_door",
                    buildingId = building.id,
                    cell = cell,
                    facing = facing,
                    startsOpen = false
                };
            }

            IEnumerable<Vector2Int> BuildDoorSpur(VillageDoorLayout door, Vector2Int facing)
            {
                var cells = new List<Vector2Int>();
                var current = door.cell + facing;
                var target = facing.y != 0
                    ? new Vector2Int(door.cell.x, _layout.plazaCenter.y)
                    : new Vector2Int(_layout.plazaCenter.x, door.cell.y);

                for (var i = 0; i < _layout.townSize; i++)
                {
                    if (!_grid.IsWalkable(current))
                    {
                        cells.Add(current);
                        _grid.SetCellKind(current, VillageCellKind.Road);
                    }

                    if (current == target)
                    {
                        break;
                    }

                    current += facing;
                }

                if (cells.Count == 0)
                {
                    cells.Add(door.cell + facing);
                }

                return cells;
            }

            bool TryCreateFencePath(VillageBuildingLayout building, VillageDoorLayout door, out Vector2Int[] fenceCells)
            {
                var path = BuildFencePolyline(building, door.facing);
                if (path.Count < 3)
                {
                    fenceCells = Array.Empty<Vector2Int>();
                    return false;
                }

                var seen = new HashSet<Vector2Int>();
                for (var index = 0; index < path.Count; index++)
                {
                    var cell = path[index];
                    if (!IsInBounds(cell) || _grid.GetCellKind(cell) != VillageCellKind.Empty || !seen.Add(cell))
                    {
                        fenceCells = Array.Empty<Vector2Int>();
                        return false;
                    }
                }

                fenceCells = path.ToArray();
                return true;
            }

            List<Vector2Int> BuildFencePolyline(VillageBuildingLayout building, Vector2Int facing)
            {
                var rect = new RectInt(building.origin.x, building.origin.y, building.size.x, building.size.y);
                var path = new List<Vector2Int>();

                if (facing == Vector2Int.down)
                {
                    AppendLine(path, new Vector2Int(rect.xMin - 1, rect.yMin), new Vector2Int(rect.xMin - 1, rect.yMax));
                    AppendLine(path, new Vector2Int(rect.xMin - 1, rect.yMax), new Vector2Int(rect.xMax, rect.yMax));
                    AppendLine(path, new Vector2Int(rect.xMax, rect.yMax), new Vector2Int(rect.xMax, rect.yMin));
                    return path;
                }

                if (facing == Vector2Int.up)
                {
                    AppendLine(path, new Vector2Int(rect.xMin - 1, rect.yMax - 1), new Vector2Int(rect.xMin - 1, rect.yMin - 1));
                    AppendLine(path, new Vector2Int(rect.xMin - 1, rect.yMin - 1), new Vector2Int(rect.xMax, rect.yMin - 1));
                    AppendLine(path, new Vector2Int(rect.xMax, rect.yMin - 1), new Vector2Int(rect.xMax, rect.yMax - 1));
                    return path;
                }

                if (facing == Vector2Int.left)
                {
                    AppendLine(path, new Vector2Int(rect.xMin, rect.yMin - 1), new Vector2Int(rect.xMax, rect.yMin - 1));
                    AppendLine(path, new Vector2Int(rect.xMax, rect.yMin - 1), new Vector2Int(rect.xMax, rect.yMax));
                    AppendLine(path, new Vector2Int(rect.xMax, rect.yMax), new Vector2Int(rect.xMin, rect.yMax));
                    return path;
                }

                AppendLine(path, new Vector2Int(rect.xMax - 1, rect.yMin - 1), new Vector2Int(rect.xMin - 1, rect.yMin - 1));
                AppendLine(path, new Vector2Int(rect.xMin - 1, rect.yMin - 1), new Vector2Int(rect.xMin - 1, rect.yMax));
                AppendLine(path, new Vector2Int(rect.xMin - 1, rect.yMax), new Vector2Int(rect.xMax - 1, rect.yMax));
                return path;
            }

            void AddRoad(string id, IEnumerable<Vector2Int> cells)
            {
                var path = new List<Vector2Int>();
                foreach (var cell in cells)
                {
                    if (!IsInBounds(cell))
                    {
                        continue;
                    }

                    path.Add(cell);
                    if (_grid.GetCellKind(cell) == VillageCellKind.Empty)
                    {
                        _grid.SetCellKind(cell, VillageCellKind.Road);
                    }
                }

                _roads.Add(new VillageRoadPath
                {
                    id = id,
                    cells = path.ToArray()
                });
            }

            IEnumerable<Vector2Int> Line(Vector2Int from, Vector2Int to)
            {
                var cells = new List<Vector2Int>();
                var current = from;
                cells.Add(current);

                while (current.x != to.x)
                {
                    current += new Vector2Int(Math.Sign(to.x - current.x), 0);
                    cells.Add(current);
                }

                while (current.y != to.y)
                {
                    current += new Vector2Int(0, Math.Sign(to.y - current.y));
                    cells.Add(current);
                }

                return cells;
            }

            void AppendLine(List<Vector2Int> path, Vector2Int from, Vector2Int to)
            {
                foreach (var cell in Line(from, to))
                {
                    if (path.Count == 0 || path[path.Count - 1] != cell)
                    {
                        path.Add(cell);
                    }
                }
            }

            Vector2Int ResolveOrigin(BuildingSlot slot, int width, int height)
            {
                var center = _layout.plazaCenter;
                var jitter = RandomOffset(2);
                return slot.Facing switch
                {
                    { x: 0, y: -1 } => new Vector2Int(center.x + slot.Anchor.x + jitter.x - (width / 2), center.y + slot.Anchor.y - height),
                    { x: 0, y: 1 } => new Vector2Int(center.x + slot.Anchor.x + jitter.x - (width / 2), center.y + slot.Anchor.y),
                    { x: -1, y: 0 } => new Vector2Int(center.x + slot.Anchor.x, center.y + slot.Anchor.y + jitter.y - (height / 2)),
                    { x: 1, y: 0 } => new Vector2Int(center.x + slot.Anchor.x - width, center.y + slot.Anchor.y + jitter.y - (height / 2)),
                    _ => new Vector2Int(center.x + slot.Anchor.x, center.y + slot.Anchor.y + jitter.y - (height / 2)),
                };
            }

            Vector2Int FindNearestWalkable(Vector2Int preferred, bool includeEmpty)
            {
                if (_grid.IsWalkable(preferred, includeEmpty))
                {
                    return preferred;
                }

                for (var radius = 1; radius < 8; radius++)
                {
                    for (var y = -radius; y <= radius; y++)
                    {
                        for (var x = -radius; x <= radius; x++)
                        {
                            var candidate = preferred + new Vector2Int(x, y);
                            if (_grid.IsWalkable(candidate, includeEmpty))
                            {
                                return candidate;
                            }
                        }
                    }
                }

                return _layout.plazaCenter;
            }

            Vector2Int FindUniqueNpcSpawnCell(Vector2Int preferred, HashSet<Vector2Int> usedCells)
            {
                var resolved = FindNearestWalkable(preferred, false);
                if (!usedCells.Contains(resolved))
                {
                    return resolved;
                }

                for (var radius = 1; radius <= 8; radius++)
                {
                    for (var y = -radius; y <= radius; y++)
                    {
                        for (var x = -radius; x <= radius; x++)
                        {
                            var candidate = FindNearestWalkable(preferred + new Vector2Int(x, y), false);
                            if (usedCells.Contains(candidate))
                            {
                                continue;
                            }

                            return candidate;
                        }
                    }
                }

                return resolved;
            }

            Vector2Int RandomCell(int margin)
            {
                return new Vector2Int(
                    _rng.Next(margin, _layout.townSize - margin),
                    _rng.Next(margin, _layout.townSize - margin));
            }

            Vector2Int RandomOffset(int radius)
            {
                return new Vector2Int(_rng.Next(-radius, radius + 1), _rng.Next(-radius, radius + 1));
            }

            VillageDoorLayout? FindDoorForBuilding(string buildingId)
            {
                for (var index = 0; index < _doors.Count; index++)
                {
                    var door = _doors[index];
                    if (string.Equals(door.buildingId, buildingId, StringComparison.Ordinal))
                    {
                        return door;
                    }
                }

                return null;
            }

            bool IsInBounds(RectInt rect)
            {
                return rect.xMin >= 1 && rect.yMin >= 1 && rect.xMax < _layout.townSize - 1 && rect.yMax < _layout.townSize - 1;
            }

            bool IsInBounds(Vector2Int cell)
            {
                return cell.x >= 0 && cell.x < _layout.townSize && cell.y >= 0 && cell.y < _layout.townSize;
            }

            readonly struct BuildingSlot
            {
                public BuildingSlot(string id, Vector2Int anchor, Vector2Int facing)
                {
                    Id = id;
                    Anchor = anchor;
                    Facing = facing;
                }

                public string Id { get; }
                public Vector2Int Anchor { get; }
                public Vector2Int Facing { get; }
            }

            readonly struct NpcDistrictAnchor
            {
                public NpcDistrictAnchor(Vector2Int spawnCell, Vector2Int patrolCenter, Vector2Int facing, int patrolRadius)
                {
                    SpawnCell = spawnCell;
                    PatrolCenter = patrolCenter;
                    Facing = facing;
                    PatrolRadius = patrolRadius;
                }

                public Vector2Int SpawnCell { get; }
                public Vector2Int PatrolCenter { get; }
                public Vector2Int Facing { get; }
                public int PatrolRadius { get; }
            }
        }
    }
}
