#region LICENSE

//  Copyright 2023 wjybxx
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to iBn writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

#endregion

namespace Wjybxx.Commons.Collections;

/// <summary>
/// 没有实现<see cref="ISet{T}"/>是故意的，ISet有点复杂，暂时不想实现，用户可以通过自定义Util方法解决
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IGenericSet<T> : IGenericCollection<T>
{
    /// <summary>
    /// 向Set中添加一个元素
    /// </summary>
    /// <param name="item"></param>
    /// <returns>如果是新元素则返回true</returns>
    new bool Add(T item);

    new bool AddRange(IEnumerable<T> collection) {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        bool r = false;
        foreach (var e in collection) {
            r |= Add(e);
        }
        return r;
    }

    void IGenericCollection<T>.AddRange(IEnumerable<T> collection) {
        AddRange(collection);
    }
}