using System;
using Unity.Mathematics;

namespace RimTalk.Memory.Utils
{

    /// <summary>
    /// 专为 RimWorld 设计的 0GC 环形缓冲区
    /// 注意不支持foreach
    /// 缓冲区尺寸会被强制设定为二的幂
    /// </summary>
    public class RimRingBuffer<T>
    {
        // 核心：内部数组
        private readonly T[] _buffer;

        // 元数据
        private readonly int _capacity; // 容量（数组尺寸）
        private readonly int _capacityMask; // 魔法掩码（数组尺寸-1）
        private int _head = 0; // 头指针，指向最老的元素
        private int _count = 0; // 当前元素数量，永远不超过数组尺寸

        // 外部接口
        public int Count => _count;

        public RimRingBuffer(int newCapacity)
        {
            // 取输入值的下一个 2 的幂，保证数组尺寸是 2 的幂，以使用位运算实现环绕
            _capacity = math.ceilpow2(newCapacity);

            // 完成初始化
            _buffer = new T[_capacity];
            _capacityMask = _capacity - 1;
        }

        /// <summary>
        /// 添加新元素。如果满了，会自动覆盖最老的数据
        /// </summary>
        public void Add(T item)
        {
            // 使用一个非常coooool的位运算来实现环绕，等价于 (_head + _count) % _capacity
            _buffer[(_head + _count) & _capacityMask] = item;

            // 如果还没满，直接增加计数；如果满了，就把头指针往前挪一位，覆盖最老的元素
            if (_count < _capacity) _count++;
            else _head = (_head + 1) & _capacityMask; // 同上，等价于 _head = (_head + 1) % _capacity
        }

        /// <summary>
        /// 核心魔法：索引器。
        /// 外界用起来就像 List 一样。index = 0 永远是【最老】的，index = Count - 1 永远是【最新】的。
        /// 不过注意是只读的
        /// </summary>
        public T this[int index]
        {
            get
            {
                // 其实基于位运算的索引映射可以非常coooool的使用负数访问
                // 但要灌满了才好使，所以这里忍痛作罢
                if (index < 0 || index >= _count)
                {
                    throw new IndexOutOfRangeException($"索引 {index} 超出边界 (Count: {_count})");
                }
                // 通过位运算，把线性的索引映射到环形数组上
                return _buffer[(_head + index) & _capacityMask];
            }
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }

}
