using System;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 简单的文本输入对话框，用于导入常识
    /// </summary>
    public class Dialog_TextInput : Window
    {
        private string title;
        private string description;
        private string text;
        private Action<string> onAccept;
        private Action onCancel;
        private bool multiline;
        private Vector2 scrollPos = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600f, multiline ? 500f : 250f);

        public Dialog_TextInput(string title, string description, string initialText, Action<string> onAccept, Action onCancel = null, bool multiline = false)
        {
            this.title = title;
            this.description = description;
            this.text = initialText ?? "";
            this.onAccept = onAccept;
            this.onCancel = onCancel;
            this.multiline = multiline;
            
            this.doCloseX = true;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), title);
            Text.Font = GameFont.Small;

            float y = 40f;
            
            if (!string.IsNullOrEmpty(description))
            {
                float descHeight = Text.CalcHeight(description, inRect.width);
                Widgets.Label(new Rect(0f, y, inRect.width, descHeight), description);
                y += descHeight + 10f;
            }

            Rect textRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
            if (multiline)
            {
                Rect viewRect = new Rect(0f, 0f, textRect.width - 16f, Mathf.Max(Text.CalcHeight(text, textRect.width - 16f), textRect.height));
                Widgets.BeginScrollView(textRect, ref scrollPos, viewRect);
                text = Widgets.TextArea(viewRect, text);
                Widgets.EndScrollView();
            }
            else
            {
                text = Widgets.TextField(textRect, text);
            }

            Rect buttonRect = new Rect(inRect.width - 220f, inRect.height - 40f, 100f, 35f);
            if (Widgets.ButtonText(buttonRect, "RimTalk_Knowledge_Save".Translate()))
            {
                onAccept?.Invoke(text);
                Close();
            }

            buttonRect.x += 110f;
            if (Widgets.ButtonText(buttonRect, "RimTalk_Knowledge_Cancel".Translate()))
            {
                onCancel?.Invoke();
                Close();
            }
        }
    }
}
