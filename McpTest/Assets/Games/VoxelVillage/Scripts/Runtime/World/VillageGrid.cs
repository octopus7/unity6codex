#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace McpTest.VoxelVillage
{
    public sealed class VillageGrid
    {
        readonly VillageCellKind[] _cells;

        public VillageGrid(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _cells = new VillageCellKind[Width * Height];
        }

        public int Width { get; }

        public int Height { get; }

        public int CellCount => _cells.Length;

        public static VillageGrid FromLayout(VillageLayoutData layout)
        {
            var grid = new VillageGrid(layout.townSize, layout.townSize);
            grid.ApplyLayout(layout);
            return grid;
        }

        public void Clear(VillageCellKind fill = VillageCellKind.Empty)
        {
            for (var index = 0; index < _cells.Length; index++)
            {
                _cells[index] = fill;
            }
        }

        public VillageCellKind GetCellKind(Vector2Int cell)
        {
            if (!TryGetIndex(cell.x, cell.y, out var index))
            {
                return VillageCellKind.Empty;
            }

            return _cells[index];
        }

        public void SetCellKind(Vector2Int cell, VillageCellKind kind)
        {
            if (!TryGetIndex(cell.x, cell.y, out var index))
            {
                return;
            }

            _cells[index] = kind;
        }

        public bool IsWalkable(Vector2Int cell)
        {
            return IsWalkable(cell, true);
        }

        public bool IsWalkable(Vector2Int cell, bool includeEmpty)
        {
            if (!TryGetIndex(cell.x, cell.y, out var index))
            {
                return false;
            }

            switch (_cells[index])
            {
                case VillageCellKind.Road:
                case VillageCellKind.Plaza:
                case VillageCellKind.DoorOpen:
                case VillageCellKind.NpcSpawn:
                    return true;
                case VillageCellKind.Empty:
                    return includeEmpty;
                default:
                    return false;
            }
        }

        public bool IsRectClear(RectInt rect)
        {
            for (var y = rect.yMin; y < rect.yMax; y++)
            {
                for (var x = rect.xMin; x < rect.xMax; x++)
                {
                    if (!TryGetIndex(x, y, out var index))
                    {
                        return false;
                    }

                    if (_cells[index] != VillageCellKind.Empty)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool TrySetDoorState(Vector2Int cell, bool open)
        {
            if (!TryGetIndex(cell.x, cell.y, out var index))
            {
                return false;
            }

            if (_cells[index] != VillageCellKind.DoorClosed && _cells[index] != VillageCellKind.DoorOpen)
            {
                return false;
            }

            _cells[index] = open ? VillageCellKind.DoorOpen : VillageCellKind.DoorClosed;
            return true;
        }

        public void ApplyLayout(VillageLayoutData layout, bool doorsOpen = false)
        {
            Clear();

            for (var roadIndex = 0; roadIndex < layout.roads.Length; roadIndex++)
            {
                MarkPath(layout.roads[roadIndex].cells, VillageCellKind.Road);
            }

            for (var buildingIndex = 0; buildingIndex < layout.buildings.Length; buildingIndex++)
            {
                MarkBuilding(layout.buildings[buildingIndex]);
            }

            for (var doorIndex = 0; doorIndex < layout.doors.Length; doorIndex++)
            {
                var door = layout.doors[doorIndex];
                SetCellKind(door.cell, doorsOpen || door.startsOpen ? VillageCellKind.DoorOpen : VillageCellKind.DoorClosed);
            }

            for (var fenceIndex = 0; fenceIndex < layout.fences.Length; fenceIndex++)
            {
                MarkPath(layout.fences[fenceIndex].cells, VillageCellKind.Fence);
            }

            for (var foliageIndex = 0; foliageIndex < layout.foliage.Length; foliageIndex++)
            {
                SetCellKind(layout.foliage[foliageIndex].cell, VillageCellKind.Foliage);
            }

            for (var spawnIndex = 0; spawnIndex < layout.npcSpawnPoints.Length; spawnIndex++)
            {
                SetCellKind(layout.npcSpawnPoints[spawnIndex].cell, VillageCellKind.NpcSpawn);
            }

            var center = layout.plazaCenter;
            for (var offsetY = -2; offsetY <= 2; offsetY++)
            {
                for (var offsetX = -2; offsetX <= 2; offsetX++)
                {
                    SetCellKind(center + new Vector2Int(offsetX, offsetY), VillageCellKind.Plaza);
                }
            }
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            return FindPath(start, goal, true);
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, bool includeEmpty)
        {
            var path = new List<Vector2Int>();
            if (!TryFindPath(start, goal, path, includeEmpty))
            {
                return path;
            }

            return path;
        }

        public bool TryFindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> path)
        {
            return TryFindPath(start, goal, path, true);
        }

        public bool TryFindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> path, bool includeEmpty)
        {
            path.Clear();
            if (!IsWalkable(start, includeEmpty) || !IsWalkable(goal, includeEmpty))
            {
                return false;
            }

            var openSet = new List<Vector2Int> { start };
            var openLookup = new HashSet<int> { Encode(start.x, start.y) };
            var closed = new HashSet<int>();
            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, int>
            {
                [Encode(start.x, start.y)] = 0
            };
            var fScore = new Dictionary<int, int>
            {
                [Encode(start.x, start.y)] = Heuristic(start, goal)
            };

            while (openSet.Count > 0)
            {
                var currentIndex = FindLowestScoreIndex(openSet, fScore, gScore);
                var current = openSet[currentIndex];
                var currentKey = Encode(current.x, current.y);

                if (current == goal)
                {
                    ReconstructPath(cameFrom, currentKey, path);
                    return true;
                }

                openSet.RemoveAt(currentIndex);
                openLookup.Remove(currentKey);
                closed.Add(currentKey);

                var neighbors = GetCardinalNeighbors(current);
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighbor = neighbors[i];
                    var neighborKey = Encode(neighbor.x, neighbor.y);
                    if (closed.Contains(neighborKey) || !IsWalkable(neighbor, includeEmpty))
                    {
                        continue;
                    }

                    var tentativeG = gScore[currentKey] + 1;
                    if (!gScore.TryGetValue(neighborKey, out var existingG) || tentativeG < existingG)
                    {
                        cameFrom[neighborKey] = currentKey;
                        gScore[neighborKey] = tentativeG;
                        fScore[neighborKey] = tentativeG + Heuristic(neighbor, goal);

                        if (!openLookup.Contains(neighborKey))
                        {
                            openSet.Add(neighbor);
                            openLookup.Add(neighborKey);
                        }
                    }
                }
            }

            return false;
        }

        public bool TrySetRoadPath(IEnumerable<Vector2Int> cells)
        {
            var wroteAny = false;
            foreach (var cell in cells)
            {
                SetCellKind(cell, VillageCellKind.Road);
                wroteAny = true;
            }

            return wroteAny;
        }

        void MarkBuilding(VillageBuildingLayout building)
        {
            for (var y = building.origin.y; y < building.origin.y + building.size.y; y++)
            {
                for (var x = building.origin.x; x < building.origin.x + building.size.x; x++)
                {
                    var isPerimeter =
                        x == building.origin.x ||
                        x == building.origin.x + building.size.x - 1 ||
                        y == building.origin.y ||
                        y == building.origin.y + building.size.y - 1;
                    if (isPerimeter)
                    {
                        SetCellKind(new Vector2Int(x, y), VillageCellKind.Building);
                    }
                }
            }
        }

        void MarkPath(Vector2Int[] cells, VillageCellKind kind)
        {
            for (var index = 0; index < cells.Length; index++)
            {
                SetCellKind(cells[index], kind);
            }
        }

        List<Vector2Int> GetCardinalNeighbors(Vector2Int cell)
        {
            var neighbors = new List<Vector2Int>(4);
            AddNeighbor(neighbors, cell + Vector2Int.up);
            AddNeighbor(neighbors, cell + Vector2Int.down);
            AddNeighbor(neighbors, cell + Vector2Int.left);
            AddNeighbor(neighbors, cell + Vector2Int.right);
            return neighbors;
        }

        void AddNeighbor(List<Vector2Int> neighbors, Vector2Int cell)
        {
            if (IsInside(cell))
            {
                neighbors.Add(cell);
            }
        }

        bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        bool TryGetIndex(int x, int y, out int index)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                index = -1;
                return false;
            }

            index = (y * Width) + x;
            return true;
        }

        static int Heuristic(Vector2Int from, Vector2Int to)
        {
            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        }

        int FindLowestScoreIndex(List<Vector2Int> openSet, Dictionary<int, int> fScore, Dictionary<int, int> gScore)
        {
            var bestIndex = 0;
            var bestCell = openSet[0];
            var bestKey = Encode(bestCell.x, bestCell.y);
            var bestF = fScore.TryGetValue(bestKey, out var fValue) ? fValue : int.MaxValue;
            var bestG = gScore.TryGetValue(bestKey, out var gValue) ? gValue : int.MaxValue;

            for (var index = 1; index < openSet.Count; index++)
            {
                var cell = openSet[index];
                var key = Encode(cell.x, cell.y);
                var currentF = fScore.TryGetValue(key, out var f) ? f : int.MaxValue;
                var currentG = gScore.TryGetValue(key, out var g) ? g : int.MaxValue;
                if (currentF < bestF || (currentF == bestF && currentG < bestG))
                {
                    bestIndex = index;
                    bestCell = cell;
                    bestKey = key;
                    bestF = currentF;
                    bestG = currentG;
                }
            }

            return bestIndex;
        }

        void ReconstructPath(Dictionary<int, int> cameFrom, int currentKey, List<Vector2Int> path)
        {
            var reverse = new List<Vector2Int> { Decode(currentKey) };
            while (cameFrom.TryGetValue(currentKey, out var previous))
            {
                currentKey = previous;
                reverse.Add(Decode(currentKey));
            }

            reverse.Reverse();
            path.AddRange(reverse);
        }

        int Encode(int x, int y)
        {
            return (y * Width) + x;
        }

        Vector2Int Decode(int key)
        {
            var y = key / Width;
            var x = key % Width;
            return new Vector2Int(x, y);
        }
    }
}
