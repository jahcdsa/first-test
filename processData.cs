using LinqToStdf;
using LinqToStdf.Records.V4;
using System;
using System.Collections.Generic;
using System.Linq;

string path = "D:\\C#_Code\\C-study\\C-study-master\\HF0253A_V11_F8DUT_TEST_P15395__05_10272025_091847.stdf";

var reader = new StdfFile(path);
// Cache records to avoid re-enumeration
var allRecords = reader.GetRecords().ToList();
var Prrs = allRecords.OfExactType<Prr>().ToList();
var Ptrs = allRecords.OfExactType<Ptr>().ToList();
var Result = new Dictionary<string, Dictionary<(int, int), double>>();
Result = ProcessData(Ptrs, Prrs);
Console.WriteLine(Result.Count());

// 打印结果查看
foreach (var item in Result)
{
    Console.WriteLine($"Test: {item.Key}, Coordinates: {item.Value.Count}");
    foreach (var coord in item.Value.Take(5)) // 只显示前5个坐标
    {
        Console.WriteLine($"  Coordinate: {coord.Key}, Value: {coord.Value}");
    }
}

Console.ReadKey();

Dictionary<string, Dictionary<(int, int), double>> ProcessData(List<Ptr> ptrs, List<Prr> prrs)
{
    var result = new Dictionary<string, Dictionary<(int, int), double>>();

    if (ptrs == null || prrs == null || ptrs.Count == 0)
        return result;

    int dataIndex = 0;
    int coordinateIndex = 0;

    while (dataIndex < ptrs.Count)
    {
        // 动态检测当前的循环倍数（1-8之间）
        int currentCycleMultiple = DetectCycleMultiple(ptrs, dataIndex);
        //Console.WriteLine($"Processing from index {dataIndex} with multiple: {currentCycleMultiple}");
                                           
        // 处理当前循环倍数的数据块，返回已消费的 Ptr 数量
        int consumed = ProcessCycleBlock(ptrs, prrs, result,
                                        ref dataIndex, ref coordinateIndex, currentCycleMultiple);

        // 如果没有消费任何项，跳过当前项以避免无限循环
        if (consumed == 0)
        {
            dataIndex++;
        }
    }

    return result;
}

int DetectCycleMultiple(List<Ptr> ptrs, int startIndex)
{
    if (startIndex >= ptrs.Count - 1)
        return 1;

    string[] baseNames = ExtractBaseNames(ptrs, startIndex);
    if (baseNames.Length == 0)
        return 1;

    for (int multiple = 8; multiple >= 1; multiple--)
    {
        if (IsValidCycleMultiple(ptrs, startIndex, baseNames, multiple))
        {
            return multiple;
        }
    }
    return 1;
}

string[] ExtractBaseNames(List<Ptr> ptrs, int startIndex)
{
    var baseNames = new List<string>();
    var seenNames = new HashSet<string>();

    int index = startIndex;

    // 收集直到遇到重复名称为止的所有不同名称
    while (index < ptrs.Count && index < startIndex + 32) // 最多检查32个
    {
        string currentName = GetTestName(ptrs[index]);

        if (seenNames.Contains(currentName))
        {
            break;
        }

        seenNames.Add(currentName);
        baseNames.Add(currentName);
        index++;
    }

    return baseNames.ToArray();
}

bool IsValidCycleMultiple(List<Ptr> ptrs, int startIndex, string[] baseNames, int multiple)
{
    int baseCount = baseNames.Length;
    if (baseCount == 0) return false;

    // 检查是否有足够的数据来验证这个倍数
    int requiredLength = baseCount * multiple * 2; // 至少检查两个完整周期
    if (startIndex + requiredLength > ptrs.Count)
        return false;

    // 验证倍数模式
    for (int baseIdx = 0; baseIdx < baseCount; baseIdx++)
    {
        string expectedName = baseNames[baseIdx];

        // 检查当前基础名称在多个周期中的重复模式
        for (int cycle = 0; cycle < 2; cycle++) // 检查两个周期
        {
            for (int repeat = 0; repeat < multiple; repeat++)
            {
                int dataIdx = startIndex + (cycle * baseCount * multiple) + (baseIdx * multiple) + repeat;

                if (dataIdx >= ptrs.Count)
                    return false;

                if (GetTestName(ptrs[dataIdx]) != expectedName)
                    return false;
            }
        }
    }

    return true;
}

// 返回已消费的 Ptr 数量
int ProcessCycleBlock(List<Ptr> ptrs, List<Prr> prrs,
                     Dictionary<string, Dictionary<(int, int), double>> result,
                     ref int dataIndex, ref int coordinateIndex, int cycleMultiple)
{
    // 首先确定基础名称序列
    string[] baseNames = ExtractBaseNames(ptrs, dataIndex);
    if (baseNames.Length == 0)
        return 0;

    int baseCount = baseNames.Length;
    int blockSize = baseCount * cycleMultiple;

    int totalConsumed = 0;

    // 处理整个块（尽可能多的完整周期）
    while (dataIndex + blockSize <= ptrs.Count)
    {
        // 为整个块按顺序分配坐标，使每个 Ptr 对应一个 Prr
        int dataEntriesThisBlock = 0;

        // 处理一个完整周期
        bool blockMatch = true;
        for (int baseIdx = 0; baseIdx < baseCount && blockMatch; baseIdx++)
        {
            string baseName = baseNames[baseIdx];

            for (int repeat = 0; repeat < cycleMultiple; repeat++)
            {
                int currentDataIndex = dataIndex + (baseIdx * cycleMultiple) + repeat;

                if (currentDataIndex >= ptrs.Count)
                {
                    blockMatch = false;
                    break;
                }

                var dataObject = ptrs[currentDataIndex];

                // 验证名称是否符合预期模式
                if (GetTestName(dataObject) != baseName)
                {
                    blockMatch = false;
                    break;
                }

                // 对应的坐标按序号分配，避免覆盖
                int currentCoordIndex = (coordinateIndex + dataEntriesThisBlock) % prrs.Count;
                var coordinate = prrs[currentCoordIndex];

                // 添加到结果字典（先保证非空）
                if (dataObject.Result != null)
                {
                    if (!result.TryGetValue(baseName, out var dict))
                    {
                        dict = new Dictionary<(int, int), double>();
                        result[baseName] = dict;
                    }

                    // 使用 Prr 的坐标信息
                    dict[((int)coordinate.XCoordinate, (int)coordinate.YCoordinate)] = dataObject.Result.Value;
                }

                dataEntriesThisBlock++;
            }
        }

        if (!blockMatch)
        {
            // 如果整个块不匹配，则不要消费它，返回已消费数量
            return totalConsumed;
        }

        // 完整块已处理，更新索引
        dataIndex += blockSize;
        totalConsumed += blockSize;

        // 坐标索引按处理的 Ptr 数量前进
        coordinateIndex = (coordinateIndex + dataEntriesThisBlock) % prrs.Count;
    }

    return totalConsumed;
}

// 辅助方法：从 Ptr 中提取测试名称
string GetTestName(Ptr ptr)
{
    if (ptr == null || string.IsNullOrWhiteSpace(ptr.TestText))
        return "Unknown";

    var parts = ptr.TestText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return parts.Length >= 2 ? parts[1] : parts[0];
}
