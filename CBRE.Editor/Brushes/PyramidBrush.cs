﻿using CBRE.Common;
using CBRE.DataStructures.Geometric;
using CBRE.DataStructures.MapObjects;
using CBRE.Editor.Brushes.Controls;
using System.Collections.Generic;
using System.Linq;

namespace CBRE.Editor.Brushes {
    public class PyramidBrush : IBrush {
        public string Name {
            get { return "Pyramid"; }
        }

        public bool CanRound { get { return true; } }

        public IEnumerable<BrushControl> GetControls() {
            return new List<BrushControl>();
        }

        public IEnumerable<MapObject> Create(IDGenerator generator, Box box, ITexture texture, int roundDecimals) {
            var solid = new Solid(generator.GetNextObjectID()) { Colour = Colour.GetRandomBrushColour() };
            // The lower Z plane will be base
            var c1 = new Vector3(box.Start.X, box.Start.Y, box.Start.Z).Round(roundDecimals);
            var c2 = new Vector3(box.End.X, box.Start.Y, box.Start.Z).Round(roundDecimals);
            var c3 = new Vector3(box.End.X, box.End.Y, box.Start.Z).Round(roundDecimals);
            var c4 = new Vector3(box.Start.X, box.End.Y, box.Start.Z).Round(roundDecimals);
            var c5 = new Vector3(box.Center.X, box.Center.Y, box.End.Z).Round(roundDecimals);
            var faces = new[]
                            {
                                new[] { c1, c2, c3, c4 },
                                new[] { c2, c1, c5 },
                                new[] { c3, c2, c5 },
                                new[] { c4, c3, c5 },
                                new[] { c1, c4, c5 }
                            };
            foreach (var arr in faces) {
                var face = new Face(generator.GetNextFaceID()) {
                    Parent = solid,
                    Plane = new Plane(arr[0], arr[1], arr[2]),
                    Colour = solid.Colour,
                    Texture = { Texture = texture }
                };
                face.Vertices.AddRange(arr.Select(x => new Vertex(x, face)));
                face.UpdateBoundingBox();
                face.AlignTextureToFace();
                solid.Faces.Add(face);
            }
            solid.UpdateBoundingBox();
            yield return solid;
        }
    }
}
