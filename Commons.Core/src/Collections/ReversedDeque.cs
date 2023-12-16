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

public class ReversedDeque<TKey> : ReversedCollectionView<TKey>, IDeque<TKey>
{
    public ReversedDeque(IDeque<TKey> hashSet) :
        base(hashSet) {
    }

    private IDeque<TKey> Delegated => (IDeque<TKey>)_delegated;

    public override IDeque<TKey> Reversed() {
        return (IDeque<TKey>)_delegated;
    }

    // Queue接口中的方法不能精确反转，允许重写

    #region queue

    public virtual void Enqueue(TKey item) {
        Delegated.Enqueue(item);
    }

    public virtual bool TryEnqueue(TKey item) {
        return Delegated.TryEnqueue(item);
    }

    public virtual TKey Dequeue() {
        return Delegated.Dequeue();
    }

    public virtual bool TryDequeue(out TKey item) {
        return Delegated.TryDequeue(out item);
    }

    public virtual bool Peek(out TKey item) {
        return Delegated.Peek(out item);
    }

    #endregion

    #region dequeue

    public bool TryAddFirst(TKey item) {
        return Delegated.TryAddLast(item);
    }

    public bool TryAddLast(TKey item) {
        return Delegated.TryAddFirst(item);
    }

    #endregion
}