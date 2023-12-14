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

public interface ISequencedCollection<T> : IGenericCollection<T>
{
    /// <summary>
    /// 返回一个当前集合的逆序视图
    /// 对逆序视图的AddFirst、AddLast、RemoveFirst、RemoveLast等明确方向的操作将颠倒，
    /// 而普通的Add、Remove等方法将不受影响。
    /// </summary>
    /// <returns></returns>
    ISequencedCollection<T> Reversed();

    /// <summary>
    /// 查看集合的首个元素
    /// </summary>
    /// <param name="item"></param>
    /// <returns>如果集合不为空则返回true</returns>
    public bool PeekFirst(out T item);

    /// <summary>
    /// 获取集合首部元素
    /// </summary>
    /// <exception cref="InvalidOperationException">如果集合为空</exception>
    public T First { get; }

    /// <summary>
    /// 查看集合的末尾元素
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool PeekLast(out T item);

    /// <summary>
    /// 获取集合尾部元素
    /// </summary>
    /// <exception cref="InvalidOperationException">如果集合为空</exception>
    public T Last { get; }

    /// <summary>
    /// 添加元素到集合的首部
    /// </summary>
    /// <param name="item"></param>
    void AddFirst(T item);

    /// <summary>
    /// 添加元素到集合的尾部
    /// </summary>
    /// <param name="item"></param>
    void AddLast(T item);

    /// <summary>
    /// 移除首部的元素
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">如果集合为空</exception>
    T RemoveFirst();

    /// <summary>
    /// 尝试删除集合的首部元素，如果集合为空，则返回false
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    bool TryRemoveFirst(out T item);

    /// <summary>
    /// 移除尾部的元素
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">如果集合为空</exception>
    T RemoveLast();

    /// <summary>
    /// 尝试删除集合的尾部元素，如果集合为空，则返回false
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    bool TryRemoveLast(out T item);

    #region itr

    /// <summary>
    /// 获取逆序迭代器
    /// (也可通过Reversed视图拿到，这是个快捷方法)
    /// </summary>
    /// <returns></returns>
    IEnumerator<T> GetReversedEnumerator();

    /// <summary>
    /// 拷贝数据到指定数组
    /// </summary>
    /// <param name="array">目标数组</param>
    /// <param name="arrayIndex">目标数组偏移</param>
    /// <param name="reversed">是否逆序</param>
    void CopyTo(T[] array, int arrayIndex, bool reversed = false);

    void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
        CopyTo(array, arrayIndex, false);
    }

    #endregion
}