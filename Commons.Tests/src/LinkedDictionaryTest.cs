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

using NUnit.Framework;
using Wjybxx.Commons.Collections;

namespace Commons.Tests;

public class LinkedDictionaryTest
{
    [Test]
    public void TestDic() {
        HashSet<int> keySet = new HashSet<int>(10240);
        List<int> keyList = new List<int>(10240);
        LinkedDictionary<int, string> dictionary = new LinkedDictionary<int, string>(10240);

        while (keySet.Count < 10000) {
            if (Random.Shared.Next(0, 2) == 1 && keyList.Count > 1000) {
                int idx = Random.Shared.Next(0, keyList.Count);
                int key = keyList[idx];
                keyList.RemoveAt(idx);
                keySet.Remove(key);
                dictionary.Remove(key, out _);
                continue;
            }
            var next = Random.Shared.Next(0, 200000);
            if (keySet.Add(next)) {
                keyList.Add(next);
                dictionary[next] = next.ToString();
            }
        }
        Assert.AreEqual(keyList.Count, dictionary.Count);
        int index = 0;
        foreach (KeyValuePair<int, string> pair in dictionary) {
            int expectedKey = keyList[index++];
            int realKey = pair.Key;
            Assert.AreEqual(expectedKey, realKey);
        }
    }
}