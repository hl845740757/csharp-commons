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
/// 我尝试继承非泛型接口，但C#的接口规则与java不同，在接口冲突时(有相同签名的方法时)，需要重定义方法以隐藏父接口方法，会导致非常多的方法，实在让人不爽。
///
/// 1.AddRange这类接口只对List这类接口是有意义的，可连续拷贝；对于其它集合类型则不是必须的。
/// 2.对于其它类型结合，批量增删元素可以通过扩展方法实现，接口层提供触发扩容的接口便足够了。
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IGenericCollection<T> : ICollection<T>, IReadOnlyCollection<T>
{
    new int Count { get; }

    /// <summary>
    /// 调整空间
    /// 1.该接口以允许用户触发扩容，通常用于批量添加元素之前;
    /// 2.Hash结构通常有较大的辅助空间，该接口以允许用户触发收缩;
    /// 3.该接口不一定产生效用，与实现类相关（默认应该空实现，空实现是安全的方式）;
    /// 4.不进行默认实现，以免代理类忘记转发;
    /// 5.该接口可能有较大开销，应避免频繁调用;
    /// </summary>
    /// <param name="expectedCount">期望的元素数量，不是直接的空间大小，不可小于当前count</param>
    void AdjustCapacity(int expectedCount);

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