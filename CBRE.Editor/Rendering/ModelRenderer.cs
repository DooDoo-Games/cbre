using CBRE.DataStructures.Geometric;
using CBRE.DataStructures.MapObjects;
using CBRE.DataStructures.Models;
using CBRE.Graphics;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CBRE.Editor.Rendering {
    public static class ModelRenderer {
        public static Dictionary<DataStructures.Models.Model, Dictionary<Common.ITexture, VertexBuffer>> vertexBuffers = new Dictionary<DataStructures.Models.Model, Dictionary<Common.ITexture, VertexBuffer>>();

        public static void Register(DataStructures.Models.Model model) {
            var transforms = model.GetTransforms();
            List<VertexPositionColorTexture> vertexList = new List<VertexPositionColorTexture>();
            vertexBuffers.Add(model, new Dictionary<Common.ITexture, VertexBuffer>());

            foreach (var group in model.GetActiveMeshes().GroupBy(x => x.SkinRef)) {
                var texture = model.Textures[group.Key].TextureObject;
                foreach (var mesh in group) {
                    foreach (var v in mesh.Vertices) {
                        var transform = transforms[v.BoneWeightings.First().Bone.BoneIndex];
                        Vector3 c = new Vector3(v.Location * transform);
                        vertexList.Add(new VertexPositionColorTexture(c.ToXna(), Microsoft.Xna.Framework.Color.White, new Microsoft.Xna.Framework.Vector2(v.TextureU, v.TextureV)));
                    }
                    VertexBuffer buffer = new VertexBuffer(GlobalGraphics.GraphicsDevice, typeof(VertexPositionColorTexture), vertexList.Count, BufferUsage.WriteOnly);
                    buffer.SetData(vertexList.ToArray(), 0, vertexList.Count);
                    // TODO: The TryAdd is incorrect here. I think what happens is that assimp differentiates the textures,
                    // CBRE can't find any of them so it just falls back to a dummy one, which all meshes now share.
                    vertexBuffers[model].TryAdd(texture, buffer);
                    vertexList.Clear();
                }
            }
        }

        private static readonly RasterizerState WireframeRasterizerState = new RasterizerState {
            FillMode = FillMode.WireFrame,
            CullMode = CullMode.None,
        };

        public static void Render(DataStructures.Models.Model model, Matrix mat, BasicEffect effect, bool wireframe = false) {
            var oldWorld = effect.World;
            effect.World = mat.ToXna();

            foreach (var texBuf in vertexBuffers[model]) {
                var texture = texBuf.Key;

                if (texture != null) ((AsyncTexture)texture).Bind();
                effect.Texture = PrimitiveDrawing.Texture;
                effect.TextureEnabled = true;
                effect.VertexColorEnabled = false;
                effect.CurrentTechnique.Passes[0].Apply();
                GlobalGraphics.GraphicsDevice.SetVertexBuffer(texBuf.Value);
                if (wireframe) {
                    // TODO: This is super hacky but who gives a damn!!
                    GlobalGraphics.GraphicsDevice.RasterizerState = WireframeRasterizerState;
                }
                GlobalGraphics.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, texBuf.Value.VertexCount / 3);
                if (texture != null) ((AsyncTexture)texture).Unbind();

                effect.TextureEnabled = false;
                effect.VertexColorEnabled = true;
            }

            effect.World = oldWorld;
        }
    }
}
