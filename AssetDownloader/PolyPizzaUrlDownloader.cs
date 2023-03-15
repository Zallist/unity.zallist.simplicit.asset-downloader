// GIST URL: https://gist.github.com/Zallist/6008c195a8607e7d8d22892052513a93

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
using UnityEngine.Networking;
using System;

namespace SimplicitEditor.AssetDownloader
{
    [InitializeOnLoad]
    internal class PolyPizzaUrlDownloader : ScriptableWizard
    {
        // Using the fact that window.__SERVER_APP_STATE__ is in the html to get the model information
        private static readonly System.Text.RegularExpressions.Regex appStateRegex = new(@"<script.+>\s*window\.__SERVER_APP_STATE__\s*=\s*(?<state>{.+})\s*</script>",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static string DefaultAssetFolder
        {
            get => EditorPrefs.GetString($"{nameof(PolyPizzaUrlDownloader)}_{nameof(DefaultAssetFolder)}", "Assets/Models/PolyPizza/");
            set => EditorPrefs.SetString($"{nameof(PolyPizzaUrlDownloader)}_{nameof(DefaultAssetFolder)}", value);
        }

        private static bool ListenToClipboard
        {
            get => EditorPrefs.GetBool($"{nameof(PolyPizzaUrlDownloader)}_{nameof(ListenToClipboard)}", true);
            set
            {
                if (value == ListenToClipboard)
                    return;

                EditorPrefs.SetBool($"{nameof(PolyPizzaUrlDownloader)}_{nameof(ListenToClipboard)}", value);

                if (value)
                    EditorApplication.update += ClipboardListener;
            }
        }

        static PolyPizzaUrlDownloader()
        {
            SetupInterop();
        }

        [SerializeField]
        protected string url = "https://poly.pizza/";

        [SerializeField]
        protected string folderPath;

        protected virtual void OnEnable()
        {
            folderPath = DefaultAssetFolder;
        }

        protected override bool DrawWizardGUI()
        {
            EditorGUI.BeginChangeCheck();

            url = EditorGUILayout.TextField("URL", url);
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
            var url = this.url;
            var folderPath = this.folderPath;

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(folderPath))
                return;

            if (!System.IO.Directory.Exists(folderPath))
                System.IO.Directory.CreateDirectory(folderPath);

            DefaultAssetFolder = folderPath;

            var pageReq = UnityWebRequest.Get(url);

            pageReq.downloadHandler = new DownloadHandlerBuffer();

            EditorUtility.DisplayProgressBar("Getting model...", "Getting webpage", float.NaN);

            pageReq.SendWebRequest().completed += (op) =>
            {
                try
                {
                    if (pageReq.result == UnityWebRequest.Result.Success)
                    {
                        HandlePageRequest(pageReq.downloadHandler.text, folderPath, url);
                    }
                    else
                    {
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("An error occurred while downloading the webpage", pageReq.error, "OK");
                    }
                }
                catch (Exception ex)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("An error occurred while parsing the webpage", ex.ToString(), "OK");
                    throw;
                }
                finally
                {
                    pageReq.Dispose();
                }
            };
        }

        private void HandlePageRequest(string text, string folderPath, string url)
        {
            var match = appStateRegex.Match(text);

            if (!match.Success)
                throw new Exception("An error occurred when parsing the page state");

            var stateJson = match.Groups["state"].Value;
            var state = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(stateJson);

            var model = state.initialData.model;
            var zipFile = $"https://static.poly.pizza/{model.ResourceID}.zip";

            Downloader.DownloadWithPayload(new Downloader.Payload()
            {
                Name = model.Title,
                CreatorName = model.Creator?.Username,
                License = model.Licence,
                DownloadUrl = zipFile,
                Url = url
            }, folderPath);
        }

        public static void Show(string url = null)
        {
            var wizard = ScriptableWizard.DisplayWizard<PolyPizzaUrlDownloader>("Poly.Pizza Downloader");

            if (!string.IsNullOrEmpty(url))
                wizard.url = url;
        }

        [MenuItem("Tools/Game Asset Downloader/Poly.Pizza", priority = -2051)]
        public static void DownloadPolyPizza() => Show();

        #region Interop
        private static void SetupInterop()
        {
            EditorApplication.update -= ClipboardListener;
            EditorApplication.update += ClipboardListener;
        }

        private static string lastHandled = null;
        private static string LastHandledPolyPizzaUrl
        {
            get => EditorPrefs.GetString($"{nameof(PolyPizzaUrlDownloader)}_{nameof(LastHandledPolyPizzaUrl)}", "");
            set => EditorPrefs.SetString($"{nameof(PolyPizzaUrlDownloader)}_{nameof(LastHandledPolyPizzaUrl)}", value);
        }

        private static readonly System.Text.RegularExpressions.Regex urlRegex = new(@"(?:\b|^) (?<url>https://poly.pizza/m/.+) (?:\s|,|\.|$)",
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

            if (copyBuffer.Contains("poly.pizza", StringComparison.OrdinalIgnoreCase))
            {
                var match = urlRegex.Match(copyBuffer);

                if (match.Success)
                {
                    var url = match.Groups["url"].Value;

                    if (LastHandledPolyPizzaUrl != url)
                    {
                        LastHandledPolyPizzaUrl = url;
                        Show(url);
                    }
                }
            }
        }
        #endregion
    }
}
