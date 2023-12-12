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
/// C#在提供了泛型实现后，集合和字典的接口键值一团乱麻
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public interface IGenericDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary
{
    new IGenericCollection<TKey> Keys { get; }
    new IGenericCollection<TValue> Values { get; }

    #region 接口适配

    // 不建议子类再实现这些接口

    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
    ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

    ICollection IDictionary.Keys {
        get {
            IGenericDictionary<TKey, TValue> castDic = this;
            return castDic.Keys;
        }
    }
    ICollection IDictionary.Values {
        get {
            IGenericDictionary<TKey, TValue> castDic = this;
            return castDic.Values;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() {
        IDictionary<TKey, TValue> castDic = this;
        return castDic.GetEnumerator();
    }

    /** 默认实现将不支持修改key和value */
    IDictionaryEnumerator IDictionary.GetEnumerator() {
        IDictionary<TKey, TValue> castDic = this;
        return new DictionaryEnumeratorAdapter<TKey, TValue>(castDic.GetEnumerator());
    }

    void IDictionary.Add(object key, object? value) {
        if (key is TKey key2 && value is TValue value2) {
            Add(key2, value2);
        }
        else {
            throw new ArgumentException("Incompatible key or value");
        }
    }

    bool IDictionary.Contains(object key) {
        return key is TKey key2 && Contains(key2);
    }

    void IDictionary.Remove(object key) {
        if (key is TKey key2) {
            Remove(key2);
        }
    }

    object? IDictionary.this[object key] {
        get {
            if (key is TKey key2) {
                return this[key2];
            }
            return null;
        }
        set {
            if (key is TKey key2 && value is TValue value2) {
                this[key2] = value2;
            }
            else {
                throw new ArgumentException("Incompatible key or value");
            }
        }
    }

    #endregion

    #region util

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsCompatibleKey(object key) {
        return key is TKey;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsCompatibleValue(object value) {
        return value is TValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TKey EnsureCompatibleKey(object key) {
        if (key is not TKey key2) throw new ArgumentException("Incompatible key");
        return key2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TValue EnsureCompatibleValue(object? value) {
        if (value is not TValue value2) throw new ArgumentException("Incompatible value");
        return value2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void EnsureCompatible(object key, object? value) {
        if (key is not TKey) throw new ArgumentException("Incompatible key");
        if (value is not TValue) throw new ArgumentException("Incompatible value");
    }

    #endregion
}