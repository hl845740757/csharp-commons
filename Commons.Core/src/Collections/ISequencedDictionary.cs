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

namespace Wjybxx.Commons.Collections;

public interface ISequencedDictionary<TKey, TValue> : IGenericDictionary<TKey, TValue>,
    ISequencedCollection<KeyValuePair<TKey, TValue>>
{
    new ISequencedDictionary<TKey, TValue> Reversed();

    /// <summary>
    /// 获取不安全的key集合视图，对key的删除将作用于原始的字典
    /// </summary>
    /// <param name="reversed">是否反转</param>
    /// <returns></returns>
    ISequencedCollection<TKey> UnsafeKeys(bool reversed = false);

    #region get

    new ISequencedCollection<TKey> Keys { get; }
    new ISequencedCollection<TValue> Values { get; }

    public TKey FirstKey { get; }

    public TKey LastKey { get; }

    public bool PeekFirstKey(out TKey key);

    public bool PeekLastKey(out TKey key);

    #endregion

    #region add

    /// <summary>
    /// 添加键值对到字典的首部。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <exception cref="InvalidOperationException">如果key已经存在</exception>
    public void AddFirst(TKey key, TValue value);

    /// <summary>
    /// 如果key不存在则添加成功并返回true，否则返回false
    /// </summary>
    /// <returns>是否添加成功</returns>
    public bool TryAddFirst(TKey key, TValue value);

    /// <summary>
    /// 添加键值对到字典的尾部。
    /// 一般情况下等同于调用<code>Add(key, value)</code>
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">如果key已经存在</exception>
    public void AddLast(TKey key, TValue value);

    /// <summary>
    /// 如果key不存在则添加成功并返回true，否则返回false。
    /// </summary>
    /// <returns>是否添加成功</returns>
    public bool TryAddLast(TKey key, TValue value);

    /// <summary>
    /// 如果key存在则覆盖，并移动到首部；如果key不存在，则插入到首部
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public PutResult<TValue> PutFirst(TKey key, TValue value);

    /// <summary>
    /// 如果key存在则覆盖，并移动到末尾；如果key不存在，则插入到末尾
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public PutResult<TValue> PutLast(TKey key, TValue value);

    #endregion

    #region 接口适配

    IGenericCollection<TKey> IGenericDictionary<TKey, TValue>.Keys => Keys;
    IGenericCollection<TValue> IGenericDictionary<TKey, TValue>.Values => Values;
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
    ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

    ISequencedCollection<KeyValuePair<TKey, TValue>> ISequencedCollection<KeyValuePair<TKey, TValue>>.Reversed() {
        return Reversed();
    }

    IGenericCollection<TKey> IGenericDictionary<TKey, TValue>.UnsafeKeys() {
        return UnsafeKeys();
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) {
        Add(item.Key, item.Value);
    }

    void ISequencedCollection<KeyValuePair<TKey, TValue>>.AddFirst(KeyValuePair<TKey, TValue> item) {
        AddFirst(item.Key, item.Value);
    }

    void ISequencedCollection<KeyValuePair<TKey, TValue>>.AddLast(KeyValuePair<TKey, TValue> item) {
        AddLast(item.Key, item.Value);
    }

    #endregion
}