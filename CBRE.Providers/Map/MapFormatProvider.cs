﻿using CBRE.Common;
using CBRE.DataStructures.Geometric;
using CBRE.DataStructures.MapObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using static CBRE.Common.PrimitiveConversion;

namespace CBRE.Providers.Map {
    public class MapFormatProvider : MapProvider {
        protected override IEnumerable<MapFeature> GetFormatFeatures() {
            return new[]
            {
                MapFeature.Worldspawn,
                MapFeature.Solids,
                MapFeature.Entities
            };
        }

        protected override bool IsValidForFileName(string filename) {
            return filename.EndsWith(".map", StringComparison.OrdinalIgnoreCase)
                || filename.EndsWith(".max", StringComparison.OrdinalIgnoreCase);
        }

        private string CleanLine(string line) {
            if (line == null) return null;
            var ret = line;
            if (ret.Contains("//")) ret = ret.Substring(0, ret.IndexOf("//", StringComparison.Ordinal)); // Comments
            return ret.Trim();
        }

        private void Assert(bool b, string message = "Malformed file.") {
            if (!b) throw new Exception(message);
        }

        private string FormatVector3(Vector3 c) {
            return c.X.ToString("0.000")
                   + " " + c.Y.ToString("0.000")
                   + " " + c.Z.ToString("0.000");
        }

        private void CollectSolids(List<Solid> solids, MapObject parent) {
            solids.AddRange(parent.GetChildren().OfType<Solid>());
            parent.GetChildren().OfType<Group>().ToList().ForEach(x => CollectSolids(solids, x));
        }

        private void CollectEntities(List<Entity> entities, MapObject parent) {
            entities.AddRange(parent.GetChildren().OfType<Entity>());
            parent.GetChildren().OfType<Group>().ToList().ForEach(x => CollectEntities(entities, x));
        }

        private Face ReadFace(string line, IDGenerator generator) {
            var parts = line.Split(' ').Where(x => !String.IsNullOrWhiteSpace(x)).ToList();


            Assert(parts[0] == "(");
            Assert(parts[4] == ")");
            Assert(parts[5] == "(");
            Assert(parts[9] == ")");
            Assert(parts[10] == "(");
            Assert(parts[14] == ")");

            var face = new Face(generator.GetNextFaceID()) {
                Plane = new Plane(Vector3.Parse(parts[1], parts[2], parts[3]),
                    Vector3.Parse(parts[6], parts[7], parts[8]),
                    Vector3.Parse(parts[11], parts[12], parts[13])),
                Texture = { Name = parts[15] }
            };

            // Cater for older-style map formats
            if (parts.Count == 21) {
                face.AlignTextureToFace();
                face.Texture.XShift = ParseDecimal(parts[16]);
                face.Texture.YShift = ParseDecimal(parts[17]);
                face.Texture.Rotation = ParseDecimal(parts[18]);
                face.Texture.XScale = ParseDecimal(parts[19]);
                face.Texture.YScale = ParseDecimal(parts[20]);
            } else {
                Assert(parts[16] == "[");
                Assert(parts[21] == "]");
                Assert(parts[22] == "[");
                Assert(parts[27] == "]");

                face.Texture.UAxis = Vector3.Parse(parts[17], parts[18], parts[19]);
                face.Texture.XShift = ParseDecimal(parts[20]);
                face.Texture.VAxis = Vector3.Parse(parts[23], parts[24], parts[25]);
                face.Texture.YShift = ParseDecimal(parts[26]);
                face.Texture.Rotation = ParseDecimal(parts[28]);
                face.Texture.XScale = ParseDecimal(parts[29]);
                face.Texture.YScale = ParseDecimal(parts[30]);
            }

            return face;
        }

