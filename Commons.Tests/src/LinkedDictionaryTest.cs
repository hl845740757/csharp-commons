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

using System.Runtime.Serialization.Formatters.Binary;
using NUnit.Framework;
using Wjybxx.Commons.Collections;

namespace Commons.Tests;

public class LinkedDictionaryTest
{
    [Test]
    [Repeat(5)]
    public void TestIntDic() {
        int expectedCount = 10000;
        HashSet<int> keySet = new HashSet<int>(expectedCount);
        List<int> keyList = new List<int>(expectedCount);
        LinkedDictionary<int, string> dictionary = new LinkedDictionary<int, string>(expectedCount / 3); // 顺便测试扩容

        // 在插入期间随机删除已存在的key；不宜太频繁，否则keyList的移动开销太大
        while (keySet.Count < expectedCount) {
            if (Random.Shared.Next(0, 10) == 1 && keyList.Count > expectedCount / 3) {
                int idx = Random.Shared.Next(0, keyList.Count);
                int key = keyList[idx];
                keyList.RemoveAt(idx);
                keySet.Remove(key);
                dictionary.Remove(key, out _);
                continue;
            }
            var next = Random.Shared.Next();
            if (keySet.Add(next)) {
                keyList.Add(next);
                dictionary[next] = next.ToString();
            }
        }
        Assert.AreEqual(keyList.Count, keySet.Count);
        Assert.AreEqual(keyList.Count, dictionary.Count);

        int index = 0;
        foreach (KeyValuePair<int, string> pair in dictionary) {
            int expectedKey = keyList[index++];
            int realKey = pair.Key;
            Assert.AreEqual(expectedKey, realKey);
        }
    }

    [Test]
    [Repeat(5)]
    public void TestStringDic1() {
        TestStringDic(10000);
    }

    [Test]
    [Repeat(5)]
    public void TestStringDic2() {
        TestStringDic(100000);
    }

    private static LinkedDictionary<string, string> TestStringDic(int expectedCount) {
        List<string> keyList = new List<string>(expectedCount);
        LinkedDictionary<string, string> dictionary = new LinkedDictionary<string, string>(expectedCount / 3); // 顺便测试扩容

        var buffer = new byte[12];
        while (dictionary.Count < expectedCount) {
            Random.Shared.NextBytes(buffer);
            string next = Convert.ToHexString(buffer);
            if (dictionary.TryAdd(next, next)) {
                keyList.Add(next);
            }
        }
        Assert.AreEqual(keyList.Count, dictionary.Count);

        int index = 0;
        foreach (KeyValuePair<string, string> pair in dictionary) {
            var expectedKey = keyList[index++];
            var realKey = pair.Key;
            if (expectedKey != realKey) {
                throw new InvalidOperationException($"expectedKey:{expectedKey} == realKey:{realKey}");
            }
        }
        return dictionary;
    }

    [Test]
    public void TestAdjustCapacity() {
        LinkedDictionary<string, string> dictionary = TestStringDic(10000);
        dictionary.AdjustCapacity(15000);
        dictionary.AdjustCapacity(10001);
        dictionary.AdjustCapacity(10000);
    }

    /** 序列化测试 */
    [Test]
    public void SerialTest() {
        LinkedDictionary<string, string> dictionary = TestStringDic(1000);
        BinaryFormatter formatter = new BinaryFormatter();
        MemoryStream stream = new MemoryStream(new byte[64 * 1024]);
        formatter.Serialize(stream, dictionary);

        stream.Position = 0;
        LinkedDictionary<string, string> dictionary2 = (LinkedDictionary<string, string>)formatter.Deserialize(stream);
        foreach (KeyValuePair<string, string> pair in dictionary) {
            string value2 = dictionary2[pair.Key];
            Assert.AreEqual(pair.Value, value2);
        }
    }
}