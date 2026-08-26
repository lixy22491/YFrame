using System.Text.RegularExpressions;
using Xunit;
using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Tools
{
    /// <summary>
    /// RegexHelper 单元测试
    /// </summary>
    public class RegexHelperTests
    {
        /// <summary>
        /// 全部选项关闭时返回 None
        /// </summary>
        [Fact]
        public void BuildOptions_AllOff_ReturnsNone()
        {
            var options = YF_RegexHelper.BuildOptions(false, false, false, false);
            Assert.Equal(RegexOptions.None, options);
        }

        /// <summary>
        /// 选项组合正确合并
        /// </summary>
        [Fact]
        public void BuildOptions_Combination_MergesCorrectly()
        {
            var options = YF_RegexHelper.BuildOptions(true, true, true, true);
            Assert.True(options.HasFlag(RegexOptions.IgnoreCase));
            Assert.True(options.HasFlag(RegexOptions.Multiline));
            Assert.True(options.HasFlag(RegexOptions.Singleline));
            Assert.True(options.HasFlag(RegexOptions.IgnorePatternWhitespace));
        }

        /// <summary>
        /// 合法模式通过校验
        /// </summary>
        [Fact]
        public void IsValidPattern_Valid_ReturnsTrue()
        {
            Assert.True(YF_RegexHelper.IsValidPattern(@"\d+", out string? error));
            Assert.Null(error);
        }

        /// <summary>
        /// 非法模式校验失败并返回错误信息
        /// </summary>
        [Fact]
        public void IsValidPattern_Invalid_ReturnsFalseWithError()
        {
            // "[" 是未闭合字符类，属于非法模式
            Assert.False(YF_RegexHelper.IsValidPattern("[", out string? error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        /// <summary>
        /// 空模式校验失败
        /// </summary>
        [Fact]
        public void IsValidPattern_Empty_ReturnsFalse()
        {
            Assert.False(YF_RegexHelper.IsValidPattern("", out _));
        }

        /// <summary>
        /// 基础匹配：统计所有匹配项及索引
        /// </summary>
        [Fact]
        public void MatchAll_Basic_ReturnsAllMatches()
        {
            var results = YF_RegexHelper.MatchAll(@"\d+", "a1 b22 c333", RegexOptions.None);
            Assert.Equal(3, results.Count);
            Assert.Equal("1", results[0].Value);
            Assert.Equal(1, results[0].Index);
            Assert.Equal("22", results[1].Value);
            Assert.Equal("333", results[2].Value);
        }

        /// <summary>
        /// 忽略大小写选项生效
        /// </summary>
        [Fact]
        public void MatchAll_IgnoreCase_MatchesCaseInsensitive()
        {
            var options = YF_RegexHelper.BuildOptions(ignoreCase: true, multiline: false, singleline: false, ignorePatternWhitespace: false);
            var results = YF_RegexHelper.MatchAll("hello", "Hello HELLO hello", options);
            Assert.Equal(3, results.Count);
        }

        /// <summary>
        /// 多行模式：^ 可匹配每行行首
        /// </summary>
        [Fact]
        public void MatchAll_Multiline_AnchorsPerLine()
        {
            var options = YF_RegexHelper.BuildOptions(false, multiline: true, false, false);
            var results = YF_RegexHelper.MatchAll("^abc", "abc\nabc\nxyz", options);
            Assert.Equal(2, results.Count);
        }

        /// <summary>
        /// 无匹配时返回空列表
        /// </summary>
        [Fact]
        public void MatchAll_NoMatch_ReturnsEmpty()
        {
            var results = YF_RegexHelper.MatchAll(@"\d+", "无数字", RegexOptions.None);
            Assert.Empty(results);
        }

        /// <summary>
        /// 非法模式执行匹配返回空列表而不抛异常
        /// </summary>
        [Fact]
        public void MatchAll_InvalidPattern_ReturnsEmptyWithoutThrow()
        {
            var results = YF_RegexHelper.MatchAll("[", "some text", RegexOptions.None);
            Assert.Empty(results);
        }

        /// <summary>
        /// 空输入或空模式返回空列表
        /// </summary>
        [Fact]
        public void MatchAll_EmptyInputOrPattern_ReturnsEmpty()
        {
            Assert.Empty(YF_RegexHelper.MatchAll("", "text", RegexOptions.None));
            Assert.Empty(YF_RegexHelper.MatchAll("\\d+", "", RegexOptions.None));
        }
    }
}