        private void WriteFace(StreamWriter sw, Face face) {
            // ( -128 64 64 ) ( -64 64 64 ) ( -64 0 64 ) AAATRIGGER [ 1 0 0 0 ] [ 0 -1 0 0 ] 0 1 1
            var strings = face.Vertices.Take(3).Select(x => "( " + FormatVector3(x.Location) + " )").ToList();
            strings.Add(String.IsNullOrWhiteSpace(face.Texture.Name) ? "AAATRIGGER" : face.Texture.Name);
            strings.Add("[");
            strings.Add(FormatVector3(face.Texture.UAxis));
            strings.Add(face.Texture.XShift.ToString("0.000"));
            strings.Add("]");
            strings.Add("[");
            strings.Add(FormatVector3(face.Texture.VAxis));
            strings.Add(face.Texture.YShift.ToString("0.000"));
            strings.Add("]");
            strings.Add(face.Texture.Rotation.ToString("0.000"));
            strings.Add(face.Texture.XScale.ToString("0.000"));
            strings.Add(face.Texture.YScale.ToString("0.000"));
            sw.WriteLine(String.Join(" ", strings));
        }

        private Solid ReadSolid(StreamReader rdr, IDGenerator generator) {
            var faces = new List<Face>();
            string line;
            while ((line = CleanLine(rdr.ReadLine())) != null) {
                if (String.IsNullOrWhiteSpace(line)) continue;
                if (line == "}") {
                    var ret = Solid.CreateFromIntersectingPlanes(faces.Select(x => x.Plane), generator);
                    ret.Colour = Colour.GetRandomBrushColour();
                    foreach (var face in ret.Faces) {
                        var f = faces.FirstOrDefault(x => x.Plane.Normal.EquivalentTo(face.Plane.Normal));
                        if (f == null) {
                            // TODO: Report invalid solids
                            Debug.WriteLine("Invalid solid!");
                            return null;
                        }
                        face.Texture = f.Texture;
                        face.Parent = ret;
                        face.Colour = ret.Colour;
                    }
                    ret.UpdateBoundingBox(false);
                    return ret;
                }
                faces.Add(ReadFace(line, generator));
            }
            return null;
        }

        private void WriteSolid(StreamWriter sw, Solid solid) {
            sw.WriteLine("{");
            solid.Faces.ForEach(x => WriteFace(sw, x));
            sw.WriteLine("}");
        }

        private static readonly string[] ExcludedKeys = new[] { "spawnflags", "classname", "origin", "wad", "mapversion" };

        private static void ReadProperty(Entity ent, string line) {
            var split = line.Split(' ');
            var key = split[0].Trim('"');

            var val = String.Join(" ", split.Skip(1)).Trim('"');

            if (key == "classname") {
                ent.EntityData.Name = val;
            } else if (key == "spawnflags") {
                ent.EntityData.Flags = int.Parse(val);
            } else if (key == "origin") {
                var osp = val.Split(' ');
                ent.Origin = Vector3.Parse(osp[0], osp[1], osp[2]);
            } else if (!ExcludedKeys.Contains(key.ToLower())) {
                ent.EntityData.SetPropertyValue(key, val);
            }
        }

        private void WriteProperty(StreamWriter sw, string key, string value) {
            sw.WriteLine('"' + key + "\" \"" + value + '"');
        }

        private Entity ReadEntity(StreamReader rdr, IDGenerator generator) {
            var ent = new Entity(generator.GetNextObjectID()) { EntityData = new EntityData(), Colour = Colour.GetRandomBrushColour() };
            string line;
            while ((line = CleanLine(rdr.ReadLine())) != null) {
                if (String.IsNullOrWhiteSpace(line)) continue;
                if (line[0] == '"') ReadProperty(ent, line);
                else if (line[0] == '{') {
                    var s = ReadSolid(rdr, generator);
                    if (s != null) s.SetParent(ent, false);
                } else if (line[0] == '}') break;
            }
            ent.UpdateBoundingBox(false);
            return ent;
        }

