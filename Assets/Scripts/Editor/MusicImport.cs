using UnityEditor;
using UnityEngine;

namespace LLTacticalWar.EditorTools
{
    // Music under Assets/Resources/Music streams from disk as Vorbis instead of decompressing a
    // minute-plus stereo track into memory on load.
    public class MusicImport : AssetPostprocessor
    {
        const string Root = "Assets/Resources/" + AudioBank.MusicDir;

        public override uint GetVersion() => 1;

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var a = (AudioImporter)assetImporter;
            a.forceToMono = false;
            a.loadInBackground = true;
            a.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.Streaming,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.7f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
            };
        }
    }
}
