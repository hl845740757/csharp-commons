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
using System.Runtime.CompilerServices;

namespace Wjybxx.Commons.Collections;

/// <summary>
/// C#在提供了泛型实现后，集合和字典的接口简直一团乱麻；
/// 我尝试继承非泛型接口，但C#的接口规则与java不同，在接口冲突时(有相同签名的方法时)，需要重定义方法以隐藏父接口方法，
/// 会导致非常多的方法，实在让人不爽。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IGenericCollection<T> : ICollection<T>, IReadOnlyCollection<T>
{
    public new int Count { get; }

    /// <summary>
    /// 批量添加元素
    /// 1.该接口层面不要求原子性，子类实现可以有更强的约束。
    /// 2.C#的原始Add接口是没有返回值的，因此这里也无法设定返回值。
    /// </summary>
    /// <param name="collection"></param>
    void AddRange(IEnumerable<T> collection) {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        foreach (var e in collection) {
            Add(e);
        }
    }

    #region 接口适配

    int ICollection<T>.Count => Count;
    int IReadOnlyCollection<T>.Count => Count;

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    #endregion

    #region util

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsCompatible(object value) {
        return value is T;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static T EnsureCompatible(object value) {
        if (value is not T value2) throw new ArgumentException("Incompatible value");
        return value2;
    }

    #endregion
}