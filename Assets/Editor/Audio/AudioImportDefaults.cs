#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// A-01: единые настройки импорта для звуков Assets/Audio/Game. Остальные папки не трогает.
// Длинные фоны и дроны — Streaming (не держим распакованные минуты в памяти), короткие — DecompressOnLoad
// с предзагрузкой, чтобы первая пугалка не подтормаживала.
public sealed class AudioImportDefaults : AssetPostprocessor
{
    public const string Root = "Assets/Audio/Game/";

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Root)) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        bool bed = assetPath.Contains("/Ambience/") || assetPath.Contains("/Music/");
        bool longHorror = assetPath.Contains("/Horror/") && (assetPath.Contains("Tinnitus") || assetPath.Contains("Riser"));
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.loadType = bed ? AudioClipLoadType.Streaming : longHorror ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        settings.quality = bed ? .6f : .75f;
        settings.preloadAudioData = !bed;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = bed;
        // Фоны и дроны — стерео 2D, остальное играет из точки в мире (моно уже на этапе подготовки).
        importer.forceToMono = !bed && !assetPath.Contains("/Horror/");
    }
}
#endif
