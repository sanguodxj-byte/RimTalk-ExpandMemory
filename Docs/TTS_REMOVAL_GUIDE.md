# TTS 功能删除指南

## ? 已完成

1. ? 删除 TTS 核心服务文件
   - `Source/Memory/TTS/AzureTTSService.cs`
   - `Source/Memory/TTS/ITTSService.cs`
   - `Source/Memory/TTS/TTSAudioPlayer.cs`
   - `Source/Memory/TTS/TTSHelper.cs`
   - `Source/Memory/TTS/TTSManager.cs`

2. ? 删除 TTS Patch 文件
   - `Source/Patches/TTSAutoPlayPatcher.cs`

3. ? 删除 TTS 文档
   - `Docs/TTS_*.md`
   - `Docs/KNOWLEDGE_TEXTAREA_FIX.md`

## ? 需要手动完成

### 1. 编辑 `Source/RimTalkSettings.cs`

**需要删除的部分：**

#### A. 字段定义（第 51-58 行附近）
```csharp
// TTS 语音服务配置
public bool enableTTS = false;
public string ttsProvider = "Azure";
public string azureSpeechKey = "";
public string azureSpeechRegion = "eastus";
public string ttsVoice = "zh-CN-XiaoxiaoNeural";
public float ttsRate = 1.0f;
public float ttsVolume = 1.0f;
public bool ttsAutoPlay = false;
```

#### B. UI 折叠状态（第 97 行附近）
```csharp
private static bool expandTTSSettings = false;
```

#### C. ExposeData 序列化（第 130-138 行附近）
```csharp
Scribe_Values.Look(ref enableTTS, "tts_enableTTS", false);
Scribe_Values.Look(ref ttsProvider, "tts_provider", "Azure");
Scribe_Values.Look(ref azureSpeechKey, "tts_azureSpeechKey", "");
Scribe_Values.Look(ref azureSpeechRegion, "tts_azureSpeechRegion", "eastus");
Scribe_Values.Look(ref ttsVoice, "tts_voice", "zh-CN-XiaoxiaoNeural");
Scribe_Values.Look(ref ttsRate, "tts_rate", 1.0f);
Scribe_Values.Look(ref ttsVolume, "tts_volume", 1.0f);
Scribe_Values.Look(ref ttsAutoPlay, "tts_autoPlay", false);
```

#### D. DoSettingsWindowContents 方法（第 236 行附近）
删除这行：
```csharp
DrawCollapsibleSection(listingStandard, "?? TTS 语音服务", ref expandTTSSettings, () => DrawTTSSettings(listingStandard));
```

#### E. DrawTTSSettings 方法（整个方法，约 60-100 行）
```csharp
private void DrawTTSSettings(Listing_Standard listing)
{
    // ... 删除整个方法 ...
}
```

#### F. TestTTS 方法（整个方法，约 20-30 行）
```csharp
private void TestTTS()
{
    // ... 删除整个方法 ...
}
```

### 2. 验证编译

删除上述代码后，运行：
```powershell
msbuild RimTalk-ExpandMemory.csproj /p:Configuration=Release
```

应该编译成功。

### 3. 更新 About.xml（可选）

如果 `About/About.xml` 中提到了 TTS 功能，也可以删除相关描述。

---

## ?? 快速查找方法

在 `Source/RimTalkSettings.cs` 中搜索以下关键词：
- `TTS`
- `enableTTS`
- `ttsProvider`
- `azureSpeech`
- `DrawTTSSettings`
- `TestTTS`

删除所有包含这些关键词的代码块。

---

## ? 验证清单

- [ ] 删除 TTS 字段定义
- [ ] 删除 TTS UI 折叠状态
- [ ] 删除 TTS 序列化代码
- [ ] 删除 DrawTTSSettings 调用
- [ ] 删除 DrawTTSSettings 方法
- [ ] 删除 TestTTS 方法
- [ ] 编译成功

---

**完成后，TTS 功能将完全从项目中移除。**
