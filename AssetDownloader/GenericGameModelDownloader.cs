/*
MIT License

Copyright (c) 2022 Zallist

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using UnityEngine;
using UnityEditor;
using System;

namespace SimplicitEditor.AssetDownloader
{
    [InitializeOnLoad]
    internal class GenericGameModelDownloader : ScriptableWizard
    {
        private static string DefaultAssetFolder
        {
            get => EditorPrefs.GetString($"{nameof(GenericGameModelDownloader)}_{nameof(DefaultAssetFolder)}", "Assets/Models/Placeholders/");
            set => EditorPrefs.SetString($"{nameof(GenericGameModelDownloader)}_{nameof(DefaultAssetFolder)}", value);
        }

        private static bool ListenToClipboard
        {
            get => EditorPrefs.GetBool($"{nameof(GenericGameModelDownloader)}_{nameof(ListenToClipboard)}", true);
            set
            {
                if (value == ListenToClipboard)
                    return;

                EditorPrefs.SetBool($"{nameof(GenericGameModelDownloader)}_{nameof(ListenToClipboard)}", value);

                if (value)
                    EditorApplication.update += ClipboardListener;
            }
        }

        static GenericGameModelDownloader()
        {
            SetupInterop();
        }

        private const string PAYLOAD_PREFIX = "unity-asset-payload::";

        [SerializeField]
        protected string payload = "unity-asset-payload::{}";

        [SerializeField]
        protected string folderPath;

        protected virtual void OnEnable()
        {
            folderPath = DefaultAssetFolder;
        }

        protected override bool DrawWizardGUI()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Payload", GUILayout.ExpandWidth(true));
            payload = EditorGUILayout.TextArea(payload, EditorStyles.textArea, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight * 4f));
            folderPath = EditorGUILayout.TextField("Folder Path", folderPath);

            EditorGUILayout.Separator();

            EditorGUI.BeginChangeCheck();

            var listenToClipboard = EditorGUILayout.ToggleLeft("Listen to Clipboard", ListenToClipboard, GUILayout.ExpandWidth(true));

            if (EditorGUI.EndChangeCheck())
                ListenToClipboard = listenToClipboard;

            return EditorGUI.EndChangeCheck();
        }

        void OnWizardCreate()
        {
            var payload = this.payload;
            var folderPath = this.folderPath;

            if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(folderPath))
                return;

            if (payload.StartsWith(PAYLOAD_PREFIX))
                payload = payload.Substring(PAYLOAD_PREFIX.Length);

            var parsedPayload = Newtonsoft.Json.JsonConvert.DeserializeObject<Downloader.Payload>(payload);

            if (parsedPayload == null)
            {
                Debug.LogError($"{payload} is not valid JSON");
                return;
            }

            if (!System.IO.Directory.Exists(folderPath))
                System.IO.Directory.CreateDirectory(folderPath);

            DefaultAssetFolder = folderPath;

            Downloader.DownloadWithPayload(parsedPayload, folderPath);
        }

        public static void Show(string payload = null)
        {
            var wizard = ScriptableWizard.DisplayWizard<GenericGameModelDownloader>("Generic Game Asset Downloader");

            if (!string.IsNullOrEmpty(payload))
                wizard.payload = payload;
        }

        [MenuItem("Tools/Game Asset Downloader/Generic Asset Payload", priority = -3051)]
        public static void DownloadGenericGameAsset() => Show();

        #region Interop
        private static void SetupInterop()
        {
            EditorApplication.update -= ClipboardListener;
            EditorApplication.update += ClipboardListener;
        }

        private static string lastHandled = null;
        private static string LastHandledPayload
        {
            get => EditorPrefs.GetString($"{nameof(GenericGameModelDownloader)}_{nameof(LastHandledPayload)}", "");
            set => EditorPrefs.SetString($"{nameof(GenericGameModelDownloader)}_{nameof(LastHandledPayload)}", value);
        }

        private static readonly System.Text.RegularExpressions.Regex payloadRegex = new(PAYLOAD_PREFIX + @"(?<payload>\{.+\})$",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static void ClipboardListener()
        {
            if (!ListenToClipboard)
            {
                EditorApplication.update -= ClipboardListener;
                return;
            }

            var copyBuffer = EditorGUIUtility.systemCopyBuffer;

            if (copyBuffer == lastHandled)
                return;

            lastHandled = copyBuffer;

            if (copyBuffer.Contains(PAYLOAD_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                var match = payloadRegex.Match(copyBuffer);

                if (match.Success)
                {
                    var payload = match.Groups["payload"].Value;

                    if (LastHandledPayload != payload)
                    {
                        LastHandledPayload = payload;
                        Show(payload);
                    }
                }
            }
        }
        #endregion

        [MenuItem("Tools/Game Asset Downloader/Sketchfab Browser Plugin", priority = -1050)]
        private static void GetBrowserPlugin()
        {
            System.Diagnostics.Process.Start("https://greasyfork.org/en/scripts/454630-sketchfab-unity-asset-payload-creator");
        }
    }
}
