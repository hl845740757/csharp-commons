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

public class ReversedDictionaryView<TKey, TValue> : ReversedCollectionView<KeyValuePair<TKey, TValue>>, ISequencedDictionary<TKey, TValue>
{
    public ReversedDictionaryView(ISequencedDictionary<TKey, TValue> delegated)
        : base(delegated) {
    }

    private ISequencedDictionary<TKey, TValue> Dictionary => (ISequencedDictionary<TKey, TValue>)_delegated;

    public ISequencedCollection<TKey> Keys => Dictionary.Keys;
    public ISequencedCollection<TValue> Values => Dictionary.Values;

    public TValue this[TKey key] {
        get => Dictionary[key];
        set => Dictionary[key] = value; // 等同add
    }

    public override ISequencedDictionary<TKey, TValue> Reversed() {
        return Dictionary;
    }

    #region get

    public TKey FirstKey => Dictionary.LastKey;
    public TKey LastKey => Dictionary.FirstKey;

    public bool PeekFirstKey(out TKey key) {
        return Dictionary.PeekLastKey(out key);
    }

    public bool PeekLastKey(out TKey key) {
        return Dictionary.PeekFirstKey(out key);
    }

    public bool TryGetValue(TKey key, out TValue value) {
        return Dictionary.TryGetValue(key, out value);
    }

    public TValue GetOrDefault(TKey key) {
        return Dictionary.GetOrDefault(key);
    }

    public TValue GetOrDefault(TKey key, TValue defVal) {
        return Dictionary.GetOrDefault(key, defVal);
    }

    public TValue GetAndMoveToFirst(TKey key) {
        return Dictionary.GetAndMoveToLast(key);
    }

    public bool TryGetAndMoveToFirst(TKey key, out TValue value) {
        return Dictionary.TryGetAndMoveToLast(key, out value);
    }

    public TValue GetAndMoveToLast(TKey key) {
        return Dictionary.GetAndMoveToFirst(key);
    }

    public bool TryGetAndMoveToLast(TKey key, out TValue value) {
        return Dictionary.TryGetAndMoveToFirst(key, out value);
    }

    public bool ContainsKey(TKey key) {
        return Dictionary.ContainsKey(key);
    }

    public bool ContainsValue(TValue value) {
        return Dictionary.ContainsValue(value);
    }

    #endregion

    #region add

    public virtual void Add(TKey key, TValue value) {
        Dictionary.Add(key, value); // add默认不修改方向，但允许重写
    }

    public virtual bool TryAdd(TKey key, TValue value) {
        return Dictionary.TryAdd(key, value);
    }

    public void AddFirst(TKey key, TValue value) {
        Dictionary.AddLast(key, value);
    }

    public bool TryAddFirst(TKey key, TValue value) {
        return Dictionary.TryAddLast(key, value);
    }

    public void AddLast(TKey key, TValue value) {
        Dictionary.AddFirst(key, value);
    }

    public bool TryAddLast(TKey key, TValue value) {
        return Dictionary.TryAddFirst(key, value);
    }

    public PutResult<TValue> PutFirst(TKey key, TValue value) {
        return Dictionary.PutLast(key, value);
    }

    public PutResult<TValue> PutLast(TKey key, TValue value) {
        return Dictionary.PutFirst(key, value);
    }

    public virtual PutResult<TValue> Put(TKey key, TValue value) {
        return Dictionary.Put(key, value); // put默认不修改方向，但允许重写
    }

    public virtual void PutRange(IEnumerable<KeyValuePair<TKey, TValue>> collection) {
        Dictionary.PutRange(collection);
    }

    #endregion

    #region remove

    public bool Remove(TKey key) {
        return Dictionary.Remove(key);
    }

    public bool Remove(TKey key, out TValue value) {
        return Dictionary.Remove(key, out value);
    }

    #endregion
}