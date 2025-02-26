﻿using System;
using System.IO;
using CBRE.Common.Mediator;
using CBRE.DataStructures.MapObjects;
using CBRE.Editor.Documents;
using CBRE.Editor.Popup;
using CBRE.Editor.Settings;
using CBRE.Providers;
using CBRE.Providers.Map;
using CBRE.Settings;
using ImGuiNET;
using NativeFileDialogNET;
using Num = System.Numerics;
using Path = System.IO.Path;

namespace CBRE.Editor {
    partial class GameMain : IMediatorListener {
        public void Notify(Enum message, object data) {
            /*if (Enum.TryParse(message, true, out HotkeysMediator hotkeys)) {

            }*/
            if (!Mediator.ExecuteDefault(this, message, data)) {
                throw new Exception("Invalid GameMain message: " + message + ", with data: " + data);
            }
        }

        public void MediatorError(object sender, MediatorExceptionEventArgs e) {
            Logging.Logger.ShowException(e.Exception, e.Message.ToString());
        }

        public void Subscribe() {
            Mediator.Subscribe(HotkeysMediator.FileNew, this);
            Mediator.Subscribe(HotkeysMediator.FileOpen, this);
        }

        public void FileNew() {
            Document doc = new(null, new DataStructures.MapObjects.Map());
            DocumentManager.AddAndSwitch(doc);
        }

        public void FileOpen() {
            var currFilePath = Path.GetDirectoryName(DocumentManager.CurrentDocument?.MapFile);
            if (string.IsNullOrEmpty(currFilePath)) { currFilePath = Directory.GetCurrentDirectory(); }

            var result = new NativeFileDialog()
                .SelectFile()
                .AddSupportedFiltersLoad()
                .Open(out string outPath, null, currFilePath);
            if (result == DialogResult.Okay) {
                try {
                    Map _map = MapProvider.GetMapFromFile(outPath);
                    DocumentManager.AddAndSwitch(new Document(outPath, _map));
                }
                catch (ProviderException e) {
                    GameMain.Instance.Popups.Add(new MessagePopup("Error", e.Message, new ImColor() { Value = new Num.Vector4(1f, 0f, 0f, 1f) }));
                }
            }
        }

        public void Options() {
            GameMain.Instance.Popups.Add(new SettingsPopup());
        }

        public void MapInformation() {
            GameMain.Instance.Popups.Add(new MapInformationPopup());
        }

        public void About() {
            GameMain.Instance.Popups.Add(new AboutPopup());
        }
    }
}
