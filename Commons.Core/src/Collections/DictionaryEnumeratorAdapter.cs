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

namespace Wjybxx.Commons.Collections;

public class DictionaryEnumeratorAdapter<TKey, TValue> : IDictionaryEnumerator
{
    private readonly IEnumerator<KeyValuePair<TKey, TValue>> _enumerator;

    public DictionaryEnumeratorAdapter(IEnumerator<KeyValuePair<TKey, TValue>> enumerator) {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
    }

    public bool MoveNext() {
        return _enumerator.MoveNext();
    }

    public void Reset() {
        _enumerator.Reset();
    }

    public object Current => _enumerator.Current;
    [Obsolete("不支持修改元素")] public DictionaryEntry Entry => new(_enumerator.Current.Key, _enumerator.Current.Value);
    public object Key => _enumerator.Current.Key;
    public object? Value => _enumerator.Current.Value;
}