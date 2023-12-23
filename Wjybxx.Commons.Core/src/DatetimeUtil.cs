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

namespace Wjybxx.Commons;

/// <summary>
/// 日期时间工具类
/// </summary>
public static class DatetimeUtil
{
    /** 1毫秒对应的tick数 */
    public const long TicksPerMillisecond = 10000;
    /** 1秒对应的tick数 */
    public const long TicksPerSecond = TicksPerMillisecond * 1000;

    /// <summary>
    /// 转unix秒时间戳
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static long ToEpochSeconds(DateTime dateTime) {
        return (long)dateTime.Subtract(DateTime.UnixEpoch).TotalSeconds;
    }

    /// <summary>
    /// 转Unix毫秒时间戳
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static long ToEpochMillis(DateTime dateTime) {
        return (long)dateTime.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
    }
}