        private void WriteEntity(StreamWriter sw, Entity ent) {
            var solids = new List<Solid>();
            CollectSolids(solids, ent);

            sw.WriteLine("{");
            WriteProperty(sw, "classname", ent.EntityData.Name);

            if (ent.EntityData.Flags > 0) {
                // VHE doesn't write the spawnflags when they are zero
                WriteProperty(sw, "spawnflags", ent.EntityData.Flags.ToString());
            }
            foreach (var prop in ent.EntityData.Properties) {
                if (prop.Key == "classname" || prop.Key == "spawnflags" || prop.Key == "origin") continue;

                // VHE doesn't write empty or zero values to the .map file
                var gameDataProp = ent.GameData != null ? ent.GameData.Properties.FirstOrDefault(x => String.Equals(x.Name, prop.Key, StringComparison.OrdinalIgnoreCase)) : null;
                if (gameDataProp != null) {
                    var emptyGd = String.IsNullOrWhiteSpace(gameDataProp.DefaultValue) || gameDataProp.DefaultValue == "0";
                    var emptyProp = String.IsNullOrWhiteSpace(prop.Value) || prop.Value == "0";

                    // The value hasn't changed from the default, don't write if it's an empty value
                    if (emptyGd && emptyProp) continue;
                }
                WriteProperty(sw, prop.Key, prop.Value);
            }

            if (solids.Any()) solids.ForEach(x => WriteSolid(sw, x)); // Brush entity
            else WriteProperty(sw, "origin", FormatVector3(ent.Origin)); // Point entity

            sw.WriteLine("}");
        }

        private void WriteWorld(StreamWriter sw, World world) {
            var solids = new List<Solid>();
            var entities = new List<Entity>();
            CollectSolids(solids, world);
            CollectEntities(entities, world);

            sw.WriteLine("{");

            WriteProperty(sw, "classname", world.EntityData.Name);
            WriteProperty(sw, "spawnflags", world.EntityData.Flags.ToString());
            WriteProperty(sw, "mapversion", "220");
            foreach (var prop in world.EntityData.Properties) {
                if (prop.Key == "classname" || prop.Key == "spawnflags" || prop.Key == "mapversion") continue;
                WriteProperty(sw, prop.Key, prop.Value);
            }
            solids.ForEach(x => WriteSolid(sw, x));

            sw.WriteLine("}");

            entities.ForEach(x => WriteEntity(sw, x));
        }

        private List<Entity> ReadAllEntities(StreamReader rdr, IDGenerator generator) {
            var list = new List<Entity>();
            string line;
            while ((line = CleanLine(rdr.ReadLine())) != null) {
                if (String.IsNullOrWhiteSpace(line)) continue;
                if (line == "{") list.Add(ReadEntity(rdr, generator));
            }
            return list;
        }

        /// <summary>
        /// Reads a map from a stream in MAP format.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <returns>The parsed map</returns>
        protected override DataStructures.MapObjects.Map GetFromStream(Stream stream) {
            using (var reader = new StreamReader(stream)) {
                var map = new DataStructures.MapObjects.Map();
                var allentities = ReadAllEntities(reader, map.IDGenerator);
                var worldspawn = allentities.FirstOrDefault(x => x.EntityData.Name == "worldspawn")
                                 ?? new Entity(0) { EntityData = { Name = "worldspawn" } };
                allentities.Remove(worldspawn);
                map.WorldSpawn.EntityData = worldspawn.EntityData;
                allentities.ForEach(x => x.SetParent(map.WorldSpawn, false));
                foreach (var obj in worldspawn.GetChildren().ToArray()) {
                    obj.SetParent(map.WorldSpawn, false);
                }
                map.WorldSpawn.UpdateBoundingBox(false);
                return map;
            }
        }

        /// <summary>
        /// Writes a map to a stream in MAP format.
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="map">The map to save</param>
        protected override void SaveToStream(Stream stream, DataStructures.MapObjects.Map map) {
            using (var writer = new StreamWriter(stream)) {
                WriteWorld(writer, map.WorldSpawn);
            }
        }
    }
}
