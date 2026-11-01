// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Imma.Foobbuteo.Editor
{
    internal sealed class CodePagesPostBuild : IPostprocessBuildWithReport
    {
        private const string PackageAssetPath = "Packages/org.nuget.system.text.encoding.codepages/package.json";
        private const string AssemblyName = "System.Text.Encoding.CodePages.dll";

        public int callbackOrder => int.MaxValue;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform is not BuildTarget.StandaloneWindows and not BuildTarget.StandaloneWindows64)
            {
                // Debug.Log($"CodePagesPostBuild Skiped. Platform: {report.summary.platform}");
                return;
            }

            var package =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackageAssetPath)
                ?? throw new BuildFailedException(
                    $"Cannot locate the {PackageAssetPath} package required to restore {AssemblyName}."
                );

            string sourcePath = Path.Combine(package.resolvedPath, "lib", "netstandard2.0", "Windows", AssemblyName);
            if (!File.Exists(sourcePath))
            {
                throw new BuildFailedException($"Code-pages assembly not found at: {sourcePath}");
            }

            string outputDirectory = Path.GetDirectoryName(report.summary.outputPath);
            string playerName = Path.GetFileNameWithoutExtension(report.summary.outputPath);
            string destinationPath = Path.Combine(outputDirectory, $"{playerName}_Data", "Managed", AssemblyName);
            if (!File.Exists(destinationPath))
            {
                throw new BuildFailedException($"Built code-pages assembly was not found at: {destinationPath}");
            }

            File.Copy(sourcePath, destinationPath, true);

            long sourceLength = new FileInfo(sourcePath).Length;
            long destinationLength = new FileInfo(destinationPath).Length;
            if (destinationLength != sourceLength)
            {
                throw new BuildFailedException(
                    $"Failed to restore {AssemblyName}: expected {sourceLength} bytes, got {destinationLength} bytes."
                );
            }

            Debug.Log($"Restored full {AssemblyName} ({destinationLength} bytes): {destinationPath}");
        }
    }
}
