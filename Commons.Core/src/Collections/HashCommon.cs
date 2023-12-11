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

using System.Runtime.CompilerServices;
using static Wjybxx.Commons.Collections.MathCommon;

namespace Wjybxx.Commons.Collections;

/// <summary>
/// 该类部分修改自Java的Fastutil集合库
/// </summary>
public static class HashCommon
{
    #region fastutil

    /** The initial default size of a hash table. */
    public const int DefaultInitialSize = 16;
    /** The default load factor of a hash table. */
    public const float DefaultLoadFactor = .75f;

    /** see https://github.com/leventov/Koloboke */
    private const uint IntPhi = 0x9E3779B9;

    /** 对原始的hash进行混淆，降低冲突概率 */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mix(int x) {
        uint h = (uint)x * IntPhi;
        return (int)(h ^ (h >> 16));
    }

    public static int MaxFill(int capacity, float factor) {
        /* We must guarantee that there is always at least
         * one free entry (even with pathological load factors). */
        return Math.Min((int)Math.Ceiling(capacity * factor), capacity - 1);
    }

    public static int ArraySize(int expected, float f) {
        long s = Math.Max(MinArraySize, NextPowerOfTwo((long)Math.Ceiling(expected / f)));
        if (s > (1 << 30)) throw new ArgumentException("Too large (" + expected + " expected elements with load factor " + f + ")");
        return (int)s;
    }

    #endregion

    /** Hash结构的最小数组大小 -- Fastutil中Hash结构的最小空间为2，我调整为4 */
    public const int MinArraySize = 4;
    /** Hash结构的最大数组大小 */
    public const int MaxArraySize = 1 << 30;

    public static void CheckLoadFactor(float loadFactor) {
        if (loadFactor <= 0 || loadFactor >= 1) {
            throw new ApplicationException("Load factor must be greater than 0 and smaller than 1");
        }
    }
}