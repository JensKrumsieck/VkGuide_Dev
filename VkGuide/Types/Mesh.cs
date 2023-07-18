using System.Numerics;
using System.Runtime.InteropServices.ComTypes;
using Silk.NET.Assimp;

namespace VkGuide.Types;

public class Mesh
{
    public Vertex[] Vertices;
    public AllocatedBuffer VertexBuffer;
    
    public static unsafe Mesh LoadFromObj(string filename)
    {
        using var assimp = Assimp.GetApi();
        var scene = assimp.ImportFile(filename, (uint) PostProcessPreset.TargetRealTimeMaximumQuality);
        var vertices = new List<Vertex>();
        VisitNode(scene->MRootNode);
        assimp.ReleaseImport(scene);

        void VisitNode(Node* node)
        {
            for (var m = 0; m < node->MNumMeshes; m++)
            {
                var mesh = scene->MMeshes[node->MMeshes[m]];
                for (var f = 0; f < mesh->MNumFaces; f++)
                {
                    var face = mesh->MFaces[f];
                    for (var i = 0; i < face.MNumIndices; i++)
                    {
                        var index = face.MIndices[i];
                        var position = mesh->MVertices[index];
                        var normal = mesh->MNormals[index];
                        var tex = mesh->MTextureCoords[0][index];
                        var uv = new Vector2(tex.X, 1 - tex.Y);
                        var vertex = new Vertex
                        {
                            Position = position,
                            Normal = normal,
                            Color = normal,
                            Uv = uv
                        };
                        vertices.Add(vertex);
                    }
                }
            }

            for (var c = 0; c < node->MNumChildren; c++)
                VisitNode(node->MChildren[c]);
        }

        return new Mesh
        {
            Vertices = vertices.ToArray()
        };
    }
}
