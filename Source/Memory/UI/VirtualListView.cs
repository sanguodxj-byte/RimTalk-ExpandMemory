using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 虚拟化列表视图 - 只渲染可见项，提升大列表性能
    /// ★ v3.3.19: 性能优化 - 1000+项列表仍能流畅滚动
    /// </summary>
    public class VirtualListView<T> where T : class
    {
        private List<T> items = new List<T>();
        private Func<T, float> getItemHeight;
        private Action<Rect, T, int> drawItem;
        private Vector2 scrollPosition;

        // 布局缓存
        private readonly List<float> itemOffsets = new List<float>();
        private readonly List<float> itemHeights = new List<float>();
        private float totalHeight = 0f;

        // 配置
        public float ItemSpacing { get; set; } = 5f;
        public Vector2 ScrollPosition
        {
            get => scrollPosition;
            set => scrollPosition = value;
        }

        // ? 新增：空列表提示文本（可自定义）
        public string EmptyLabel { get; set; } = "No items to display";

        public VirtualListView(Func<T, float> getItemHeight, Action<Rect, T, int> drawItem)
        {
            this.getItemHeight = getItemHeight ?? throw new ArgumentNullException(nameof(getItemHeight));
            this.drawItem = drawItem ?? throw new ArgumentNullException(nameof(drawItem));
        }

        /// <summary>
        /// 设置列表数据
        /// </summary>
        public void SetItems(List<T> newItems)
        {
            items = newItems ?? new List<T>();
            RebuildLayout();
        }

        /// <summary>
        /// 重建布局缓存
        /// </summary>
        private void RebuildLayout()
        {
            itemOffsets.Clear();
            itemHeights.Clear();
            totalHeight = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                itemOffsets.Add(totalHeight);
                float height = getItemHeight(items[i]);
                itemHeights.Add(height);
                totalHeight += height + ItemSpacing;
            }
        }

        /// <summary>
        /// 绘制列表（虚拟化渲染）
        /// </summary>
        public void Draw(Rect rect)
        {
            if (items.Count == 0)
            {
                // 空列表提示
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(rect, EmptyLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, totalHeight);

            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            // 计算可见范围
            float visibleTop = scrollPosition.y;
            float visibleBottom = visibleTop + rect.height;

            GetVisibleIndexRange(visibleTop, visibleBottom, out int startIndex, out int endIndex);

            // 只渲染可见项
            for (int i = startIndex; i <= endIndex; i++)
            {
                float itemTop = itemOffsets[i];
                float itemHeight = itemHeights[i];
                Rect itemRect = new Rect(0f, itemTop, viewRect.width, itemHeight);
                drawItem(itemRect, items[i], i);
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// 查找Y范围内的项索引（用于拖拽选择）
        /// </summary>
        public List<int> FindItemsInRange(float minY, float maxY)
        {
            var indices = new List<int>();

            if (items.Count == 0)
                return indices;

            // 使用二分查找快速定位候选区间，然后线性扫描可交叉部分
            int start = FindFirstIndexAtOrBefore(minY);
            if (start < 0) start = 0;

            for (int i = start; i < items.Count; i++)
            {
                float itemTop = itemOffsets[i];
                float itemBottom = itemTop + itemHeights[i];

                if (itemTop > maxY)
                    break;

                if (itemBottom >= minY)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        /// <summary>
        /// 获取渲染统计信息（用于调试）
        /// </summary>
        public string GetRenderStats(float viewportHeight)
        {
            if (items.Count == 0)
                return "0/0 rendered";

            float visibleTop = scrollPosition.y;
            float visibleBottom = visibleTop + viewportHeight;

            GetVisibleIndexRange(visibleTop, visibleBottom, out int startIndex, out int endIndex);

            int visibleCount = 0;
            if (endIndex >= startIndex)
                visibleCount = endIndex - startIndex + 1;

            return $"{visibleCount}/{items.Count} rendered";
        }

        private void GetVisibleIndexRange(float visibleTop, float visibleBottom, out int startIndex, out int endIndex)
        {
            startIndex = FindFirstIndexAtOrBefore(visibleTop);
            if (startIndex < 0) startIndex = 0;

            endIndex = FindLastIndexAtOrAfter(visibleBottom);
            if (endIndex < 0) endIndex = items.Count - 1;

            if (startIndex < 0) startIndex = 0;
            if (endIndex >= items.Count) endIndex = items.Count - 1;
            if (endIndex < startIndex) endIndex = startIndex;
        }

        // 找到“可能包含 y 的项”（项底部 >= y）的最小 index
        private int FindFirstIndexAtOrBefore(float y)
        {
            int low = 0;
            int high = items.Count - 1;
            int result = items.Count; // default: not found

            while (low <= high)
            {
                int mid = (low + high) / 2;
                float top = itemOffsets[mid];
                float bottom = top + itemHeights[mid];

                if (bottom >= y)
                {
                    result = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            if (result == items.Count)
                return items.Count - 1;

            return result;
        }

        // 找到“可能包含 y 的项”（项顶部 <= y）的最大 index
        private int FindLastIndexAtOrAfter(float y)
        {
            int low = 0;
            int high = items.Count - 1;
            int result = -1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                float top = itemOffsets[mid];

                if (top <= y)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            if (result < 0)
                return 0;

            return result;
        }
    }
}
