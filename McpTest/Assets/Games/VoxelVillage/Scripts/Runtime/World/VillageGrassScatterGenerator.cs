#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace McpTest.VoxelVillage
{
    public enum VillageGrassVariant
    {
        PatchA,
        PatchB,
        PatchC,
        PatchD
    }

    public readonly struct VillageGrassPlacement
    {
        public VillageGrassPlacement(Vector2Int cell, VillageGrassVariant variant, Vector2 cellOffset, float yaw)
        {
            Cell = cell;
            Variant = variant;
            CellOffset = new Vector2(
                Mathf.Clamp(cellOffset.x, -0.35f, 0.35f),
                Mathf.Clamp(cellOffset.y, -0.35f, 0.35f));
            Yaw = yaw;
        }

        public Vector2Int Cell { get; }

        public VillageGrassVariant Variant { get; }

        public Vector2 CellOffset { get; }

        public float Yaw { get; }
    }

    public static class VillageGrassScatterGenerator
    {
        const int SectorSize = 9;
        const int MinCandidatesPerSector = 8;
        const int MinCellSpacing = 2;
        const int PlazaClearRadius = 6;
        const int EdgeMargin = 2;

        public static VillageGrassPlacement[] Generate(VillageLayoutData layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            return Generate(layout, VillageGrid.FromLayout(layout));
        }

        public static VillageGrassPlacement[] Generate(VillageLayoutData layout, VillageGrid grid)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            var rng = new System.Random(layout.seed ^ 0x5F3759DF);
            var placements = new List<VillageGrassPlacement>(grid.CellCount / 48);
            var sectorCountX = Mathf.CeilToInt(grid.Width / (float)SectorSize);
            var sectorCountY = Mathf.CeilToInt(grid.Height / (float)SectorSize);

            for (var sectorY = 0; sectorY < sectorCountY; sectorY++)
            {
                for (var sectorX = 0; sectorX < sectorCountX; sectorX++)
                {
                    var rect = new RectInt(
                        sectorX * SectorSize,
                        sectorY * SectorSize,
                        Mathf.Min(SectorSize, grid.Width - (sectorX * SectorSize)),
                        Mathf.Min(SectorSize, grid.Height - (sectorY * SectorSize)));
                    PlaceSectorGrass(layout, grid, rect, rng, placements);
                }
            }

            return placements.ToArray();
        }

        static void PlaceSectorGrass(
            VillageLayoutData layout,
            VillageGrid grid,
            RectInt sectorRect,
            System.Random rng,
            List<VillageGrassPlacement> placements)
        {
            var candidates = CollectCandidates(layout, grid, sectorRect);
            if (candidates.Count < MinCandidatesPerSector)
            {
                return;
            }

            var desiredCount = candidates.Count >= 42 ? 2 : 1;
            for (var index = 0; index < desiredCount; index++)
            {
                if (!TryTakeCandidate(candidates, placements, rng, out var cell))
                {
                    break;
                }

                var variant = (VillageGrassVariant)((placements.Count + (layout.seed & 3)) % 4);
                var placement = new VillageGrassPlacement(
                    cell,
                    variant,
                    new Vector2(RandomRange(rng, -0.24f, 0.24f), RandomRange(rng, -0.24f, 0.24f)),
                    RandomRange(rng, 0f, 360f));

                placements.Add(placement);
                PruneNearbyCandidates(candidates, cell);
            }
        }

        static List<Vector2Int> CollectCandidates(VillageLayoutData layout, VillageGrid grid, RectInt sectorRect)
        {
            var candidates = new List<Vector2Int>(sectorRect.width * sectorRect.height);
            for (var y = sectorRect.yMin; y < sectorRect.yMax; y++)
            {
                for (var x = sectorRect.xMin; x < sectorRect.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!IsGrassCandidate(layout, grid, cell))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            return candidates;
        }

        static bool IsGrassCandidate(VillageLayoutData layout, VillageGrid grid, Vector2Int cell)
        {
            if (cell.x < EdgeMargin ||
                cell.y < EdgeMargin ||
                cell.x >= grid.Width - EdgeMargin ||
                cell.y >= grid.Height - EdgeMargin)
            {
                return false;
            }

            if (grid.GetCellKind(cell) != VillageCellKind.Empty)
            {
                return false;
            }

            var delta = cell - layout.plazaCenter;
            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) <= PlazaClearRadius)
            {
                return false;
            }

            return true;
        }

        static bool TryTakeCandidate(
            List<Vector2Int> candidates,
            List<VillageGrassPlacement> placements,
            System.Random rng,
            out Vector2Int cell)
        {
            while (candidates.Count > 0)
            {
                var candidateIndex = rng.Next(candidates.Count);
                cell = candidates[candidateIndex];
                candidates.RemoveAt(candidateIndex);

                if (HasNearbyPlacement(placements, cell))
                {
                    continue;
                }

                return true;
            }

            cell = default;
            return false;
        }

        static bool HasNearbyPlacement(List<VillageGrassPlacement> placements, Vector2Int cell)
        {
            for (var index = 0; index < placements.Count; index++)
            {
                var existing = placements[index].Cell;
                if (Mathf.Abs(existing.x - cell.x) <= MinCellSpacing &&
                    Mathf.Abs(existing.y - cell.y) <= MinCellSpacing)
                {
                    return true;
                }
            }

            return false;
        }

        static void PruneNearbyCandidates(List<Vector2Int> candidates, Vector2Int cell)
        {
            for (var index = candidates.Count - 1; index >= 0; index--)
            {
                var candidate = candidates[index];
                if (Mathf.Abs(candidate.x - cell.x) <= MinCellSpacing &&
                    Mathf.Abs(candidate.y - cell.y) <= MinCellSpacing)
                {
                    candidates.RemoveAt(index);
                }
            }
        }

        static float RandomRange(System.Random rng, float minInclusive, float maxInclusive)
        {
            return Mathf.Lerp(minInclusive, maxInclusive, (float)rng.NextDouble());
        }
    }
}
