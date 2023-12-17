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
/// 1.是否支持null元素，取决于实现
/// 2.接口中不再增加特殊含义的方法名，以免方法数过多导致混淆。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IDeque<T> : IQueue<T>, IStack<T>, ISequencedCollection<T>
{
    /// <summary>
    /// 注意：Queue和栈的操作都是隐含方向的，因此相关操作会被反转。
    /// </summary>
    /// <returns></returns>
    new IDeque<T> Reversed();

    #region queue

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

    #endregion

    #region 接口适配

    ISequencedCollection<T> ISequencedCollection<T>.Reversed() {
        return Reversed();
    }

    #endregion
}