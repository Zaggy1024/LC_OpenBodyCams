using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenBodyCams.Utilities
{
    internal static class MeshUtils
    {
        private static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
        {
            var meshCopy = new Mesh()
            {
                indexFormat = nonReadableMesh.indexFormat,
            };

            // Handle vertices
            nonReadableMesh.vertexBufferTarget = GraphicsBuffer.Target.Vertex;
            if (nonReadableMesh.vertexBufferCount > 0)
            {
                using var buffer = nonReadableMesh.GetVertexBuffer(0);
                var size = buffer.stride * buffer.count;
                var data = new byte[size];
                buffer.GetData(data);
                meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
                meshCopy.SetVertexBufferData(data, 0, 0, size);
            }

            // Handle triangles
            nonReadableMesh.indexBufferTarget = GraphicsBuffer.Target.Index;
            meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
            using (var buffer = nonReadableMesh.GetIndexBuffer())
            {
                var size = buffer.stride * buffer.count;
                var indicesData = new byte[size];
                buffer.GetData(indicesData);
                meshCopy.SetIndexBufferParams(buffer.count, nonReadableMesh.indexFormat);
                meshCopy.SetIndexBufferData(indicesData, 0, 0, size);
                buffer.Release();
            }

            // Restore submesh structure
            var currentIndexOffset = 0;
            for (var i = 0; i < meshCopy.subMeshCount; i++)
            {
                var subMeshIndexCount = (int)nonReadableMesh.GetIndexCount(i);
                meshCopy.SetSubMesh(i, new SubMeshDescriptor(currentIndexOffset, subMeshIndexCount));
                currentIndexOffset += subMeshIndexCount;
            }

            return meshCopy;
        }

        internal static Mesh CopySubmesh(Mesh mesh, int submesh)
        {
            mesh = MakeReadableMeshCopy(mesh);

            var indexRemap = new Dictionary<int, int>();

            var verts = mesh.vertices;
            var tris = mesh.GetTriangles(submesh);
            var uvs = mesh.uv;

            var submeshVerts = new List<Vector3>(verts.Length);
            var submeshUVs = new List<Vector2>(uvs.Length);
            var submeshTris = new int[tris.Length];

            for (int i = 0; i < tris.Length; i++)
            {
                var index = tris[i];

                if (!indexRemap.TryGetValue(index, out var remappedIndex))
                {
                    indexRemap[index] = remappedIndex = submeshVerts.Count;
                    submeshVerts.Add(verts[index]);
                    submeshUVs.Add(uvs[index]);
                }

                submeshTris[i] = remappedIndex;
            }

            return new Mesh()
            {
                vertices = [.. submeshVerts],
                uv = [.. submeshUVs],
                triangles = submeshTris,
            };
        }
    }
}
