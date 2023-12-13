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
    #region list

    public static bool ContainsRef<TE>(IList<TE> list, TE element) where TE : class {
        for (int i = 0, size = list.Count; i < size; i++) {
            if (ReferenceEquals(list[i], element)) {
                return true;
            }
        }
        return false;
    }

    public static int IndexOfRef<TE>(IList<TE> list, Object element) where TE : class {
        for (int idx = 0, size = list.Count; idx < size; idx++) {
            if (ReferenceEquals(list[idx], element)) {
                return idx;
            }
        }
        return -1;
    }

    public static int LastIndexOfRef<TE>(IList<TE> list, Object element) where TE : class {
        for (int idx = list.Count - 1; idx >= 0; idx--) {
            if (ReferenceEquals(list[idx], element)) {
                return idx;
            }
        }
        return -1;
    }

    public static bool RemoveRef<TE>(IList<TE> list, Object element) where TE : class {
        int index = IndexOfRef(list, element);
        if (index < 0) {
            return false;
        }
        list.RemoveAt(index);
        return true;
    }

    #endregion

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

    public static bool ContainsRef<TE>(TE[] list, TE element) where TE : class {
        for (int i = 0, size = list.Length; i < size; i++) {
            if (ReferenceEquals(list[i], element)) {
                return true;
            }
        }
        return false;
    }

    public static int IndexOfRef<TE>(TE[] list, Object element) where TE : class {
        for (int idx = 0, size = list.Length; idx < size; idx++) {
            if (ReferenceEquals(list[idx], element)) {
                return idx;
            }
        }
        return -1;
    }

    public static int LastIndexOfRef<TE>(TE[] list, Object element) where TE : class {
        for (int idx = list.Length - 1; idx >= 0; idx--) {
            if (ReferenceEquals(list[idx], element)) {
                return idx;
            }
        }
        return -1;
    }

    #endregion
}