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

using System.Collections;

namespace Wjybxx.Commons.Collections;

/// <summary>
///
/// 1.按照C#的习惯，当元素不存在时默认抛出异常
/// 2.未来可扩展查询下一个key的方法
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public interface ISequencedDictionary<TKey, TValue> : IGenericDictionary<TKey, TValue> where TKey : notnull
{
    #region peek

    public KeyValuePair<TKey, TValue> FirstPair { get; }
    public TKey FirstKey { get; }
    public TValue FirstValue { get; }

    public KeyValuePair<TKey, TValue> LastPair { get; }
    public TKey LastKey { get; }
    public TValue LastValue { get; }

    /// <summary>
    /// 获取key关联的值，如果关联的值不存在，则返回预设的默认值。
    /// 1.如果字典支持自定义默认值，则返回自定义默认值。
    /// 2.如果字典不支持自定义默认值，则返回default分配的对象。
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public TValue GetOrDefault(TKey key);

    /// <summary>
    /// 获取key关联的值，如果关联的值不存在，则返回给定的默认值。
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="defVal">key不存在时的默认值</param>
    /// <returns></returns>
    public TValue GetOrDefault(TKey key, TValue defVal);

    /// <summary>
    /// 获取元素，并将元素移动到首部
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns>如果元素存在则返回true</returns>
    public bool GetAndMoveToFirst(TKey key, out TValue value);

    /// <summary>
    /// 获取元素，并将元素移动到尾部
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns>如果元素存在则返回true</returns>
    public bool GetAndMoveToLast(TKey key, out TValue value);

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

    #region remove

    /// <summary>
    /// 字典的原生接口Remove只有返回值，其实是不好的，更多的情况下我们需要返回值；但C#存在值结构，当value是值类型的时候总是返回值会导致不必要的内存分配。
    /// <see cref="Dictionary{TKey,TValue}"/>中提供了该补偿方法，但未在接口中添加。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value">接收返回值</param>
    /// <returns>是否删除成功</returns>
    public bool Remove(TKey key, out TValue value);

    /// <summary>
    /// 删除有序字典的首个键值对
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">如果字典为空</exception>
    public KeyValuePair<TKey, TValue> RemoveFirst();

    /// <summary>
    /// 尝试删除有序字典的首个键值对。
    /// 如果字典不为空，则删除首个键值对并返回true，否则返回false
    /// </summary>
    /// <param name="pair"></param>
    /// <returns>是否删除成功</returns>
    public bool TryRemoveFirst(out KeyValuePair<TKey, TValue> pair);

    /// <summary>
    /// 删除有序字典的末尾键值对
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">如果字典为空</exception>
    public KeyValuePair<TKey, TValue> RemoveLast();

    /// <summary>
    /// 尝试删除有序字典的末尾键值对。
    /// 如果字典不为空，则删除末尾键值对并返回true，否则返回false。
    /// </summary>
    /// <param name="pair"></param>
    /// <returns>是否删除成功</returns>
    public bool TryRemoveLast(out KeyValuePair<TKey, TValue> pair);

    #endregion

    #region itr

    /// <summary>
    /// 获取反向迭代器
    /// 暂时不打算定义复杂Collection接口，即使以后添加，该接口也可以作为常用的快捷方法。
    /// </summary>
    /// <returns></returns>
    IEnumerator<KeyValuePair<TKey, TValue>> GetReversedEnumerator();

    #endregion
}