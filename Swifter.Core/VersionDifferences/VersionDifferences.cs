﻿using Swifter.Tools;
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Swifter
{
    /// <summary>
    /// 该文档用于解决版本差异性。
    /// </summary>
    public static partial class VersionDifferences
    {
#if NETSTANDARD2_0
        internal static readonly bool? DynamicAssemblyCanAccessNonPublicTypes = null;
        internal static readonly bool? DynamicAssemblyCanAccessNonPublicMembers = null;
#else
        internal static readonly bool? DynamicAssemblyCanAccessNonPublicTypes = false;
        internal static readonly bool? DynamicAssemblyCanAccessNonPublicMembers = false;
#endif
        /// <summary>
        /// 获取对象的 TypeHandle 值。
        /// </summary>
        /// <param name="obj">对象</param>
        /// <returns>返回一个 IntPtr 值。</returns>

#if NET451 || NET45 || NET40 || NET35 || NET30 || NET20 || NETCOREAPP2_0 || NETCOREAPP2_1
        [MethodImpl(AggressiveInlining)]
        public static IntPtr GetTypeHandle(object obj)
        {
            return Unsafe.GetObjectTypeHandle(obj);
        }
#else
        [MethodImpl(AggressiveInlining)]
        public static IntPtr GetTypeHandle(object obj)
        {
            if (ObjectTypeHandleEqualsTypeHandle)
            {
                // Faster
                return Unsafe.GetObjectTypeHandle(obj);
            }
            else
            {
                // Stable
                return obj.GetType().TypeHandle.Value;
            }
        }
#endif
        /// <summary>
        /// 判断 ObjectTypeHandle 和 TypeHandle 是否一致。
        /// </summary>
        public static readonly bool ObjectTypeHandleEqualsTypeHandle =
            typeof(DBNull).TypeHandle.Value == Unsafe.GetObjectTypeHandle(DBNull.Value) &&
            typeof(string).TypeHandle.Value == Unsafe.GetObjectTypeHandle(string.Empty);

#if NET20 || NET30 || NET35 || NET40
        /// <summary>
        /// 表示该方法尽量内敛。
        /// </summary>
        public const MethodImplOptions AggressiveInlining = (MethodImplOptions)256;

        /// <summary>
        /// 定义动态程序集。
        /// </summary>
        /// <param name="assName">程序集名称</param>
        /// <param name="access">程序集的可访问性</param>
        /// <returns>返回动态程序集生成器</returns>
        [MethodImpl(AggressiveInlining)]
        public static AssemblyBuilder DefineDynamicAssembly(AssemblyName assName, AssemblyBuilderAccess access)
        {
            return AppDomain.CurrentDomain.DefineDynamicAssembly(assName, access);
        }

        /// <summary>
        /// 创建运行时程序集。
        /// </summary>
        /// <param name="typeBuilder">动态程序集生成器</param>
        /// <returns>运行时程序集</returns>
        public static Type CreateTypeInfo(this TypeBuilder typeBuilder)
        {
            return typeBuilder.CreateType();
        }
#else
        /// <summary>
        /// 表示该方法尽量内敛。
        /// </summary>
        public const MethodImplOptions AggressiveInlining = MethodImplOptions.AggressiveInlining;

        /// <summary>
        /// 定义动态程序集。
        /// </summary>
        /// <param name="assName">程序集名称</param>
        /// <param name="access">程序集的可访问性</param>
        /// <returns>返回动态程序集生成器</returns>
        [MethodImpl(AggressiveInlining)]
        public static AssemblyBuilder DefineDynamicAssembly(AssemblyName assName, AssemblyBuilderAccess access)
        {
            return AssemblyBuilder.DefineDynamicAssembly(assName, access);
        }
#endif

        /// <summary>
        /// 往字符串写入器中写入一个字符串。
        /// </summary>
        /// <param name="textWriter">字符串写入器</param>
        /// <param name="chars">字符串地址</param>
        /// <param name="length">字符串长度</param>
        [MethodImpl(AggressiveInlining)]
        public static unsafe void WriteChars(TextWriter textWriter, char* chars, int length)
        {
#if NETCOREAPP2_1
            textWriter.Write(new ReadOnlySpan<char>(chars, length));
#else
            if (length > 130 && ArrayHelper.IsSupportedOneRankValueArrayInfo)
            {
                var ends = ArrayHelper.AsTempOneRankValueArray<char>(chars, length, out var starts);

                textWriter.Write(starts);

                textWriter.Write(ends);

                Unsafe.CopyBlock(
                    ref Unsafe.As<char, byte>(ref chars[0]),
                    ref Unsafe.As<char, byte>(ref starts[0]),
                    (uint)(starts.Length * sizeof(char)));
            }
            else
            {
                const int bufferLength = 128;

                var buffer = new char[Math.Min(bufferLength, length)];

                for (int index = 0, count = buffer.Length;
                    index < length;
                    index += count, count = Math.Min(buffer.Length, length - index))
                {
                    Unsafe.CopyBlock(
                        ref Unsafe.As<char, byte>(ref buffer[0]),
                        ref Unsafe.As<char, byte>(ref chars[index]),
                        (uint)(count * sizeof(char)));

                    textWriter.Write(buffer, 0, count);
                }
            }
#endif
        }

        /// <summary>
        /// 在字符串读取器中读取一个字符串。
        /// </summary>
        /// <param name="textReader">字符串读取器</param>
        /// <param name="chars">字符串地址</param>
        /// <param name="length">字符串长度</param>
        /// <returns>返回读取的字符串长度</returns>
        [MethodImpl(AggressiveInlining)]
        public static unsafe int ReadChars(TextReader textReader, char* chars, int length)
        {
#if NETCOREAPP2_1
            return textReader.Read(new Span<char>(chars, length));
#else
            if (length > 130 && ArrayHelper.IsSupportedOneRankValueArrayInfo)
            {
                var ends = ArrayHelper.AsTempOneRankValueArray<char>(chars, length, out var starts);

                var count = textReader.Read(starts, 0, starts.Length);

                if (count == starts.Length)
                {
                    count += textReader.Read(ends, 0, ends.Length);
                }

                Unsafe.CopyBlock(
                    ref Unsafe.As<char, byte>(ref chars[0]),
                    ref Unsafe.As<char, byte>(ref starts[0]),
                    (uint)(starts.Length * sizeof(char)));

                return count;
            }
            else
            {
                const int bufferLength = 128;

                var buffer = new char[bufferLength];

                var total = 0;

                int readCount;

                while (total < length &&
                    (readCount = textReader.Read(buffer, 0, Math.Min(bufferLength, length - total))) != 0)
                {
                    Unsafe.CopyBlock(
                        ref Unsafe.As<char, byte>(ref chars[total]),
                        ref Unsafe.As<char, byte>(ref buffer[0]),
                        (uint)readCount * sizeof(char));

                    total += readCount;
                }

                return total;
            }
#endif
        }

        /// <summary>
        /// 在流中写入一个内存块。
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="bytes">内存块地址</param>
        /// <param name="length">内存块长度</param>
        /// <returns>返回一个异步操作</returns>
        [MethodImpl(AggressiveInlining)]
        public static unsafe void WriteBytes(Stream stream, byte* bytes, int length)
        {
#if NETCOREAPP2_1
            stream.Write(new ReadOnlySpan<byte>(bytes, length));
#else
            if (length > 130 && ArrayHelper.IsSupportedOneRankValueArrayInfo)
            {
                var ends = ArrayHelper.AsTempOneRankValueArray<byte>(bytes, length, out var starts);

                stream.Write(starts, 0, starts.Length);

                stream.Write(ends, 0, ends.Length);

                Unsafe.CopyBlock(
                    ref bytes[0],
                    ref starts[0],
                    (uint)starts.Length);
            }
            else
            {
                const int bufferLength = 128;

                var buffer = new byte[Math.Min(bufferLength, length)];

                for (int index = 0, count = buffer.Length;
                    index < length;
                    index += count, count = Math.Min(buffer.Length, length - index))
                {
                    Unsafe.CopyBlock(
                        ref buffer[0],
                        ref bytes[index],
                        (uint)count);

                    stream.Write(buffer, 0, count);
                }
            }
#endif
        }

        /// <summary>
        /// 在流中读取一个内存块。
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="bytes">内存块地址</param>
        /// <param name="length">内存块长度</param>
        /// <returns>返回一个 int 异步操作</returns>
        [MethodImpl(AggressiveInlining)]
        public static unsafe int ReadBytes(Stream stream, byte* bytes, int length)
        {
#if NETCOREAPP2_1
            return stream.Read(new Span<byte>(bytes, length));
#else
            if (length > 130 && ArrayHelper.IsSupportedOneRankValueArrayInfo)
            {
                var ends = ArrayHelper.AsTempOneRankValueArray<byte>(bytes, length, out var starts);

                var count = stream.Read(starts, 0, starts.Length);

                if (count == starts.Length)
                {
                    count += stream.Read(ends, 0, ends.Length);
                }

                Unsafe.CopyBlock(
                    ref bytes[0],
                    ref starts[0],
                    (uint)starts.Length);

                return count;
            }
            else
            {
                const int bufferLength = 128;

                var buffer = new byte[bufferLength];

                var total = 0;

                int readCount;

                while (total < length &&
                    (readCount = stream.Read(buffer, 0, Math.Min(bufferLength, length - total))) != 0)
                {
                    Unsafe.CopyBlock(
                        ref bytes[total],
                        ref buffer[0],
                        (uint)readCount);

                    total += readCount;
                }

                return total;
            }
#endif
        }


        /// <summary>
        /// 缓冲 TextReader 到 HGlobalCache 中。
        /// </summary>
        /// <param name="hGCache">HGlobalCache</param>
        /// <param name="textReader">TextReader</param>
        /// <returns>返回缓冲的长度</returns>
        [MethodImpl(AggressiveInlining)]
        public static unsafe int Buffer(this HGlobalCache<char> hGCache, TextReader textReader)
        {
            int offset = 0;

        Loop:

            if (offset >= hGCache.Count)
            {
                hGCache.Expand(1218);
            }

            int readCount = ReadChars(
                textReader,
                hGCache.GetPointer() + offset,
                hGCache.Count - offset);

            offset += readCount;

            if (offset == hGCache.Count)
            {
                goto Loop;
            }

            return offset;
        }
    }
}