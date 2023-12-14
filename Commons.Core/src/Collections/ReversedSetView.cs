﻿#region LICENSE

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

public class ReversedSequenceSetView<TKey> : ReversedCollectionView<TKey>, ISequencedSet<TKey>
{
    public ReversedSequenceSetView(ISequencedSet<TKey> hashSet) :
        base(hashSet) {
    }

    private ISequencedSet<TKey> Delegated => (ISequencedSet<TKey>)_delegated;

    public override ISequencedSet<TKey> Reversed() {
        return (ISequencedSet<TKey>)_delegated;
    }

    public bool Add(TKey item) {
        return Delegated.Add(item); // 不颠倒顺序
    }
}