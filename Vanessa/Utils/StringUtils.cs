using System;
using System.Text;

namespace Kxnrl.Vanessa.Utils;

public class StringUtils
{
    /// <summary>
    /// 在一串 UTF-8 字节串中从后往前找到最后一个完整的 Unicode 字符
    /// </summary>
    /// <param name="buffer">要查找的字节串</param>
    /// <param name="byteCount">要查找的字节串的长度</param>
    /// <returns>最后一个完整 Unicode 字符的下标</returns>
    public static int FindLastCompleteCharIndex(byte[] buffer, int byteCount)
    {
        // 从字节数组的末尾向前遍历，找到最后一个完整的字符的索引
        for (var position = byteCount - 1; position >= 0; position--)
        {
            // 如果当前字节是单字节字符（ASCII字符），直接返回当前位置
            if (buffer[position] < 0x80)
                return position;

            // 如果当前字节是多字节字符的延续字节（即高两位为10），继续向前查找
            if ((buffer[position] & 0xC0) != 0x80) continue;

            // 计算当前字符的字节数
            var count = 0;
            while (position >= 0 && (buffer[position] & 0xC0) == 0x80)
            {
                position--;
                count++;
            }

            // 如果已经遍历到数组的开头，继续处理下一个字符
            if (position < 0) continue;

            // 获取当前字符的起始字节
            var lead = buffer[position];
            int required;

            // 根据起始字节的值判断当前字符的字节数
            switch (lead >> 4)
            {
                case 0b1100: required = 1; break; // 2字节字符
                case 0b1110: required = 2; break; // 3字节字符
                case 0b1111: required = 3; break; // 4字节字符
                default: continue; // 无效字符，继续处理下一个字符
            }

            // 如果当前字符的字节数与预期一致，返回当前字符的起始位置
            if (count == required)
                return position;
        }

        // 如果没有找到完整的字符，返回-1
        return -1;
    }

    /// <summary>
    /// 将一串字符串截断到最大长度,并在最后添加省略号
    /// </summary>
    /// <param name="str">要截断的字符串</param>
    /// <param name="maxLength">最大长度</param>
    public static string GetTruncatedStringByMaxByteLength(string str, int maxLength)
    {
        // 如果字符串长度小于最大长度，直接返回
        var strBuffer = Encoding.UTF8.GetBytes(str);
        if (strBuffer.Length < maxLength) return str;

        // 预留3个字节给省略号
        var truncatedLength = maxLength - 3;
        var truncatedBuffer = new byte[truncatedLength];
        Buffer.BlockCopy(strBuffer, 0, truncatedBuffer, 0, truncatedLength);
        
        // 找到最后一个完整的字符的索引
        var lastCompleteCharIndex = FindLastCompleteCharIndex(truncatedBuffer, truncatedLength);
        if (lastCompleteCharIndex == -1) return "Error";
        
        // 创建截断后的字节数组
        var truncatedString = new byte[lastCompleteCharIndex];
        Buffer.BlockCopy(truncatedBuffer, 0, truncatedString, 0, lastCompleteCharIndex);
        
        // 将省略号转换为字节数组
        var ellipsisByteString = "..."u8.ToArray();
        
        // 创建最终的字节数组，包含截断后的字符串和省略号
        var finalBuffer = new byte[truncatedString.Length + ellipsisByteString.Length];
        Buffer.BlockCopy(truncatedString, 0, finalBuffer, 0, truncatedString.Length);
        Buffer.BlockCopy(ellipsisByteString, 0, finalBuffer, truncatedString.Length, ellipsisByteString.Length);
        
        // 将最终的字节数组转换为字符串
        return Encoding.UTF8.GetString(finalBuffer);
    }
}