using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Две версии игры для Windows: Release — для игроков, Debug — Development Build со стартом со стадии
// (IntroSequence.useDebugSkip), логами и инструментами. Сцены берутся из Build Settings, в билд попадает
// только то, на что они ссылаются, плюс Resources. Папка Builds в git не идёт.
public static class GameBuild
{
    private const string Root = "Builds";
    private const string ExeName = "Knocks from beneath.exe";
    private const string ReportName = "build-report.txt";

    [MenuItem("Knocks/Build/Release (Windows)")]
    public static void Release() => Build("Release", BuildOptions.None);

    [MenuItem("Knocks/Build/Debug (Windows)")]
    public static void DebugBuild() => Build("Debug", BuildOptions.Development | BuildOptions.AllowDebugging);

    private static void Build(string flavor, BuildOptions options)
    {
        string folder = Path.Combine(Root, flavor);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);

        var player = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = Path.Combine(folder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = options
        };

        BuildReport report = BuildPipeline.BuildPlayer(player);
        if (flavor == "Release")
        {
            // Служебные папки Unity: нужны для разбора падений, игрокам их не отдают.
            foreach (string extra in Directory.GetDirectories(folder).Where(path => path.Contains("DoNotShip") || path.Contains("DontShip")))
                Directory.Delete(extra, true);
            // Библиотека InferenceEngine: сама сборка вырезана фильтром ниже.
            File.Delete(Path.Combine(folder, "DirectML.dll"));
        }

        File.WriteAllText(Path.Combine(folder, ReportName), Describe(flavor, report));
        Debug.Log($"[GameBuild] {flavor}: {report.summary.result}, {report.summary.totalErrors} ошибок, {folder}");
    }

    private static string Describe(string flavor, BuildReport report)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"{flavor} {PlayerSettings.bundleVersion}: {report.summary.result}");
        text.AppendLine($"Ошибок: {report.summary.totalErrors}, предупреждений: {report.summary.totalWarnings}, время: {report.summary.totalTime}");
        text.AppendLine($"Размер: {report.summary.totalSize / (1024f * 1024f):F1} МБ");
        text.AppendLine();
        text.AppendLine("Сборки:");
        foreach (BuildFile file in report.GetFiles().Where(file => file.path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).OrderBy(file => file.path))
            text.AppendLine("  " + Path.GetFileName(file.path));
        text.AppendLine();
        text.AppendLine("Самые большие ассеты:");
        foreach (PackedAssetInfo asset in report.packedAssets.SelectMany(pack => pack.contents).OrderByDescending(asset => asset.packedSize).Take(40))
            text.AppendLine($"  {asset.packedSize / (1024f * 1024f),7:F1} МБ  {asset.sourceAssetPath}");
        return text.ToString();
    }

    // Пакеты редакторских инструментов тянут в плеер сборки без ограничения по платформе; игра их не использует.
    private sealed class ReleaseAssemblyFilter : IFilterBuildAssemblies
    {
        private static readonly string[] EditorTools =
        {
            "MCPForUnity.Runtime.dll", "Unity.AI.MCP.Runtime.dll", "Unity.AI.Tracing.dll",
            "Unity.InferenceEngine.dll", "Unity.InferenceEngine.Tokenization.dll"
        };

        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            if ((buildOptions & BuildOptions.Development) != 0) return assemblies;
            return assemblies.Where(path => !EditorTools.Contains(Path.GetFileName(path))).ToArray();
        }
    }
}
