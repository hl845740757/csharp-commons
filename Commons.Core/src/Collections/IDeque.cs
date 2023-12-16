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
/// 双端队列
/// 是否支持null元素，取决于实现
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IDeque<T> : IQueue<T>, ISequencedCollection<T>
{
    /// <summary>
    /// 与<see cref="ISequencedCollection{T}"/>接口中提到的约束相同，
    /// 只有明确方法的操作会被明确反转，而不确定方向的操作其方向是不确定的。
    /// 也就是说：<see cref="IQueue{T}"/>接口的中方法在反转后是不确定的，应避免调用。
    /// </summary>
    /// <returns></returns>
    new IDeque<T> Reversed();

    /// <summary>
    /// 尝试添加元素到队首
    /// </summary>
    /// <param name="item"></param>
    /// <returns>插入成功则返回true，否则返回false</returns>
    bool TryAddFirst(T item);

    /// <summary>
    /// 尝试添加元素到队尾
    /// </summary>
    /// <param name="item"></param>
    /// <returns>插入成功则返回true，否则返回false</returns>
    bool TryAddLast(T item);

    ISequencedCollection<T> ISequencedCollection<T>.Reversed() {
        return Reversed();
    }
}