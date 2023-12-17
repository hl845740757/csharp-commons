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

    #region dequeue

    public bool TryAddFirst(TKey item) {
        return Delegated.TryAddLast(item);
    }

    public bool TryAddLast(TKey item) {
        return Delegated.TryAddFirst(item);
    }

    #endregion

    // Queue和Stack接口中的方法不能精确反转，允许重写

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

    public virtual bool PeekQueue(out TKey item) {
        return Delegated.PeekQueue(out item);
    }

    #endregion

    #region stack

    public virtual void Push(TKey item) {
        Delegated.Push(item);
    }

    public virtual bool TryPush(TKey item) {
        return Delegated.TryPush(item);
    }

    public virtual TKey Pop() {
        return Delegated.Pop();
    }

    public virtual bool TryPop(out TKey item) {
        return Delegated.TryPop(out item);
    }

    public virtual bool PeekStack(out TKey item) {
        return Delegated.PeekStack(out item);
    }

    #endregion
}