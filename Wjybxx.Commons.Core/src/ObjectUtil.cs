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

using System;

namespace Wjybxx.Commons;

/// <summary>
/// 一些基础工具方法
/// </summary>
public static class ObjectUtil
{
    /// <summary>
    /// 如果参数为null，则抛出异常
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    public static T RequireNonNull<T>(T obj, string? message = null) {
        if (obj == null) throw new ArgumentNullException(nameof(obj), message);
        return obj;
    }

    /// <summary>
    /// 如果参数为null，则转为给定的默认值
    /// </summary>
    public static T NullToDef<T>(T obj, T def) {
        return obj == null ? def : obj;
    }

    /// <summary>
    /// 如果字符串为null或空字符串，则转为默认字符串
    /// </summary>
    /// <param name="value">value</param>
    /// <param name="def">默认值</param>
    /// <returns></returns>
    public static string EmptyToDef(string value, string def) {
        return string.IsNullOrEmpty(value) ? def : value;
    }

    /// <summary>
    /// 如果字符串为全空白字符串，则转为默认字符串
    /// </summary>
    /// <param name="value">value</param>
    /// <param name="def">默认值</param>
    /// <returns></returns>
    public static string BlankToDef(string value, string def) {
        return string.IsNullOrWhiteSpace(value) ? def : value;
    }
}