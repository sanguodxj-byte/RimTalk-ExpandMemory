using RimTalk.MemoryPatch;
using System.IO;
using TMPro;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.Utils
{
    public static class TextUtil
    {
        private static TMP_FontAsset _font;
        public static TMP_FontAsset InternalFont
        {
            get
            {
                _font ??= GetFont();
                return _font;
            }
        }

        public static string Translate(this MemoryLayer? layer) => layer switch
        {
            null => "RimTalk.Memory.Utils.Layers".Translate(),
            _ => ((MemoryLayer)layer).Translate()
        };
        public static string Translate(this MemoryLayer layer) => Translator.Translate(layer switch
        {
            MemoryLayer.Active => "RimTalk.Memory.Utils.Layer_Active",
            MemoryLayer.Situational => "RimTalk.Memory.Utils.Layer_Situational",
            MemoryLayer.EventLog => "RimTalk.Memory.Utils.Layer_EventLog",
            MemoryLayer.Archive => "RimTalk.Memory.Utils.Layer_Archive",
            _ => "RimTalk.Memory.Utils.Layer_Default"
        });

        public static string Translate(this MemoryType? type) => type switch
        {
            null => "RimTalk.Memory.Utils.Types".Translate(),
            _ => ((MemoryType)type).Translate()
        };
        public static string Translate(this MemoryType type) => Translator.Translate(type switch
        {
            MemoryType.Conversation => "RimTalk_MindStream_Conversation",
            MemoryType.Action => "RimTalk_MindStream_Action",
            MemoryType.Summarization => "RimTalk_Archive_TypeSummary",
            MemoryType.Event => "RimTalk_Archive_TypeEvent",
            MemoryType.Emotion => "RimTalk_Archive_TypeEmotion",
            MemoryType.Relationship => "RimTalk_Archive_TypeRelationship",
            _ => "RimTalk_Archive_Memory"
        });

        private static TMP_FontAsset GetFont()
        {
            string path = Path.Combine(LoadedModManager.GetMod<RimTalkMemoryPatchMod>().Content.RootDir,
                "Resource", "AssetBundles", "Windows64", "Fonts", "arial_msyh_tmp");

            if (AssetBundle.LoadFromFile(path) is not { } bundle)
            {
                Log.Warning($"[RimTalk.Memory] 字体 AssetBundle 加载失败: {path}");
                return null;
            }

            var font = bundle.LoadAsset<TMP_FontAsset>("arial");
            font.fallbackFontAssetTable = [bundle.LoadAsset<TMP_FontAsset>("microsoftyahei")];
            bundle.Unload(false);

            return font;
        }
    }
}
