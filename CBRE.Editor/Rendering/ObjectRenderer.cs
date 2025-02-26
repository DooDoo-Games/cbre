﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CBRE.Common;
using CBRE.DataStructures.Geometric;
using CBRE.DataStructures.MapObjects;
using CBRE.Editor.Actions.MapObjects.Operations;
using CBRE.Editor.Compiling.Lightmap;
using CBRE.Editor.Documents;
using CBRE.Extensions;
using CBRE.FileSystem;
using CBRE.Graphics;
using CBRE.Providers.Model;
using CBRE.Providers.Texture;
using CBRE.Settings;
using Microsoft.Xna.Framework.Graphics;

namespace CBRE.Editor.Rendering {
    public class ObjectRenderer {
        public struct EffectStruct {
            public BasicEffect BasicEffect;
            public Effect TexturedLightmapped;
            public Effect TexturedShaded;
            public Effect SolidShaded;
            public Effect Solid;
        }
        public EffectStruct Effects;

        public Document Document;

        public struct PointEntityVertex : IVertexType {
            public Microsoft.Xna.Framework.Vector3 Position;
            public Microsoft.Xna.Framework.Vector3 Normal;
            public Microsoft.Xna.Framework.Color Color;
            public float Selected;
            public static readonly VertexDeclaration VertexDeclaration;
            public PointEntityVertex(
                    Microsoft.Xna.Framework.Vector3 position,
                    Microsoft.Xna.Framework.Vector3 normal,
                    Microsoft.Xna.Framework.Color color
            ) {
                this.Position = position;
                this.Normal = normal;
                this.Color = color;
                this.Selected = 0.0f;
            }

            VertexDeclaration IVertexType.VertexDeclaration {
                get {
                    return VertexDeclaration;
                }
            }

            static PointEntityVertex() {
                VertexElement[] elements = {
                    new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                    new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                    new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0),
                    new VertexElement(28, VertexElementFormat.Single, VertexElementUsage.Color, 1)
                };
                VertexDeclaration declaration = new VertexDeclaration(elements);
                VertexDeclaration = declaration;
            }
        };

        private class PointEntityGeometry {
            public PointEntityGeometry(Document doc) { document = doc; }

            private readonly Document document;
            private PointEntityVertex[] vertices = null;
            private ushort[] indicesSolid = null;
            private ushort[] indicesWireframe = null;
            private VertexBuffer vertexBuffer = null;
            private IndexBuffer indexBufferSolid = null;
            private IndexBuffer indexBufferWireframe = null;
            private int vertexCount = 0;
            private int indexSolidCount = 0;
            private int indexWireframeCount = 0;

