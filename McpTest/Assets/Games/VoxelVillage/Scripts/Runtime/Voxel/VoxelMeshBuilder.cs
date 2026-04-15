#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace McpTest.VoxelVillage
{
    public sealed class VoxelModel32
    {
        public const int Size = 32;

        readonly int[,,] _voxels = new int[Size, Size, Size];

        public void SetVoxel(int x, int y, int z, int colorIndex)
        {
            if (!IsInBounds(x, y, z))
            {
                return;
            }

            _voxels[x, y, z] = colorIndex + 1;
        }

        public void FillBox(int xMin, int yMin, int zMin, int xMaxExclusive, int yMaxExclusive, int zMaxExclusive, int colorIndex)
        {
            var clampedXMin = Mathf.Clamp(xMin, 0, Size);
            var clampedYMin = Mathf.Clamp(yMin, 0, Size);
            var clampedZMin = Mathf.Clamp(zMin, 0, Size);
            var clampedXMax = Mathf.Clamp(xMaxExclusive, 0, Size);
            var clampedYMax = Mathf.Clamp(yMaxExclusive, 0, Size);
            var clampedZMax = Mathf.Clamp(zMaxExclusive, 0, Size);

            for (var x = clampedXMin; x < clampedXMax; x++)
            {
                for (var y = clampedYMin; y < clampedYMax; y++)
                {
                    for (var z = clampedZMin; z < clampedZMax; z++)
                    {
                        _voxels[x, y, z] = colorIndex + 1;
                    }
                }
            }
        }

        public bool TryGetColorIndex(int x, int y, int z, out int colorIndex)
        {
            if (!IsInBounds(x, y, z))
            {
                colorIndex = -1;
                return false;
            }

            var encoded = _voxels[x, y, z];
            if (encoded == 0)
            {
                colorIndex = -1;
                return false;
            }

            colorIndex = encoded - 1;
            return true;
        }

        static bool IsInBounds(int x, int y, int z)
        {
            return x >= 0 && x < Size && y >= 0 && y < Size && z >= 0 && z < Size;
        }
    }

    public static class VoxelMeshBuilder
    {
        static readonly Vector3[,] FaceVertices =
        {
            { new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, 1f, 1f), new Vector3(1f, 0f, 1f) },
            { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 1f), new Vector3(0f, 1f, 0f) },
            { new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 1f), new Vector3(1f, 1f, 1f), new Vector3(1f, 1f, 0f) },
            { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 1f), new Vector3(0f, 0f, 1f) },
            { new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 1f, 1f), new Vector3(0f, 1f, 1f) },
            { new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, 0f, 0f) },
        };

        static readonly Vector3Int[] NeighborOffsets =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
        };

        static readonly Vector3[] FaceNormals =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back,
        };

        static readonly Vector2[] FaceUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
        };

        public static Mesh Build(VoxelModel32 model, float voxelSize, int submeshCount, string meshName)
        {
            var vertices = new List<Vector3>(8192);
            var normals = new List<Vector3>(8192);
            var uvs = new List<Vector2>(8192);
            var triangles = new List<int>[submeshCount];

            for (var index = 0; index < submeshCount; index++)
            {
                triangles[index] = new List<int>(4096);
            }

            for (var x = 0; x < VoxelModel32.Size; x++)
            {
                for (var y = 0; y < VoxelModel32.Size; y++)
                {
                    for (var z = 0; z < VoxelModel32.Size; z++)
                    {
                        if (!model.TryGetColorIndex(x, y, z, out var colorIndex))
                        {
                            continue;
                        }

                        for (var faceIndex = 0; faceIndex < NeighborOffsets.Length; faceIndex++)
                        {
                            var neighbor = NeighborOffsets[faceIndex];
                            if (model.TryGetColorIndex(x + neighbor.x, y + neighbor.y, z + neighbor.z, out _))
                            {
                                continue;
                            }

                            AddFace(
                                vertices,
                                normals,
                                uvs,
                                triangles[colorIndex],
                                new Vector3(x * voxelSize, y * voxelSize, z * voxelSize),
                                voxelSize,
                                faceIndex);
                        }
                    }
                }
            }

            var mesh = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt32,
                subMeshCount = submeshCount
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);

            for (var submesh = 0; submesh < submeshCount; submesh++)
            {
                mesh.SetTriangles(triangles[submesh], submesh, true);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 origin,
            float voxelSize,
            int faceIndex)
        {
            var baseVertex = vertices.Count;
            for (var cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                vertices.Add(origin + FaceVertices[faceIndex, cornerIndex] * voxelSize);
                normals.Add(FaceNormals[faceIndex]);
                uvs.Add(FaceUvs[cornerIndex]);
            }

            triangles.Add(baseVertex);
            triangles.Add(baseVertex + 1);
            triangles.Add(baseVertex + 2);
            triangles.Add(baseVertex);
            triangles.Add(baseVertex + 2);
            triangles.Add(baseVertex + 3);
        }
    }
}
