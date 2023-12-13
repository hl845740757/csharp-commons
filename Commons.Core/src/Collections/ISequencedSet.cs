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
/// AddFirst在元素已存在时将移动到Set的首部
/// AddLast在元素已存在时将移动到Set的尾部
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ISequencedSet<T> : ISequencedCollection<T>, IGenericSet<T>
{
    /// <summary>
    /// 返回一个当前集合的逆序视图
    /// </summary>
    /// <returns></returns>
    new ISequencedSet<T> Reversed();

    #region 接口适配

    ISequencedCollection<T> ISequencedCollection<T>.Reversed() {
        return Reversed();
    }

    #endregion
}