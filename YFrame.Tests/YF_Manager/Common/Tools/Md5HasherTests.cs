using System.IO;
using System.Threading.Tasks;
using Xunit;
using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Tools
{
    /// <summary>
    /// Md5Hasher 单元测试
    /// </summary>
    public class Md5HasherTests
    {
        // 已知 MD5 参考值
        private const string AbcMd5 = "900150983cd24fb0d6963f7d28e17f72";

        /// <summary>
        /// 空字符串与 null 均返回空串
        /// </summary>
        [Fact]
        public void ComputeString_EmptyOrNull_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, Md5Hasher.ComputeString(""));
            Assert.Equal(string.Empty, Md5Hasher.ComputeString(null!));
        }

        /// <summary>
        /// "abc" 的 MD5 应与已知参考值一致
        /// </summary>
        [Fact]
        public void ComputeString_KnownValue_Matches()
        {
            Assert.Equal(AbcMd5, Md5Hasher.ComputeString("abc"));
        }

        /// <summary>
        /// 中文内容计算结果稳定可复现
        /// </summary>
        [Fact]
        public void ComputeString_Unicode_IsStable()
        {
            string content = "你好，YFrame";
            Assert.Equal(Md5Hasher.ComputeString(content), Md5Hasher.ComputeString(content));
        }

        /// <summary>
        /// 同一文件两次异步计算哈希一致，且长度为 32 位
        /// </summary>
        [Fact]
        public async Task ComputeFileAsync_SameFileTwice_ReturnsSameHash()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "测试文件内容 test content 12345");
                string h1 = await Md5Hasher.ComputeFileAsync(path);
                string h2 = await Md5Hasher.ComputeFileAsync(path);
                Assert.Equal(h1, h2);
                Assert.Equal(32, h1.Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 大文件分块计算时进度最终达到 1.0，且结果为 32 位
        /// </summary>
        [Fact]
        public async Task ComputeFileAsync_ReportsFinalProgress()
        {
            string path = Path.GetTempFileName();
            double last = 0;
            try
            {
                // 2MB 内容，触发多次 1MB 分块读取
                File.WriteAllText(path, new string('x', 2 * 1024 * 1024));
                var progress = new Progress<double>(p => last = p);
                string hash = await Md5Hasher.ComputeFileAsync(path, progress);
                Assert.Equal(1.0, last, 3);
                Assert.Equal(32, hash.Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 计算不存在的文件应抛出 FileNotFoundException
        /// </summary>
        [Fact]
        public async Task ComputeFileAsync_MissingFile_Throws()
        {
            string missing = Path.Combine(Path.GetTempPath(), $"missing_{System.Guid.NewGuid():N}.bin");
            await Assert.ThrowsAsync<FileNotFoundException>(() => Md5Hasher.ComputeFileAsync(missing));
        }

        /// <summary>
        /// 内容相同的两个文件 MD5 对比为 true
        /// </summary>
        [Fact]
        public void AreFilesEqual_IdenticalFiles_ReturnsTrue()
        {
            string a = Path.GetTempFileName();
            string b = Path.GetTempFileName();
            try
            {
                File.WriteAllText(a, "相同内容");
                File.WriteAllText(b, "相同内容");
                Assert.True(Md5Hasher.AreFilesEqual(a, b));
            }
            finally
            {
                File.Delete(a);
                File.Delete(b);
            }
        }

        /// <summary>
        /// 内容不同的两个文件 MD5 对比为 false
        /// </summary>
        [Fact]
        public void AreFilesEqual_DifferentContent_ReturnsFalse()
        {
            string a = Path.GetTempFileName();
            string b = Path.GetTempFileName();
            try
            {
                File.WriteAllText(a, "内容A");
                File.WriteAllText(b, "内容B");
                Assert.False(Md5Hasher.AreFilesEqual(a, b));
            }
            finally
            {
                File.Delete(a);
                File.Delete(b);
            }
        }

        /// <summary>
        /// 长度相同但内容不同的两个文件 MD5 对比为 false（验证非仅靠大小短路）
        /// </summary>
        [Fact]
        public void AreFilesEqual_SameLengthDifferentContent_ReturnsFalse()
        {
            string a = Path.GetTempFileName();
            string b = Path.GetTempFileName();
            try
            {
                File.WriteAllText(a, "AAAA");
                File.WriteAllText(b, "BBBB");
                Assert.False(Md5Hasher.AreFilesEqual(a, b));
            }
            finally
            {
                File.Delete(a);
                File.Delete(b);
            }
        }

        /// <summary>
        /// 任一文件不存在时对比返回 false
        /// </summary>
        [Fact]
        public void AreFilesEqual_MissingFile_ReturnsFalse()
        {
            string a = Path.GetTempFileName();
            try
            {
                File.WriteAllText(a, "x");
                string missing = Path.Combine(Path.GetTempPath(), $"missing_{System.Guid.NewGuid():N}.bin");
                Assert.False(Md5Hasher.AreFilesEqual(a, missing));
            }
            finally
            {
                File.Delete(a);
            }
        }
    }
}