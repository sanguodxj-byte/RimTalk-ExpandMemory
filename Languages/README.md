# RimTalk-ExpandMemory Translation Guide

## 关于 / About / について / ?? / О проекте

RimTalk-ExpandMemory 是一个为 RimWorld 模组 RimTalk 提供记忆系统扩展的模组。

This is a mod that provides memory system expansion for the RimWorld mod RimTalk.

## 翻译状态 / Translation Status

| 语言 / Language | 状态 / Status | 翻译者 / Translator |
|-----------------|---------------|-------------------|
| 简体中文 (ChineseSimplified) | ? 完成 | 原作者 |
| English | ? Complete | Original Author |
| 日本語 (Japanese) | ? 完了 | AI Translation |
| ??? (Korean) | ? ?? | AI Translation |
| Русский (Russian) | ? Завершено | AI Translation |

## 需要翻译的文件 / Files to Translate

每种语言需要翻译以下文件：

Each language needs the following files translated:

1. `Languages\[YourLanguage]\Keyed\MemoryPatch.xml` - 主要翻译文件 / Main translation file
2. `Languages\[YourLanguage]\DefInjected\MainButtonDef\RimTalk_Memory.xml` - 按钮定义 / Button definition

## 如何贡献翻译 / How to Contribute Translations

### 步骤 / Steps

1. 复制 `Languages\English\` 文件夹
2. 重命名为您的语言（例如：French, German, Spanish等）
3. 翻译 `Keyed\MemoryPatch.xml` 中的所有文本
4. 翻译 `DefInjected\MainButtonDef\RimTalk_Memory.xml` 中的文本
5. 提交 Pull Request 或通过 GitHub Issues 分享您的翻译

---

1. Copy the `Languages\English\` folder
2. Rename it to your language (e.g., French, German, Spanish, etc.)
3. Translate all text in `Keyed\MemoryPatch.xml`
4. Translate text in `DefInjected\MainButtonDef\RimTalk_Memory.xml`
5. Submit a Pull Request or share your translation via GitHub Issues

### 翻译注意事项 / Translation Guidelines

- **保持 XML 标签不变** / Keep XML tags unchanged
- **保持占位符格式** / Keep placeholder format (e.g., `{0}`, `{1}`, `{2}`, `{3}`)
- **保持文件编码为 UTF-8** / Keep file encoding as UTF-8
- **测试您的翻译** / Test your translations in-game
- **遵循 RimWorld 翻译规范** / Follow RimWorld translation conventions

### 占位符说明 / Placeholder Explanations

- `{0}` - 通常是数字或名称 / Usually a number or name
- `{1}`, `{2}`, `{3}` - 额外的参数 / Additional parameters

例如 / Example:
- `{0}'s Memories` - 将 {0} 替换为入植者名称 / {0} will be replaced with colonist name
- `Short-term: {0}/{1}` - {0} 是当前值，{1} 是最大值 / {0} is current value, {1} is max value

## 支持的 RimWorld 语言 / Supported RimWorld Languages

RimWorld 支持以下语言，欢迎为这些语言贡献翻译：

RimWorld supports the following languages, translations for these are welcome:

- Catalan (Català)
- Czech (?e?tina)
- Danish (Dansk)
- Dutch (Nederlands)
- English
- Estonian (Eesti)
- Finnish (Suomi)
- French (Fran?ais)
- German (Deutsch)
- Hungarian (Magyar)
- Italian (Italiano)
- Japanese (日本語) ?
- Korean (???) ?
- Latvian (Latvie?u)
- Lithuanian (Lietuvi?)
- Norwegian (Norsk)
- Polish (Polski)
- Portuguese (Português)
- Brazilian Portuguese (Português Brasileiro)
- Romanian (Rom?n?)
- Russian (Русский) ?
- Slovak (Sloven?ina)
- Spanish (Espa?ol)
- Latin American Spanish (Espa?ol Latinoamericano)
- Swedish (Svenska)
- Turkish (Türk?e)
- Ukrainian (Укра?нська)
- Chinese Simplified (简体中文) ?
- Chinese Traditional (繁體中文)

## 联系方式 / Contact

- GitHub Issues: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/issues
- GitHub Discussions: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/discussions

## 许可证 / License

翻译贡献将遵循本项目的许可证。

Translation contributions will follow this project's license.

---

感谢您的贡献！/ Thank you for your contribution!
