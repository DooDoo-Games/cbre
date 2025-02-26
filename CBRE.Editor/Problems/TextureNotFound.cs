﻿using CBRE.DataStructures.MapObjects;
using CBRE.Editor.Actions;
using CBRE.Editor.Actions.MapObjects.Operations;
using CBRE.Providers.Texture;
using System.Collections.Generic;
using System.Linq;

namespace CBRE.Editor.Problems {
    public class TextureNotFound : IProblemCheck {
        public IEnumerable<Problem> Check(Map map, bool visibleOnly) {
            var faces = map.WorldSpawn
                .Find(x => x is Solid && (!visibleOnly || (!x.IsVisgroupHidden && !x.IsCodeHidden)))
                .OfType<Solid>()
                .SelectMany(x => x.Faces)
                .Where(x => x.Texture.Texture == null)
                .ToList();
            foreach (var name in faces.Select(x => x.Texture.Name).Distinct()) {
                yield return new Problem(GetType(), map, faces.Where(x => x.Texture.Name == name).ToList(), Fix, "Texture not found: " + name, "This texture was not found in the currently loaded texture folders. Ensure that the correct texture folders are loaded. Fixing the problems will reset the face textures to the default texture.");
            }
        }

        public IAction Fix(Problem problem) {
            return new EditFace(problem.Faces, (d, x) => {
                var ignored = "{#!~+-0123456789".ToCharArray();
                var def = TextureProvider.Packages.SelectMany(p => p.Items.Values)
                    .OrderBy(i => new string(i.Name.Where(c => !ignored.Contains(c)).ToArray()) + "Z")
                    .FirstOrDefault();
                if (def != null) {
                    x.Texture.Name = def.Name;
                    x.Texture.Texture = def.Texture;
                    x.CalculateTextureCoordinates(true);
                }
            }, true);
        }
    }
}
