﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CBRE.Providers.GameData {
    public abstract class GameDataProvider {
        private static readonly List<GameDataProvider> RegisteredProviders;

        static GameDataProvider() {
            RegisteredProviders = new List<GameDataProvider>();
        }

        public static void Register(GameDataProvider provider) {
            RegisteredProviders.Add(provider);
        }

        public static void Deregister(GameDataProvider provider) {
            RegisteredProviders.Remove(provider);
        }

        public static DataStructures.GameData.GameData GetGameDataFromStream(Stream stream) {
            var provider = RegisteredProviders.FirstOrDefault(p => p.IsValidForStream(stream));
            if (provider != null) {
                var gd = provider.GetFromStream(stream);
                return gd;
            }
            throw new ProviderNotFoundException("No GameData provider was found for this stream.");
        }

        protected virtual bool IsValidForFile(string filename) {
            Stream strm = new FileStream(filename, FileMode.Open, FileAccess.Read);
            return IsValidForStream(strm);
        }

        protected virtual bool IsValidForString(string contents) {
            var length = Encoding.UTF8.GetByteCount(contents);
            Stream strm = new MemoryStream(length);
            strm.Write(Encoding.UTF8.GetBytes(contents), 0, length);
            return IsValidForStream(strm);
        }

        protected abstract bool IsValidForStream(Stream stream);

        protected virtual DataStructures.GameData.GameData GetFromFile(string filename) {
            using (var strm = new FileStream(filename, FileMode.Open, FileAccess.Read)) {
                return GetFromStream(strm);
            }
        }

        protected virtual DataStructures.GameData.GameData GetFromString(string contents) {
            var length = Encoding.UTF8.GetByteCount(contents);
            using (var strm = new MemoryStream(length)) {
                strm.Write(Encoding.UTF8.GetBytes(contents), 0, length);
                return GetFromStream(strm);
            }
        }

        protected abstract DataStructures.GameData.GameData GetFromStream(Stream stream);
    }
}