            public void UpdateBuffers() {
                var entities = document.Map.WorldSpawn
                    .Find(x => x is Entity { GameData.ClassType: DataStructures.GameData.ClassType.Point } e
                               && e.GameData.Behaviours.All(p => p.Name != "sprite"))
                    .OfType<Entity>().ToList();
                vertexCount = entities.Count * 24;
                indexSolidCount = entities.Count * 36;
                indexWireframeCount = entities.Count * 24;

                if (entities.Count == 0) {
                    return;
                }

                if (vertexBuffer == null || vertices == null || vertices.Length < vertexCount) {
                    vertexBuffer = new VertexBuffer(GlobalGraphics.GraphicsDevice, PointEntityVertex.VertexDeclaration, vertexCount, BufferUsage.None);
                }

                if (indexBufferSolid == null || indicesSolid == null || indicesSolid.Length < indexSolidCount) {
                    indexBufferSolid = new IndexBuffer(GlobalGraphics.GraphicsDevice, IndexElementSize.SixteenBits, indexSolidCount, BufferUsage.None);
                }

                if (indexBufferWireframe == null || indicesWireframe == null || indicesWireframe.Length < indexWireframeCount) {
                    indexBufferWireframe = new IndexBuffer(GlobalGraphics.GraphicsDevice, IndexElementSize.SixteenBits, indexWireframeCount, BufferUsage.None);
                }

                vertices = new PointEntityVertex[vertexCount];
                indicesSolid = new ushort[indexSolidCount];
                indicesWireframe = new ushort[indexWireframeCount];

                void writeVertices(int i, Entity entity) {
                    int j = 0;
                    foreach (var face in entity.BoundingBox.GetBoxFaces()) {
                        var normal = (face[1] - face[0]).Cross(face[2] - face[0]).Normalise();
                        foreach (var point in face) {
                            vertices[(i * 24) + j].Position = new Microsoft.Xna.Framework.Vector3((float)point.X, (float)point.Y, (float)point.Z);
                            vertices[(i * 24) + j].Normal = new Microsoft.Xna.Framework.Vector3((float)normal.X, (float)normal.Y, (float)normal.Z);
                            vertices[(i * 24) + j].Color = new Microsoft.Xna.Framework.Color(entity.Colour.R, entity.Colour.G, entity.Colour.B, entity.Colour.A);
                            vertices[(i * 24) + j].Selected = entity.IsSelected ? 1.0f : 0.0f;
                            j++;
                        }
                    }
                }

                void writeIndicesSolid(int i) {
                    for (int j=0;j<6;j++) {
                        indicesSolid[(i * 36) + (j * 6) + 0] = (ushort)((i * 24) + (j * 4) + 0);
                        indicesSolid[(i * 36) + (j * 6) + 1] = (ushort)((i * 24) + (j * 4) + 1);
                        indicesSolid[(i * 36) + (j * 6) + 2] = (ushort)((i * 24) + (j * 4) + 2);
                        indicesSolid[(i * 36) + (j * 6) + 3] = (ushort)((i * 24) + (j * 4) + 0);
                        indicesSolid[(i * 36) + (j * 6) + 4] = (ushort)((i * 24) + (j * 4) + 2);
                        indicesSolid[(i * 36) + (j * 6) + 5] = (ushort)((i * 24) + (j * 4) + 3);
                    }
                }
                void writeIndicesWireframe(int i) {
                    //front
                    indicesWireframe[(i * 24) + 0] = (ushort)((i * 24) + 0);
                    indicesWireframe[(i * 24) + 1] = (ushort)((i * 24) + 1);
                    indicesWireframe[(i * 24) + 2] = (ushort)((i * 24) + 1);
                    indicesWireframe[(i * 24) + 3] = (ushort)((i * 24) + 2);
                    indicesWireframe[(i * 24) + 4] = (ushort)((i * 24) + 2);
                    indicesWireframe[(i * 24) + 5] = (ushort)((i * 24) + 3);
                    indicesWireframe[(i * 24) + 6] = (ushort)((i * 24) + 3);
                    indicesWireframe[(i * 24) + 7] = (ushort)((i * 24) + 0);

                    //back
                    indicesWireframe[(i * 24) + 8] = (ushort)((i * 24) + 4);
                    indicesWireframe[(i * 24) + 9] = (ushort)((i * 24) + 5);
                    indicesWireframe[(i * 24) + 10] = (ushort)((i * 24) + 5);
                    indicesWireframe[(i * 24) + 11] = (ushort)((i * 24) + 6);
                    indicesWireframe[(i * 24) + 12] = (ushort)((i * 24) + 6);
                    indicesWireframe[(i * 24) + 13] = (ushort)((i * 24) + 7);
                    indicesWireframe[(i * 24) + 14] = (ushort)((i * 24) + 7);
                    indicesWireframe[(i * 24) + 15] = (ushort)((i * 24) + 4);

                    //front to back
                    indicesWireframe[(i * 24) + 16] = (ushort)((i * 24) + 0);
                    indicesWireframe[(i * 24) + 17] = (ushort)((i * 24) + 5);
                    indicesWireframe[(i * 24) + 18] = (ushort)((i * 24) + 1);
                    indicesWireframe[(i * 24) + 19] = (ushort)((i * 24) + 4);
                    indicesWireframe[(i * 24) + 20] = (ushort)((i * 24) + 2);
                    indicesWireframe[(i * 24) + 21] = (ushort)((i * 24) + 7);
                    indicesWireframe[(i * 24) + 22] = (ushort)((i * 24) + 3);
                    indicesWireframe[(i * 24) + 23] = (ushort)((i * 24) + 6);
                }

                for (int i=0;i<entities.Count;i++) {
                    var entity = entities[i];
                    int j = 0;
                    writeVertices(i, entity);
                    writeIndicesSolid(i);
                    writeIndicesWireframe(i);
                }

                vertexBuffer.SetData(vertices);
                indexBufferSolid.SetData(indicesSolid);
                indexBufferWireframe.SetData(indicesWireframe);
            }

