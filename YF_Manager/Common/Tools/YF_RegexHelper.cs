using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YF_Manager
{
    /// <summary>
    /// 正则表达式匹配结果项，记录单个匹配在原文中的位置与内容
    /// </summary>
    public class RegexMatchResult
    {
        /// <summary>
        /// 匹配内容在原文中的起始索引
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// 匹配内容长度
        /// </summary>
        public int Length { get; init; }

        /// <summary>
        /// 匹配到的文本内容
        /// </summary>
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// 正则表达式测试辅助工具，封装模式校验、选项构建与匹配执行逻辑，
    /// 供正则测试工具箱界面使用，同时方便单元测试。
    /// </summary>
    public static class YF_RegexHelper
    {
        /// <summary>
        /// 构建正则选项组合
        /// </summary>
        /// <param name="ignoreCase">忽略大小写</param>
        /// <param name="multiline">多行模式（^ $ 匹配每行首尾）</param>
        /// <param name="singleline">单行模式（. 匹配换行符）</param>
        /// <param name="ignorePatternWhitespace">忽略模式中的空白与注释</param>
        /// <returns>合并后的 RegexOptions</returns>
        public static RegexOptions BuildOptions(bool ignoreCase, bool multiline, bool singleline, bool ignorePatternWhitespace)
        {
            var options = RegexOptions.None;
            if (ignoreCase) options |= RegexOptions.IgnoreCase;
            if (multiline) options |= RegexOptions.Multiline;
            if (singleline) options |= RegexOptions.Singleline;
            if (ignorePatternWhitespace) options |= RegexOptions.IgnorePatternWhitespace;
            return options;
        }

        /// <summary>
        /// 校验正则模式是否合法
        /// </summary>
        /// <param name="pattern">正则模式字符串</param>
        /// <param name="errorMessage">非法时输出错误信息，合法时为空</param>
        /// <returns>模式合法返回 true</returns>
        public static bool IsValidPattern(string pattern, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(pattern))
            {
                errorMessage = "正则表达式不能为空";
                return false;
            }
            try
            {
                _ = new Regex(pattern);
                return true;
            }
            catch (ArgumentException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 执行匹配，返回所有匹配结果（含索引、长度、内容）
        /// </summary>
        /// <param name="pattern">正则模式字符串</param>
        /// <param name="input">待匹配的测试文本</param>
        /// <param name="options">正则选项</param>
        /// <returns>匹配结果列表；模式非法时返回空列表</returns>
        public static IReadOnlyList<RegexMatchResult> MatchAll(string pattern, string input, RegexOptions options)
        {
            var results = new List<RegexMatchResult>();
            if (string.IsNullOrEmpty(pattern) || input == null)
                return results;

            try
            {
                var regex = new Regex(pattern, options);
                foreach (Match m in regex.Matches(input))
                {
                    results.Add(new RegexMatchResult
                    {
                        Index = m.Index,
                        Length = m.Length,
                        Value = m.Value
                    });
                }
            }
            catch (ArgumentException)
            {
                // 模式非法时不抛异常，由调用方通过 IsValidPattern 提前拦截
            }
            return results;
        }
    }
}