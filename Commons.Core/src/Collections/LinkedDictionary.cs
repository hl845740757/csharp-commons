#region LICENSE

// Copyright 2023 wjybxx(845740757@qq.com)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion


using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Wjybxx.Commons.Collections;

/// <summary>
/// 保持插入序的字典
/// 1.使用简单的线性探测法解决Hash冲突，因此在数据量较大的情况下查询性能会降低 -- 不想写得太复杂。
/// 2.非线程安全。
///
/// 吐槽：C#的基础库里居然没有保持插入序的高性能字典，这对于编写底层工具的开发者来说太不方便了。
/// 
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class LinkedDictionary<TKey, TValue> : ISequencedDictionary<TKey, TValue> where TKey : notnull
{
    /** 总是延迟分配空间，以减少创建Dictionary的开销 */
    private Node?[]? _table; // 这个NullableReference有时真的很烦
    private Node? _head;
    private Node? _tail;

    /** 有效元素数量 */
    private int _count;
    /** 版本号 -- 发生结构性变化的时候增加，即增加和删除元素的时候；替换Key的Value不增加版本号 */
    private int _version;

    /** 计算下标使用的掩码，不依赖数组长度，避免未来调整时破坏太大 */
    private int _mask;
    /** 负载因子 */
    private float _loadFactor;
    /** count触发扩容的边界值 */
    private int _maxFill;

    /** 用于代替key自身的equals和hashcode计算；这一点C#的设计做的要好些 */
    private IEqualityComparer<TKey> _keyComparer;

    public LinkedDictionary()
        : this(0, HashCommon.DefaultLoadFactor) {
    }

    public LinkedDictionary(IEqualityComparer<TKey> comparer)
        : this(0, HashCommon.DefaultLoadFactor, comparer) {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="expectedCount">期望存储的元素个数，而不是直接的容量</param>
    /// <param name="loadFactor">有效负载因子</param>
    /// <param name="keyComparer">可用于避免Key比较时装箱</param>
    public LinkedDictionary(int expectedCount, float loadFactor = 0.75f,
                            IEqualityComparer<TKey>? keyComparer = null) {
        if (expectedCount < 0) throw new ArgumentException("The expected number of elements must be nonnegative");
        HashCommon.CheckLoadFactor(loadFactor);
        _loadFactor = loadFactor;
        _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;

        if (expectedCount == 0) {
            expectedCount = HashCommon.DefaultInitialSize;
        }
        int arraySize = HashCommon.ArraySize(expectedCount, loadFactor);
        _mask = arraySize - 1;
        _maxFill = HashCommon.MaxFill(arraySize, loadFactor);
    }

    public int Count => _count;

    public bool IsReadOnly = false;

    public TValue this[TKey key] {
        get {
            Node? node = GetNode(key);
            return node == null ? default : node._value;
        }
        set => TryInsert(key, value, InsertionOrder.Tail, InsertionBehavior.OverwriteExisting);
    }

    #region contains

    public bool ContainsKey(TKey key) {
        return Find(key) >= 0;
    }

    public bool ContainsValue(TValue value) {
        if (value == null) {
            for (Node e = _head; e != null; e = e._after) {
                if (e._value == null) {
                    return true;
                }
            }
            return false;
        }
        else {
            IEqualityComparer<TValue>? valComparer = ValComparer;
            for (Node e = _head; e != null; e = e._after) {
                if (valComparer.Equals(e._value, value)) {
                    return true;
                }
            }
            return false;
        }
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
        Node node = GetNode(item.Key);
        return node != null && ValComparer.Equals(node._value, item.Value);
    }

    private IEqualityComparer<TValue> ValComparer => EqualityComparer<TValue>.Default;

    #endregion

    #region first/last

    public KeyValuePair<TKey, TValue> FirstPair {
        get {
            if (_head == null) throw DictionaryEmptyException();
            return _head.AsPair();
        }
    }

    public TKey FirstKey {
        get {
            if (_head == null) throw DictionaryEmptyException();
            return _head._key;
        }
    }

    public TValue FirstValue {
        get {
            if (_head == null) throw DictionaryEmptyException();
            return _head._value;
        }
    }

    public KeyValuePair<TKey, TValue> LastPair {
        get {
            if (_tail == null) throw DictionaryEmptyException();
            return _tail.AsPair();
        }
    }

    public TKey LastKey {
        get {
            if (_tail == null) throw DictionaryEmptyException();
            return _tail._key;
        }
    }

    public TValue LastValue {
        get {
            if (_tail == null) throw DictionaryEmptyException();
            return _tail._value;
        }
    }

    private static InvalidOperationException DictionaryEmptyException() {
        return new InvalidOperationException("Dictionary is Empty");
    }

    #endregion

    #region get

    public bool TryGetValue(TKey key, out TValue value) {
        var node = GetNode(key);
        if (node == null) {
            value = default;
            return false;
        }
        value = node._value;
        return true;
    }

    /// <summary>
    /// 获取key关联的值，如果关联的值不存在，则返回给定的默认值
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="defVal">key不存在时的默认值</param>
    /// <returns></returns>
    public TValue GetOrDefault(TKey key, TValue defVal) {
        var node = GetNode(key);
        return node == null ? defVal : node._value;
    }

    /// <summary>
    /// 如果key存在，则返回对应的下标(大于等于0)；
    /// 如果key不存在，则返回其hash应该存储的下标的负值再减1，以识别0 -- 或者说 下标 +1 再取反
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private int Find(TKey key) {
        Node[] table = _table;
        if (table == null) {
            table = _table = new Node[_mask + 1];
        }
        int hash = HashCommon.Mix(_keyComparer.GetHashCode(key));
        int pos = _mask & hash;
        Node node = table[pos];
        if (node == null) return -(pos + 1);
        if (node._hash == hash && _keyComparer.Equals(node._key, key)) {
            return pos;
        }
        // 线性探测
        // 注意：为了利用空间，线性探测需要在越界时绕回到数组首部(mask取余绕回)
        // 由于有效内容总是小于数组长度，可保证这里一定有一个槽为null，从而不会死循环 -- 数组满了一定会扩容
        Debug.Assert(_count <= _mask);
        while (true) {
            pos = (pos + 1) & _mask;
            node = table[pos];
            if (node == null) return -(pos + 1);
            if (node._hash == hash && _keyComparer.Equals(node._key, key)) {
                return pos;
            }
        }
    }

    private Node? GetNode(TKey key) {
        Node[] table = _table;
        if (table == null || _count == 0) {
            return null;
        }
        int hash = HashCommon.Mix(_keyComparer.GetHashCode(key));
        int pos = _mask & hash;
        Node node = table[pos];
        if (node == null || (node._hash == hash && _keyComparer.Equals(node._key, key))) {
            return node;
        }
        while (true) {
            pos = (pos + 1) & _mask;
            node = table[pos];
            if (node == null || (node._hash == hash && _keyComparer.Equals(node._key, key))) {
                return node;
            }
        }
    }

    #endregion


    #region add

    public void Add(KeyValuePair<TKey, TValue> item) {
        Add(item.Key, item.Value);
    }

    /// <summary>
    /// 如果key已经存在，则抛出异常
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentException">如果key已经存在</exception>
    public void Add(TKey key, TValue value) {
        bool modified = TryInsert(key, value, InsertionOrder.Tail, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified);
    }

    /// <summary>
    /// 如果key不存在则添加成功并返回true，否则返回false
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns>是否添加成功</returns>
    public bool TryAdd(TKey key, TValue value) {
        return TryInsert(key, value, InsertionOrder.Tail, InsertionBehavior.None);
    }

    public void AddFirst(TKey key, TValue value) {
        bool modified = TryInsert(key, value, InsertionOrder.Head, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified);
    }


    public bool TryAddFirst(TKey key, TValue value) {
        return TryInsert(key, value, InsertionOrder.Head, InsertionBehavior.None);
    }


    const int PUT_NORM = 0;
    const int PUT_FIRST = 1;
    const int PUT_LAST = 2;

    private int putMode = PUT_NORM;

    private bool TryInsert(TKey key, TValue value, InsertionOrder order, InsertionBehavior behavior) {
    }

    #endregion

    #region remove

    public bool Remove(KeyValuePair<TKey, TValue> item) {
        var node = GetNode(item.Key);
        if (node != null && ValComparer.Equals(node._value, item.Value)) {
            return Remove(item.Key);
        }
        return false;
    }

    public bool Remove(TKey key) {
        throw new NotImplementedException();
    }

    public void Clear() {
        _version++; // 以防子类无权更新
        int count = _count;
        if (count > 0 && _table != null) {
            _count = 0;
            _head = _tail = null;
            Array.Clear(_table);
        }
    }

    #endregion


    #region Node

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int KeyHash(TKey key) {
        return HashCommon.Mix(_keyComparer.GetHashCode(key)); // 降低hash冲突
    }

    private class Node : IEquatable<Node>
    {
        internal readonly int _hash;
        /** 由于Key的hash使用频率极高，因此需要缓存 */
        internal readonly TKey _key;
        internal TValue? _value;

        /** 前驱和后继 */
        internal Node? _before;
        internal Node? _after;

        public Node(int hash, TKey key, TValue value) {
            _hash = hash;
            _key = key;
            _value = value;
        }

        public TValue SetValue(TValue newValue) {
            TValue oldValue = _value;
            _value = newValue;
            return oldValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValuePair<TKey, TValue> AsPair() {
            return new KeyValuePair<TKey, TValue>(_key, _value);
        }

        public override int GetHashCode() {
            return _hash; // 不使用value计算hash，因为value可能在中途变更
        }

        #region equals

        public bool Equals(Node? other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TKey>.Default.Equals(_key, other._key) && EqualityComparer<TValue>.Default.Equals(_value, other._value);
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Node)obj);
        }

        public static bool operator ==(Node? left, Node? right) {
            return Equals(left, right);
        }

        public static bool operator !=(Node? left, Node? right) {
            return !Equals(left, right);
        }

        public override string ToString() {
            return $"{nameof(_key)}: {_key}, {nameof(_value)}: {_value}";
        }

        #endregion
    }

    #endregion
}