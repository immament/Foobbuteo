// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal sealed class ServerClientAssetBuildValidator : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (
            report.summary.platformGroup != BuildTargetGroup.Standalone
            || report.summary.GetSubtarget<StandaloneBuildSubtarget>() != StandaloneBuildSubtarget.Server
        )
            return;

        var offendingPaths = report
            .packedAssets.SelectMany(packed => packed.contents)
            .Select(asset => asset.sourceAssetPath)
            .Where(IsClientAsset)
            .Distinct()
            .OrderBy(path => path)
            .ToArray();
        if (offendingPaths.Length > 0)
            throw new BuildFailedException(
                "Client assets remain in the server build:\n" + string.Join("\n", offendingPaths)
            );
        Debug.Log("Server client-asset validation passed: no client images or match audio in packed assets.");
    }

    internal static bool IsClientAsset(string path) =>
        path != null
        && (
            path.StartsWith("Assets/ClientAssets/", StringComparison.Ordinal)
            || path.StartsWith("Assets/Audio/Match/", StringComparison.Ordinal)
        );
}
