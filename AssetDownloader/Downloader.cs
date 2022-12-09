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

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace SimplicitEditor.AssetDownloader
{
    public static class Downloader
    {
        public class Payload
        {
            public string Name { get; set; }
            public string CreatorName { get; set; }
            public string License { get; set; }
            public string Url { get; set; }

            public string DownloadUrl { get; set; }
        }

        public static void DownloadWithPayload(Payload payload, string outputDirectory)
        {
            var fileReq = UnityWebRequest.Get(payload.DownloadUrl);

            if (!outputDirectory.EndsWith("/"))
                outputDirectory += "/";

            outputDirectory = $"{outputDirectory}{payload.Name}";

            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(outputDirectory))) 
            {
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(outputDirectory);
                ProjectWindowUtil.ShowCreatedAsset(Selection.activeObject);

                var @continue = EditorUtility.DisplayDialog("Asset already exists", $"An asset already exists at {outputDirectory}. Continue downloading this one in a unique directory?",
                    "Continue Downloading", "Cancel");

                if (!@continue)
                    return;
            }

            outputDirectory = AssetDatabase.GenerateUniqueAssetPath(outputDirectory);

            var tempFile = System.IO.Path.GetTempFileName();
            fileReq.downloadHandler = new DownloadHandlerFile(tempFile, false);

            EditorUtility.DisplayProgressBar("Downloading Game Asset...", "Getting file...", float.NaN);

            fileReq.SendWebRequest().completed += (op) =>
            {
                try
                {
                    if (fileReq.result == UnityWebRequest.Result.Success)
                    {
                        EditorUtility.DisplayProgressBar("Downloading Game Asset...", "Parsing file...", float.NaN);

                        ParseZipFile(tempFile, outputDirectory);

                        EditorUtility.ClearProgressBar();
                        AssetDatabase.Refresh();

                        var assetPathToShow = outputDirectory;
                        var assetPathsToSelect = new System.Collections.Generic.List<string>();

                        foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(Mesh)}", new string[] { outputDirectory }))
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);

                            assetPathToShow = path;
                            assetPathsToSelect.Add(path);
                        }

                        ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadMainAssetAtPath(assetPathToShow));
                        Selection.objects = assetPathsToSelect.Select(path => AssetDatabase.LoadMainAssetAtPath(path)).ToArray();

                        var creditFile = AssetDatabase.GenerateUniqueAssetPath(outputDirectory + "/" + payload.Name + ".credit");
                        var creditAssetPaths = AssetDatabase.FindAssets("", new string[] { outputDirectory })
                            .Select(guid => AssetDatabase.GUIDToAssetPath(guid));

#if SIMPLICIT_CREDITS
                        creditFile += ".asset";

                        var credit = ScriptableObject.CreateInstance<Simplicit.Data.AssetCredit>();
                        credit.Url = payload.Url;
                        credit.CreatorName = payload.CreatorName;
                        credit.License = payload.License;
                        credit.Assets = creditAssetPaths
                            .Select(path => AssetDatabase.LoadMainAssetAtPath(path))
                            .ToArray();

                        AssetDatabase.CreateAsset(credit, creditFile);
#else
                        creditFile += ".txt";

                        System.IO.File.WriteAllText(creditFile,
                            $"Url: {payload.Url}\n" +
                            $"Creator: {payload.CreatorName}\n" +
                            $"License: {payload.License}\n" +
                            $"Assets: {string.Join(", ", creditAssetPaths)}");

                        AssetDatabase.Refresh();
#endif
                    }
                    else
                    {
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("An error occurred while downloading the file", fileReq.error, "OK");
                    }
                }
                catch (Exception ex)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("An error occurred while parsing the asset", ex.ToString(), "OK");
                    throw;
                }
                finally
                {
                    if (System.IO.File.Exists(tempFile))
                        System.IO.File.Delete(tempFile);

                    fileReq.Dispose();
                }
            };
        }

        private static void ParseZipFile(string zipPath, string outputDirectory)
        {
            var extractTo = new System.IO.DirectoryInfo(System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString()));

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractTo.FullName, true);

                if (!System.IO.Directory.Exists(outputDirectory))
                    System.IO.Directory.CreateDirectory(outputDirectory);

                foreach (var file in extractTo.GetFiles("*.*", System.IO.SearchOption.AllDirectories))
                {
                    if (file.Extension.Contains("zip", StringComparison.OrdinalIgnoreCase))
                    {
                        ParseZipFile(file.FullName, outputDirectory);
                    }
                    else
                    {
                        // move file to output
                        var newPath = System.IO.Path.Combine(outputDirectory, file.Name);
                        var counted = 0;

                        while (System.IO.File.Exists(newPath) && counted < 10)
                        {
                            counted++;
                            newPath = System.IO.Path.Combine(outputDirectory, counted.ToString() + file.Name);
                        }

                        if (!System.IO.File.Exists(newPath))
                            file.MoveTo(newPath);
                    }
                }
            }
            finally
            {
                if (extractTo.Exists)
                    extractTo.Delete(true);
            }
        }
    }
}
