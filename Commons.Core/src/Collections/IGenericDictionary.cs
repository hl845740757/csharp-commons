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

using System.Runtime.CompilerServices;

namespace Wjybxx.Commons.Collections;

public interface IGenericDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>,
    IGenericCollection<KeyValuePair<TKey, TValue>>
{
    new TValue this[TKey key] { get; set; }
    new IGenericCollection<TKey> Keys { get; }
    new IGenericCollection<TValue> Values { get; }

    new bool TryGetValue(TKey key, out TValue value);

    new bool ContainsKey(TKey key);

    /// <summary>
    /// 是否包含给定的Value
    /// (看似IDictionary没定义此接口，实际上却必须要实现，因为Values集合要实现Contains)
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    bool ContainsValue(TValue value);

    /// <summary>
    /// 如果key不存在，则插入键值对并发那会true，否则返回false
    /// </summary>
    /// <returns>插入成功则返回true</returns>
    bool TryAdd(TKey key, TValue value);

    /// <summary>
    /// 与Add不同，Put操作在Key存在值，总是覆盖当前关联值，而不是抛出异常
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    PutResult<TValue> Put(TKey key, TValue value);

    /// <summary>
    /// 字典的原生接口Remove只返回bool值，而更多的情况下我们需要返回值；但C#存在值结构，当value是值类型的时候总是返回值会导致不必要的内存分配。
    /// <see cref="Dictionary{TKey,TValue}"/>中提供了该补偿方法，但未在接口中添加。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value">接收返回值</param>
    /// <returns>是否删除成功</returns>
    public bool Remove(TKey key, out TValue value);

    #region 接口适配

    // 泛型接口建议实现类再显式实现，因为转换为接口的情况较多，可减少转发
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
    ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

    bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => ContainsKey(key);

    bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key) => ContainsKey(key);

    TValue IDictionary<TKey, TValue>.this[TKey key] {
        get => this[key];
        set => this[key] = value;
    }

    TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] {
        get => this[key];
    }

    #endregion

    #region util

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsCompatibleKey(object key) {
        return key is TKey;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsCompatibleValue(object value) {
        return value is TValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TKey EnsureCompatibleKey(object key) {
        if (key is not TKey key2) throw new ArgumentException("Incompatible key");
        return key2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TValue EnsureCompatibleValue(object? value) {
        if (value is not TValue value2) throw new ArgumentException("Incompatible value");
        return value2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void EnsureCompatible(object key, object? value) {
        if (key is not TKey) throw new ArgumentException("Incompatible key");
        if (value is not TValue) throw new ArgumentException("Incompatible value");
    }

    #endregion
}