            public void RenderWireframe() {
                UpdateBuffers();
                if (indexWireframeCount == 0) {
                    return;
                }
                GlobalGraphics.GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GlobalGraphics.GraphicsDevice.Indices = indexBufferWireframe;
                GlobalGraphics.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, indexWireframeCount / 2);
            }

            public void RenderSolid() {
                UpdateBuffers();
                if (indexSolidCount == 0) {
                    return;
                }
                GlobalGraphics.GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GlobalGraphics.GraphicsDevice.Indices = indexBufferSolid;
                GlobalGraphics.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, indexSolidCount / 3);
            }
        };
        private readonly PointEntityGeometry pointEntityGeometry;

        public struct BrushVertex : IVertexType {
            public Microsoft.Xna.Framework.Vector3 Position;
            public Microsoft.Xna.Framework.Vector3 Normal;
            public Microsoft.Xna.Framework.Vector2 DiffuseUV;
            public Microsoft.Xna.Framework.Vector2 LightmapUV;
            public Microsoft.Xna.Framework.Color Color;
            public float Selected;
            public static readonly VertexDeclaration VertexDeclaration;
            public BrushVertex(
                    Microsoft.Xna.Framework.Vector3 position,
                    Microsoft.Xna.Framework.Vector3 normal,
                    Microsoft.Xna.Framework.Vector2 diffUv,
                    Microsoft.Xna.Framework.Vector2 lmUv,
                    Microsoft.Xna.Framework.Color color,
                    bool selected
            ) {
                this.Position = position;
                this.Normal = normal;
                this.DiffuseUV = diffUv;
                this.LightmapUV = lmUv;
                this.Color = color;
                this.Selected = selected ? 1.0f : 0.0f;
            }

            public BrushVertex(Vertex vertex) {
                Microsoft.Xna.Framework.Vector3 toMgVec3(Vector3 v)
                    => new((float)v.X, (float)v.Y, (float)v.Z);

                Microsoft.Xna.Framework.Color toMgColor(System.Drawing.Color c)
                    => new(c.R, c.G, c.B, c.A);
                
                this.Position = toMgVec3(vertex.Location);
                this.Normal = toMgVec3(vertex.Parent.Plane.Normal);
                this.DiffuseUV = new((float)vertex.TextureU, (float)vertex.TextureV);
                this.LightmapUV = new(vertex.LMU, vertex.LMV);
                this.Color = toMgColor(vertex.Parent.Colour);
                this.Selected = 0.0f;
            }

            VertexDeclaration IVertexType.VertexDeclaration {
                get {
                    return VertexDeclaration;
                }
            }

            static BrushVertex() {
                VertexElement[] elements = {
                    new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                    new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                    new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                    new VertexElement(32, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
                    new VertexElement(40, VertexElementFormat.Color, VertexElementUsage.Color, 0),
                    new VertexElement(44, VertexElementFormat.Single, VertexElementUsage.Color, 1)
                };
                VertexDeclaration declaration = new VertexDeclaration(elements);
                VertexDeclaration = declaration;
            }
        };

        private class BrushGeometry : IDisposable {
            public BrushGeometry(Document doc) { document = doc; }

            private readonly Document document;

            private class Mesh : IDisposable {
                public abstract class BufferTracker<BufferType, ElemType> : IDisposable
                    where BufferType : GraphicsResource
                    where ElemType : struct {

                    protected ElemType[] elements { get; private set; } = Array.Empty<ElemType>();
                    public BufferType Buffer { get; private set; } = null;

                    public int Count { get; private set; } = 0;

                    public bool Empty => Count <= 0;
                    
                    public void ResizeAndReset(int newSize) {
                        Count = 0;
                        if (Buffer is null
                            || elements.Length < newSize
                            || elements.Length > newSize * 2
                           ) {
                            elements = new ElemType[(int)(newSize * 1.5f)];
                            Buffer?.Dispose();
                            Buffer = CreateResource();
                        }
                    }

                    public void Add(ElemType newElem) {
                        elements[Count] = newElem;
                        Count++;
                    }

                    public abstract void Submit();
                    
                    protected abstract BufferType CreateResource();

                    public void Dispose() {
                        Buffer?.Dispose();
                    }
                }
                
                public class VertexCollection : BufferTracker<VertexBuffer, BrushVertex> {
                    public override void Submit()
                        => Buffer.SetData(elements, 0, Count);

                    protected override VertexBuffer CreateResource()
                        => new(
                            GlobalGraphics.GraphicsDevice,
                            BrushVertex.VertexDeclaration,
                            elements.Length,
                            BufferUsage.None);
                }

                public class IndexCollection : BufferTracker<IndexBuffer, ushort> {
                    public override void Submit()
                        => Buffer.SetData(elements, 0, Count);
                    
                    protected override IndexBuffer CreateResource()
                        => new(
                            GlobalGraphics.GraphicsDevice,
                            IndexElementSize.SixteenBits,
                            elements.Length,
                            BufferUsage.None);
                }

                public readonly VertexCollection Vertices = new();
                public readonly IndexCollection SolidIndices = new();
                public readonly IndexCollection WireframeIndices = new();

                public bool Empty => Vertices.Empty || SolidIndices.Empty || WireframeIndices.Empty;

                public void RenderWireframe() {
                    if (WireframeIndices.Count <= 0) { return; }
                    GlobalGraphics.GraphicsDevice.SetVertexBuffer(Vertices.Buffer);
                    GlobalGraphics.GraphicsDevice.Indices = WireframeIndices.Buffer;
                    GlobalGraphics.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, WireframeIndices.Count / 2);
                }
                
                public void RenderSolid() {
                    if (SolidIndices.Count <= 0) { return; }
                    GlobalGraphics.GraphicsDevice.SetVertexBuffer(Vertices.Buffer);
                    GlobalGraphics.GraphicsDevice.Indices = SolidIndices.Buffer;
                    GlobalGraphics.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, SolidIndices.Count / 3);
                }
                
                public void Submit() {
                    Vertices.Submit();
                    SolidIndices.Submit();
                    WireframeIndices.Submit();
                }
                
                public void Dispose() {
                    Vertices?.Dispose();
                    SolidIndices?.Dispose();
                    WireframeIndices?.Dispose();
                }
            }

            private readonly Dictionary<int, Mesh> lmMeshes = new();

            private readonly List<Face> faces = new();

            private bool dirty = false;

            private void UpdateBuffers() {
                if (!dirty) { return; }

                Dictionary<int, int> vertexCounts = new();
                Dictionary<int, int> indexSolidCounts = new();
                Dictionary<int, int> indexWireframeCounts = new();

                void addCount(Dictionary<int, int> counts, int lmIndex, int valueToAdd)
                    => counts[lmIndex] = (counts.TryGetValue(lmIndex, out var prevValue)
                        ? prevValue
                        : 0) + valueToAdd;
                for (int i=0;i<faces.Count;i++) {
                    faces[i].CalculateTextureCoordinates(minimizeShiftValues: true);
                    addCount(vertexCounts, faces[i].LmIndex, faces[i].Vertices.Count);
                    addCount(indexSolidCounts, faces[i].LmIndex, (faces[i].Vertices.Count - 2) * 3);
                    addCount(indexWireframeCounts, faces[i].LmIndex, faces[i].Vertices.Count * 2);
                }

                Mesh getMeshForLmIndex(int lmIndex) {
                    if (!lmMeshes.ContainsKey(lmIndex)) {
                        lmMeshes[lmIndex] = new Mesh();
                    }
                    return lmMeshes[lmIndex];
                }
                
                foreach (var lmIndex in vertexCounts.Keys) {
                    var mesh = getMeshForLmIndex(lmIndex);
                    mesh.Vertices.ResizeAndReset(vertexCounts[lmIndex]);
                    mesh.SolidIndices.ResizeAndReset(indexSolidCounts[lmIndex]);
                    mesh.WireframeIndices.ResizeAndReset(indexWireframeCounts[lmIndex]);
                }

                foreach (var lmIndex in lmMeshes.Keys.ToArray()) {
                    if (vertexCounts.ContainsKey(lmIndex)) { continue; }

                    lmMeshes[lmIndex].Dispose();
                    lmMeshes.Remove(lmIndex);
                }

                foreach (var face in faces) {
                    var mesh = getMeshForLmIndex(face.LmIndex);
                    int vertexIndex = mesh.Vertices.Count;
                    
                    for (int vertIndex=0;vertIndex<face.Vertices.Count;vertIndex++) {
                        var newVertex = new BrushVertex(face.Vertices[vertIndex]) {
                            Selected = document.Selection.IsFaceSelected(face) ? 1.0f : 0.0f
                        };
                        mesh.Vertices.Add(newVertex);
                        mesh.WireframeIndices.Add((ushort)(vertexIndex + vertIndex));
                        mesh.WireframeIndices.Add((ushort)(vertexIndex + ((vertIndex + 1) % face.Vertices.Count)));
                    }

                    foreach (var triangleIndex in face.GetTriangleIndices()) {
                        mesh.SolidIndices.Add((ushort)(vertexIndex + triangleIndex));
                    }
                }

                foreach (var lmIndex in vertexCounts.Keys) {
                    var mesh = getMeshForLmIndex(lmIndex);
                    mesh.Submit();
                }

                dirty = false;
            }

            public void AddFace(Face face) {
                if (!faces.Contains(face)) { faces.Add(face); }
                MarkDirty();
            }

            public void RemoveFace(Face face) {
                if (!faces.Contains(face)) { return; }

                faces.Remove(face);
                MarkDirty();
            }

            public void MarkDirty() {
                dirty = true;
            }

            public bool HasFaces {
                get { return faces.Count > 0; }
            }

            public void RenderWireframe() {
                UpdateBuffers();
                foreach (var mesh in lmMeshes.Values) {
                    mesh.RenderWireframe();
                }
            }

            public void RenderSolid(int lmIndex = -1) {
                UpdateBuffers();
                if (lmIndex >= 0) {
                    if (lmMeshes.ContainsKey(lmIndex)) {
                        lmMeshes[lmIndex].RenderSolid();
                    }
                    return;
                }
                foreach (var mesh in lmMeshes.Values) {
                    mesh.RenderSolid();
                }
            }

            public void Dispose() {
                foreach (var mesh in lmMeshes.Values) {
                    mesh.Dispose();
                }
                lmMeshes.Clear();
            }
        }


        public void MarkDirty() {
            foreach (var kvp in brushGeom) {
                kvp.Value.MarkDirty();
            }
        }

        public void MarkDirty(string texName) {
            if (brushGeom.TryGetValue(texName.ToLowerInvariant(), out BrushGeometry geom)) {
                geom.MarkDirty();
            }
        }

        public void AddMapObject(MapObject mapObject) {
            switch (mapObject)
            {
                case Solid s:
                    foreach (var f in s.Faces) { AddFace(f); }
                    break;
                case Group g:
                    foreach (var child in g.GetChildren()) { AddMapObject(child); }
                    break;
            }
        }
        
        public void RemoveMapObject(MapObject mapObject) {
            switch (mapObject)
            {
                case Solid s:
                    foreach (var f in s.Faces) { RemoveFace(f); }
                    break;
                case Group g:
                    foreach (var child in g.GetChildren()) { RemoveMapObject(child); }
                    break;
            }
        }
        
        public void SetSolidHidden(Solid solid, bool hide) {
            if (hide) {
                HideSolid(solid);
            } else {
                ShowSolid(solid);
            }
        }

        public void HideSolid(Solid solid) {
            solid.IsCodeHidden = true;
            solid.Faces.ForEach(RemoveFace);
        }

        public void ShowSolid(Solid solid) {
            solid.IsCodeHidden = false;
            solid.Faces.ForEach(AddFace);
        }

        public void MarkMapObjectDirty(MapObject mapObject) {
            switch (mapObject)
            {
                case Solid s:
                    foreach (var t in s.Faces.Select(f => f.Texture.Name).Distinct()) { MarkDirty(t); }
                    break;
                case Group g:
                    foreach (var child in g.GetChildren()) { MarkMapObjectDirty(child); }
                    break;
            }
        }

        private Dictionary<string, BrushGeometry> brushGeom = new Dictionary<string, BrushGeometry>();

        public ObjectRenderer(Document doc) {
            Document = doc;

            Effects.BasicEffect = new BasicEffect(GlobalGraphics.GraphicsDevice);
            Effects.TexturedLightmapped = GlobalGraphics.LoadEffect("Shaders/texturedLightmapped.mgfx");
            Effects.TexturedShaded = GlobalGraphics.LoadEffect("Shaders/texturedShaded.mgfx");
            Effects.SolidShaded = GlobalGraphics.LoadEffect("Shaders/solidShaded.mgfx");
            Effects.Solid = GlobalGraphics.LoadEffect("Shaders/solid.mgfx");

            foreach (Solid solid in doc.Map.WorldSpawn.Find(x => x is Solid).OfType<Solid>()) {
                solid.Faces.ForEach(AddFace);
            }
            pointEntityGeometry = new PointEntityGeometry(doc);
        }

        public void AddFace(Face face) {
            var textureName = face.Texture.Name.ToLowerInvariant();
            if (!brushGeom.ContainsKey(textureName)) {
                brushGeom.Add(textureName, new BrushGeometry(Document));
            }
            brushGeom[textureName].AddFace(face);
        }

        public void RemoveFace(Face face) {
            var textureName = face.Texture.Name.ToLowerInvariant();
            if (!brushGeom.ContainsKey(textureName)) {
                return;
            }
            brushGeom[textureName].RemoveFace(face);
            if (!brushGeom[textureName].HasFaces) {
                brushGeom[textureName].Dispose();
                brushGeom.Remove(textureName);
            }
        }

        public Microsoft.Xna.Framework.Matrix World {
            get { return Effects.BasicEffect.World; }
            set {
                Effects.BasicEffect.World = value;
                Effects.TexturedLightmapped.Parameters["World"].SetValue(value);
                Effects.TexturedShaded.Parameters["World"].SetValue(value);
                Effects.SolidShaded.Parameters["World"].SetValue(value);
                Effects.Solid.Parameters["World"].SetValue(value);
            }
        }

        public Microsoft.Xna.Framework.Matrix View {
            get { return Effects.BasicEffect.View; }
            set {
                Effects.BasicEffect.View = value;
                Effects.TexturedLightmapped.Parameters["View"].SetValue(value);
                Effects.TexturedShaded.Parameters["View"].SetValue(value);
                Effects.SolidShaded.Parameters["View"].SetValue(value);
                Effects.Solid.Parameters["View"].SetValue(value);
            }
        }

        public Microsoft.Xna.Framework.Matrix Projection {
            get { return Effects.BasicEffect.Projection; }
            set {
                Effects.BasicEffect.Projection = value;
                Effects.TexturedLightmapped.Parameters["Projection"].SetValue(value);
                Effects.TexturedShaded.Parameters["Projection"].SetValue(value);
                Effects.SolidShaded.Parameters["Projection"].SetValue(value);
                Effects.Solid.Parameters["Projection"].SetValue(value);
            }
        }

        private readonly List<(AsyncTexture Texture, BrushGeometry Geometry)> translucentGeom = new();

        private Dictionary<string, ModelReference> models = new Dictionary<string, ModelReference>();

        public void RenderTextured() {
            translucentGeom.Clear();
            foreach (var kvp in brushGeom) {
                TextureItem item = TextureProvider.GetItem(kvp.Key);
                if (item is {Texture: AsyncTexture {MonoGameTexture: { }} asyncTexture}) {
                    if (asyncTexture.HasTransparency()) {
                        translucentGeom.Add((asyncTexture, kvp.Value));
                        continue;
                    } else {
                        Effects.TexturedShaded.Parameters["xTexture"].SetValue(asyncTexture.MonoGameTexture);
                        Effects.TexturedShaded.CurrentTechnique.Passes[0].Apply();
                    }
                } else {
                    Effects.SolidShaded.CurrentTechnique.Passes[0].Apply();
                }
                kvp.Value.RenderSolid();
            }
            
            Effects.SolidShaded.CurrentTechnique.Passes[0].Apply();
            pointEntityGeometry.RenderSolid();

            var prevDepthStencilState = GlobalGraphics.GraphicsDevice.DepthStencilState;
            GlobalGraphics.GraphicsDevice.DepthStencilState = new DepthStencilState() { DepthBufferEnable = true, DepthBufferWriteEnable = false };
            foreach (var (texture, geometry) in translucentGeom) {
                Effects.TexturedShaded.Parameters["xTexture"].SetValue(texture.MonoGameTexture);
                Effects.TexturedShaded.CurrentTechnique.Passes[0].Apply();
                geometry.RenderSolid();
            }
            GlobalGraphics.GraphicsDevice.DepthStencilState = prevDepthStencilState;
        }

        public void RenderSprites(Viewport3D vp) {
            Effects.BasicEffect.CurrentTechnique.Passes[0].Apply();
            var sprites = Document.Map.WorldSpawn.Find(x => x is Entity { GameData: { } } e && e.GameData.Behaviours.Any(p => p.Name == "sprite")).OfType<Entity>().ToList();
            foreach (var sprite in sprites) {
                string key = sprite.GameData.Behaviours.FirstOrDefault(p => p.Name == "sprite")?.Values.FirstOrDefault();
                string color = sprite.GameData.Behaviours.FirstOrDefault(p => p.Name == "spritecolor")?.Values.FirstOrDefault();
                Property prop = sprite.EntityData.Properties.FirstOrDefault(p => p.Key == color);
                if (string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                TextureItem tex = TextureProvider.GetItem(key);
                if (tex is { Texture: AsyncTexture t }) {
                    PrimitiveDrawing.Begin(PrimitiveType.QuadList);
                    var c = sprite.Origin;
                    var fcolor = prop?.GetVector3(Vector3.One * 255f) ?? (Vector3.One * 255f);
                    t.Bind();
                    double amount = 25.0;
                    var up = vp.Camera.GetUp().Normalise() * amount;
                    var right = vp.Camera.GetRight().Normalise() * amount;
                    PrimitiveDrawing.Vertex3(c + up - right, 0f, 0f);
                    PrimitiveDrawing.Vertex3(c + up + right, 1f, 0f);
                    PrimitiveDrawing.Vertex3(c - up + right, 1f, 1f);
                    PrimitiveDrawing.Vertex3(c - up - right, 0f, 1f);
                    Effects.BasicEffect.Texture = PrimitiveDrawing.Texture;
                    Effects.BasicEffect.DiffuseColor = fcolor.ToXna() / 255f;
                    Effects.BasicEffect.TextureEnabled = true;
                    Effects.BasicEffect.VertexColorEnabled = false;
                    Effects.BasicEffect.CurrentTechnique.Passes[0].Apply();
                    PrimitiveDrawing.End();
                    t.Unbind();
                }
            }
            Effects.BasicEffect.TextureEnabled = false;
            Effects.BasicEffect.VertexColorEnabled = true;
        }

        public void RenderModels() {
            // Models
            Effects.BasicEffect.CurrentTechnique.Passes[0].Apply();
            var models = Document.Map.WorldSpawn
                .Find(x => x is Entity e && e.GameData != null && e.GameData.Behaviours.Any(p => p.Name == "model"))
                .OfType<Entity>().ToList();
            foreach (var model in models) {
                string key = model.GameData.Behaviours.FirstOrDefault(p => p.Name == "model").Values.FirstOrDefault();
                string path = Directories.GetModelPath(model.EntityData.GetPropertyValue(key));
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                NativeFile file = new NativeFile(path);
                if (this.models.ContainsKey(path)) {
                    Vector3 euler = model.EntityData.GetPropertyVector3("angles", Vector3.Zero);
                    Vector3 scale = model.EntityData.GetPropertyVector3("scale", Vector3.One);
                    Matrix modelMat = Matrix.Translation(model.Origin)
                                      * Matrix.RotationX(DMath.DegreesToRadians(euler.X))
                                      * Matrix.RotationY(DMath.DegreesToRadians(euler.Z))
                                      * Matrix.RotationZ(DMath.DegreesToRadians(euler.Y))
                                      * Matrix.Scale(scale);
                    ModelRenderer.Render(this.models[path].Model, modelMat, Effects.BasicEffect);
                } else if (ModelProvider.CanLoad(file)) {
                    ModelReference mref = ModelProvider.CreateModelReference(file);
                    this.models.Add(path, mref);
                    ModelRenderer.Register(this.models[path].Model);
                }
            }
        }

        public void RenderLightmapped() {
            if (Document.MGLightmaps is not {Count: > 0}) {
                RenderTextured();
                return;
            }

            foreach (var kvp in brushGeom) {
                for (int i = 0; i < Document.MGLightmaps.Count; i++) {
                    TextureItem item = TextureProvider.GetItem(kvp.Key);
                    
                    Effects.TexturedLightmapped.Parameters["lmTexture"].SetValue(Document.MGLightmaps[i]);

                    if (item is {Texture: AsyncTexture {MonoGameTexture: { }} asyncTexture}) {
                        Effects.TexturedLightmapped.Parameters["diffTexture"].SetValue(asyncTexture.MonoGameTexture);
                    }
                    Effects.TexturedLightmapped.CurrentTechnique.Passes[0].Apply();
                    kvp.Value.RenderSolid(i);
                }
            }
            Effects.SolidShaded.CurrentTechnique.Passes[0].Apply();
            pointEntityGeometry.RenderSolid();

        }

        public void RenderSolidUntextured() {
            foreach (var kvp in brushGeom) {
                Effects.SolidShaded.CurrentTechnique.Passes[0].Apply();
                kvp.Value.RenderSolid();
            }
            Effects.SolidShaded.CurrentTechnique.Passes[0].Apply();
            pointEntityGeometry.RenderSolid();
        }

        public void RenderFlatUntextured() {
            foreach (var kvp in brushGeom) {
                Effects.Solid.CurrentTechnique.Passes[0].Apply();
                kvp.Value.RenderSolid();
            }
            Effects.SolidShaded.CurrentTechnique.Passes[0].Apply();
            pointEntityGeometry.RenderSolid();
        }

        public void RenderWireframe() {
            Effects.Solid.CurrentTechnique.Passes[0].Apply();
            foreach (var kvp in brushGeom) {
                kvp.Value.RenderWireframe();
            }
            pointEntityGeometry.RenderWireframe();
        }
    }
}
