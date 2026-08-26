using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YF_Manager
{
    /// <summary>
    /// MD5 哈希计算工具，支持字符串与文件两种来源，
    /// 文件采用分块异步读取，可实时报告进度并支持取消，
    /// 同时提供双文件 MD5 对比（用于文件版本/内容一致性校验）。
    /// </summary>
    public static class Md5Hasher
    {
        /// <summary>
        /// 文件读取缓冲区大小（1MB），兼顾大文件性能与内存占用
        /// </summary>
        private const int BufferSize = 1024 * 1024;

        /// <summary>
        /// 计算字符串内容的 MD5 值（返回小写十六进制）
        /// </summary>
        /// <param name="content">要计算哈希的文本内容</param>
        /// <returns>32 位小写十六进制 MD5 字符串；内容为空时返回空串</returns>
        public static string ComputeString(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(content));
            return ToHex(hash);
        }

        /// <summary>
        /// 异步计算文件的 MD5 值（分块读取，避免一次性加载大文件）
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="progress">进度回调（0.0 ~ 1.0），可为 null</param>
        /// <param name="cancellationToken">取消令牌，用于中断计算</param>
        /// <returns>32 位小写十六进制 MD5 字符串</returns>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        public static async Task<string> ComputeFileAsync(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("计算 MD5 失败：文件不存在", filePath);

            using var md5 = MD5.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            long totalBytes = stream.Length;

            // 分块读取并喂给 MD5 累加器，同时上报进度
            var buffer = new byte[BufferSize];
            int bytesRead;
            long processed = 0;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                processed += bytesRead;
                progress?.Report(totalBytes > 0 ? (double)processed / totalBytes : 1.0);
            }

            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return ToHex(md5.Hash!);
        }

        /// <summary>
        /// 同步计算文件的 MD5 值（适合中小文件；大文件请使用异步版本）
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <returns>32 位小写十六进制 MD5 字符串</returns>
        public static string ComputeFile(string filePath)
            => ComputeFileAsync(filePath).GetAwaiter().GetResult();

        /// <summary>
        /// 对比两个文件的 MD5 值是否一致（用于校验两份文件内容/版本是否相同）
        /// </summary>
        /// <param name="filePathA">第一个文件路径</param>
        /// <param name="filePathB">第二个文件路径</param>
        /// <returns>两文件 MD5 一致返回 true，否则 false；任一文件不存在返回 false</returns>
        public static bool AreFilesEqual(string filePathA, string filePathB)
        {
            if (string.IsNullOrEmpty(filePathA) || string.IsNullOrEmpty(filePathB))
                return false;
            if (!File.Exists(filePathA) || !File.Exists(filePathB))
                return false;

            // 大小不同则内容必不同，直接短路避免重复计算
            if (new FileInfo(filePathA).Length != new FileInfo(filePathB).Length)
                return false;

            string hashA = ComputeFile(filePathA);
            string hashB = ComputeFile(filePathB);
            return string.Equals(hashA, hashB, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将字节数组转换为小写十六进制字符串
        /// </summary>
        /// <param name="bytes">哈希字节数组</param>
        /// <returns>十六进制字符串</returns>
        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}