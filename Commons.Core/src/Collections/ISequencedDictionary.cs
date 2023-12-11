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

/// <summary>
/// 按照C#的习惯，当元素不存在时默认抛出异常
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public interface ISequencedDictionary<TKey, TValue> : IDictionary<TKey, TValue> where TKey : notnull
{
    #region peek

    public KeyValuePair<TKey, TValue> FirstPair { get; }
    public TKey FirstKey { get; }
    public TValue FirstValue { get; }

    public KeyValuePair<TKey, TValue> LastPair { get; }
    public TKey LastKey { get; }
    public TValue LastValue { get; }

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

    #endregion

    #region remove

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
}