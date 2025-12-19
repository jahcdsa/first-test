using LinqToStdf;
using LinqToStdf.Records.V4;
using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        string path = @"D:\C#_Code\C-study\C-study-master\HF0253A_V11_F8DUT_TEST_P15395__05_10272025_091847.stdf";

        var reader = new StdfFile(path);
        // Cache records to avoid re-enumeration
        var allRecords = reader.GetRecords().ToList();
        var Prrs = allRecords.OfExactType<Prr>().ToList();
        var Ptrs = allRecords.OfExactType<Ptr>().ToList();
        var Result = ProcessData(Ptrs, Prrs);

        Console.WriteLine($"Total test names: {Result.Count}");

        // 打印结果查看（每个测试项前10个坐标 + 统计信息）
        foreach (var item in Result.OrderBy(kvp => kvp.Key))
        {
            var testName = item.Key;
            var values = item.Value.Values; // 所有 float 值

            if (!values.Any())
            {
                Console.WriteLine($"Test: {testName}, No data");
                continue;
            }

            float min = values.Min();
            float max = values.Max();
            float avg = values.Average();

            Console.WriteLine($"Test: {testName}");
            Console.WriteLine($"  Count: {values.Count()}, Min: {min:G9}, Max: {max:G9}, Avg: {avg:G9}");

            // 可选：仍显示前10个点详情
            foreach (var coord in item.Value.Take(10))
            {
                Console.WriteLine($"  Coord: ({coord.Key.Item1}, {coord.Key.Item2}) → Value: {coord.Value:G9}");
            }
            Console.WriteLine(); // 空行分隔
        }

        Console.ReadKey();
    }

    static Dictionary<string, Dictionary<(int, int), float>> ProcessData(List<Ptr> ptrs, List<Prr> prrs)
    {
        var result = new Dictionary<string, Dictionary<(int, int), float>>();

        if (ptrs == null || prrs == null || ptrs.Count == 0 || prrs.Count == 0)
            return result;

        // Step 1: 提取所有测试名称
        var testNames = ptrs.Select(GetTestName).ToList();

        // Step 2: 推断基础名称序列（T1~T30 或其他唯一前缀）
        var baseNames = new List<string>();
        var seen = new HashSet<string>();
        foreach (var name in testNames)
        {
            if (seen.Contains(name)) break;
            seen.Add(name);
            baseNames.Add(name);
        }

        if (baseNames.Count == 0) return result;
        int N = baseNames.Count; // 通常是30

        int ptrIndex = 0;
        int prrIndex = 0;

        while (ptrIndex < ptrs.Count)
        {
            // 尝试从 multiple = 8 到 1 找最大可行倍数
            int multiple = 1;
            for (int m = 8; m >= 1; m--)
            {
                if (ptrIndex + N * m <= ptrs.Count && IsValidBlock(testNames, ptrIndex, baseNames, m))
                {
                    multiple = m;
                    break;
                }
            }

            // 检查是否有足够的 Prr 坐标来支持这个块
            if (prrIndex + multiple > prrs.Count)
            {
                // 坐标不足，可能是文件结尾不完整，停止处理
                break;
            }

            // 处理当前块：N * multiple 个 Ptr，对应 multiple 个连续 Prr
            for (int r = 0; r < multiple; r++) // r = 第 r 次重复（对应第 r 个 Prr）
            {
                var coordRecord = prrs[prrIndex + r];
                var point = ((int)coordRecord.XCoordinate, (int)coordRecord.YCoordinate);

                for (int i = 0; i < N; i++) // 遍历每个基础测试项（T1~T30）
                {
                    int dataIndex = ptrIndex + i * multiple + r;
                    if (dataIndex >= ptrs.Count) break;

                    var ptr = ptrs[dataIndex];
                    string testName = GetTestName(ptr);

                    // 可选：严格校验名称是否匹配（可注释掉用于容错）
                    if (testName != baseNames[i]) continue;

                    if (ptr.Result.HasValue)
                    {
                        if (!result.TryGetValue(testName, out var dict))
                        {
                            dict = new Dictionary<(int, int), float>();
                            result[testName] = dict;
                        }
                        // 如果同一坐标多次写入，后值覆盖前值（符合你的需求）
                        dict[point] = ptr.Result.Value;
                    }
                }
            }

            // 移动指针到下一块
            ptrIndex += N * multiple;
            prrIndex += multiple;
        }

        return result;
    }

    // 验证从 startIndex 开始的 block 是否符合 baseNames 重复 multiple 次的结构
    static bool IsValidBlock(List<string> testNames, int startIndex, List<string> baseNames, int multiple)
    {
        int N = baseNames.Count;
        if (startIndex + N * multiple > testNames.Count)
            return false;

        for (int i = 0; i < N; i++)
        {
            string expected = baseNames[i];
            for (int r = 0; r < multiple; r++)
            {
                int idx = startIndex + i * multiple + r;
                if (testNames[idx] != expected)
                    return false;
            }
        }
        return true;
    }

    // 从 Ptr 中提取测试名称
    static string GetTestName(Ptr ptr)
    {
        if (ptr == null || string.IsNullOrWhiteSpace(ptr.TestText))
            return "Unknown";

        var parts = ptr.TestText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // 通常格式如 "123 T1" 或 "T1"，取第二个或第一个
        return parts.Length >= 2 ? parts[1] : parts[0];
    }
}
