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

public static class CollectionUtil
{
    public static bool AllowDiscardHead(this DequeOverflowBehavior behavior) {
        return behavior == DequeOverflowBehavior.CircleBuffer
               || behavior == DequeOverflowBehavior.DiscardHead;
    }

    public static bool AllowDiscardTail(this DequeOverflowBehavior behavior) {
        return behavior == DequeOverflowBehavior.CircleBuffer
               || behavior == DequeOverflowBehavior.DiscardTail;
    }

    #region 数组

    /// <summary>
    /// 拷贝数组
    /// </summary>
    /// <param name="src">原始四组</param>
    /// <param name="newLen">可大于或小于原始数组长度</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T[] CopyOf<T>(T[] src, int newLen) {
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (newLen < 0) throw new ArgumentException("newLen cant be negative");
        T[] result = new T[newLen];
        Array.Copy(src, 0, result, 0, Math.Min(src.Length, newLen));
        return result;
    }

    public static bool ContainsRef<T>(T[] list, T element) where T : class {
        for (int i = 0, size = list.Length; i < size; i++) {
            if (ReferenceEquals(list[i], element)) {
                return true;
            }
        }
        return false;
    }

    public static int IndexOfRef<T>(T[] list, Object element) where T : class {
        for (int idx = 0, size = list.Length; idx < size; idx++) {
            if (ReferenceEquals(list[idx], element)) {
                return idx;
            }
        }
        return -1;
    }

    public static int LastIndexOfRef<T>(T[] list, Object element) where T : class {
        for (int idx = list.Length - 1; idx >= 0; idx--) {
            if (ReferenceEquals(list[idx], element)) {
                return idx;
            }
        }
        return -1;
    }

    #endregion

    #region list

    public static bool ContainsRef<T>(IList<T> list, T element) where T : class {
        for (int i = 0, size = list.Count; i < size; i++) {
            if (ReferenceEquals(list[i], element)) {
                return true;
            }
        }
        return false;
    }

    public static int IndexOfRef<T>(IList<T> list, Object element) where T : class {
        for (int idx = 0, size = list.Count; idx < size; idx++) {
            if (ReferenceEquals(list[idx], element)) {
                return idx;
            }
        }
        return -1;
    }

    public static int LastIndexOfRef<T>(IList<T> list, Object element) where T : class {
        for (int idx = list.Count - 1; idx >= 0; idx--) {
            if (ReferenceEquals(list[idx], element)) {
                return idx;
            }
        }
        return -1;
    }

    public static bool RemoveRef<T>(IList<T> list, Object element) where T : class {
        int index = IndexOfRef(list, element);
        if (index < 0) {
            return false;
        }
        list.RemoveAt(index);
        return true;
    }

    #endregion

    #region collection

    /// <summary>
    /// 批量Add元素
    /// </summary>
    public static void AddAll<T>(this ICollection<T> self, IEnumerable<T> items) {
        if (self == null) throw new ArgumentNullException(nameof(self));
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (self is IGenericCollection<T> generic && items is ICollection<T> otherCollection) {
            generic.AdjustCapacity(generic.Count + otherCollection.Count);
        }
        foreach (T item in items) {
            self.Add(item);
        }
    }

    /// <summary>
    /// 批量Add元素
    /// </summary>
    public static void AddAll<T>(this IGenericCollection<T> self, IEnumerable<T> items) {
        if (self == null) throw new ArgumentNullException(nameof(self));
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (items is ICollection<T> otherCollection) {
            self.AdjustCapacity(self.Count + otherCollection.Count);
        }
        foreach (T item in items) {
            self.Add(item);
        }
    }

    /// <summary>
    /// 批量删除元素
    /// </summary>
    /// <returns>删除的元素个数</returns>
    public static int RemoveAll<T>(this ICollection<T> self, IEnumerable<T> items) {
        if (self == null) throw new ArgumentNullException(nameof(self));
        if (items == null) throw new ArgumentNullException(nameof(items));
        int r = 0;
        foreach (T key in items) {
            if (self.Remove(key)) r++;
        }
        return r;
    }

    /// <summary>
    /// 删除不在保留集合中的元素
    /// （C#的迭代器不支持迭代时删除，因此只能先收集key；由于开销可能较大，不定义为扩展方法，需要显式调用）
    /// </summary>
    /// <param name="self">需要操作的集合</param>
    /// <param name="retainItems">需要保留的元素</param>
    /// <typeparam name="T"></typeparam>
    public static void RetainAll<T>(ICollection<T> self, ICollection<T> retainItems) {
        if (self == null) throw new ArgumentNullException(nameof(self));
        if (retainItems == null) throw new ArgumentNullException(nameof(retainItems));
        IEnumerator<T> itr = self.GetEnumerator();
        if (itr is IUnsafeIterator<T> unsafeItr) {
            while (unsafeItr.MoveNext()) {
                if (!retainItems.Contains(unsafeItr.Current)) {
                    unsafeItr.Remove();
                }
            }
        }
        else {
            List<T> needRemoveKeys = new List<T>();
            while (itr.MoveNext()) {
                if (!retainItems.Contains(itr.Current)) {
                    needRemoveKeys.Add(itr.Current);
                }
            }
            for (var i = 0; i < needRemoveKeys.Count; i++) {
                self.Remove(needRemoveKeys[i]);
            }
        }
    }

    #endregion

    #region Set

    /// <summary>
    /// 批量Add元素
    /// </summary>
    /// <returns>新插入的元素个数</returns>
    public static int AddAll<T>(this ISet<T> self, IEnumerable<T> items) {
        if (self == null) throw new ArgumentNullException(nameof(self));
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (self is IGenericCollection<T> generic && items is ICollection<T> otherCollection) {
            generic.AdjustCapacity(self.Count + otherCollection.Count);
        }
        int r = 0;
        foreach (T item in items) {
            if (self.Add(item)) r++;
        }
        return r;
    }

    /// <summary>
    /// 批量Add元素
    /// (没有继承ISet接口，因此需要独立的扩展方法)
    /// </summary>
    /// <returns>新插入的元素个数</returns>
    public static int AddAll<T>(this IGenericSet<T> self, IEnumerable<T> items) {
        if (self == null) throw new ArgumentNullException(nameof(self));
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (items is ICollection<T> otherCollection) {
            self.AdjustCapacity(self.Count + otherCollection.Count);
        }
        int r = 0;
        foreach (T item in items) {
            if (self.Add(item)) r++;
        }
        return r;
    }

    #endregion

    #region dictionary

    /// <summary>
    /// 批量添加元素 -- 如果Key已存在，则覆盖
    /// </summary>
    public static void PutAll<TKey, TValue>(this IGenericDictionary<TKey, TValue> self, IEnumerable<KeyValuePair<TKey, TValue>> pairs) {
        if (self == null) throw new ArgumentNullException(nameof(self));
        if (pairs == null) throw new ArgumentNullException(nameof(pairs));
        if (pairs is ICollection<KeyValuePair<TKey, TValue>> collection) {
            self.AdjustCapacity(self.Count + collection.Count);
        }
        foreach (KeyValuePair<TKey, TValue> pair in pairs) {
            self.Put(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// 获取key关联的值，如果关联的值不存在，则返回给定的默认值。
    /// </summary>
    public static TValue GetValueOrDefault<TKey, TValue>(this IGenericDictionary<TKey, TValue> self, TKey key, TValue defValue) {
        if (self == null) throw new ArgumentNullException(nameof(self));
        return self.TryGetValue(key, out TValue value) ? value : defValue;
    }

    #endregion

    #region internal

    internal static InvalidOperationException CollectionFullException() {
        return new InvalidOperationException("Collection is full");
    }

    internal static InvalidOperationException CollectionEmptyException() {
        return new InvalidOperationException("Collection is Empty");
    }

    internal static KeyNotFoundException KeyNotFoundException(object? key) {
        return new KeyNotFoundException(key == null ? "null" : key.ToString());
    }

    #endregion
}