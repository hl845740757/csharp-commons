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

/// <summary>
/// 基础数学工具库
/// </summary>
public static class MathCommon
{
    /** 判断一个数是否是2的整次幂 */
    public static bool IsPowerOfTwo(int x) {
        return x > 0 && (x & (x - 1)) == 0;
    }

    /** 计算num最接近下一个整2次幂；如果自身是2的整次幂，则会返回自身 */
    public static int NextPowerOfTwo(int num) {
        if (num < 1) {
            return 1;
        }
        // https://acius2.blogspot.com/2007/11/calculating-next-power-of-2.html
        // C#未提供获取前导0数量的接口，因此我们选用该算法
        // 先减1，兼容自身已经是2的整次幂的情况；然后通过移位使得后续bit全部为1，再加1即获得结果
        num--;
        num = (num >> 1) | num;
        num = (num >> 2) | num;
        num = (num >> 4) | num;
        num = (num >> 8) | num;
        num = (num >> 16) | num;
        return ++num;
    }

    public static long NextPowerOfTwo(long num) {
        if (num < 1) {
            return 1;
        }
        num--;
        num = (num >> 1) | num;
        num = (num >> 2) | num;
        num = (num >> 4) | num;
        num = (num >> 8) | num;
        num = (num >> 16) | num;
        num = (num >> 32) | num;
        return ++num;
    }
}