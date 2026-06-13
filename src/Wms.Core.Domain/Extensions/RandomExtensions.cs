using System;

namespace Wms.Core.Domain.Extensions
{
    /// <summary>
    /// 随机数工具。
    /// RandomUtil 是对 Random 类的包装。在内部维护一个 RandomUtil 实例，并且使用 lock 语句保护，
    /// 因此 RandomUtil 是线程安全的。
    /// </summary>
    public static class RandomExtensions
    {
        private static readonly Random Random = new Random();
        private static readonly object _obj = new object();

        /// <summary>
        /// 返回非负随机整数。
        /// </summary>
        /// <returns></returns>
        public static int Next()
        {
            lock (_obj)
            {
                return Random.Next();
            }
        }

        /// <summary>
        /// 返回小于指定值的非负随机整数。
        /// </summary>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int Next(int max)
        {
            lock (_obj)
            {
                return Random.Next(max);
            }
        }

        /// <summary>
        /// 返回指定范围内的随机整数。
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int Next(int min, int max)
        {
            lock (_obj)
            {
                return Random.Next(min, max);
            }
        }

        /// <summary>
        /// 用随机数填充字节数组。
        /// </summary>
        /// <param name="buffer"></param>
        public static void NextBytes(byte[] buffer)
        {
            lock (_obj)
            {
                Random.NextBytes(buffer);
            }
        }

        /// <summary>
        /// 返回介于 0.0 和 1.0 之间的随机浮点数。
        /// </summary>
        /// <returns></returns>
        public static double NextDouble()
        {
            lock (_obj)
            {
                return Random.NextDouble();
            }
        }



    }

}
