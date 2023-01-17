using System;
using System.IO;
using System.Linq;
using System.Text;
using AsmResolver.IO;
using AsmResolver.Patching;

namespace AsmResolver
{
    /// <summary>
    /// Represents a single chunk of data residing in a file or memory space.
    /// </summary>
    public interface ISegment : IOffsetProvider, IWritable
    {
        /// <summary>
        /// Determines whether this structure can be relocated to another offset or virtual address.
        /// </summary>
        bool CanUpdateOffsets
        {
            get;
        }

        /// <summary>
        /// Computes the number of bytes the segment will contain when it is mapped into memory.
        /// </summary>
        /// <returns>The number of bytes.</returns>
        uint GetVirtualSize();

        /// <summary>
        /// Assigns a new file and virtual offset to the segment and all its sub-components.
        /// </summary>
        /// <param name="parameters">The parameters containing the new offset information for the segment.</param>
        void UpdateOffsets(in RelocationParameters parameters);

    }

    public static partial class Extensions
    {
        private const string ReservedStringCharacters = "\\\"\t\r\n\b";

        [ThreadStatic]
        private static StringBuilder? _buffer;

        /// <summary>
        /// Rounds the provided unsigned integer up to the nearest multiple of the provided alignment.
        /// </summary>
        /// <param name="value">The value to align.</param>
        /// <param name="alignment">The alignment. Must be a power of 2.</param>
        /// <returns>The aligned value.</returns>
        public static uint Align(this uint value, uint alignment)
        {
            alignment--;
            return (value + alignment) & ~alignment;
        }

        /// <summary>
        /// Rounds the provided unsigned integer up to the nearest multiple of the provided alignment.
        /// </summary>
        /// <param name="value">The value to align.</param>
        /// <param name="alignment">The alignment. Must be a power of 2.</param>
        /// <returns>The aligned value.</returns>
        public static ulong Align(this ulong value, ulong alignment)
        {
            alignment--;
            return (value + alignment) & ~alignment;
        }

        /// <summary>
        /// Computes the number of bytes the provided integer would require after compressing it using the integer
        /// compression as specified in ECMA-335.
        /// </summary>
        /// <param name="value">The integer to determine the compressed size of.</param>
        /// <returns>The number of bytes the value would require.</returns>
        public static uint GetCompressedSize(this uint value) => value switch
        {
            < 0x80 => sizeof(byte),
            < 0x4000 => sizeof(ushort),
            _ => sizeof(uint)
        };

        /// <summary>
        /// Computes the number of bytes the provided integer would require after compressing it using the integer
        /// compression using the 7-bit encoding.
        /// </summary>
        /// <param name="value">The integer to determine the compressed size of.</param>
        /// <returns>The number of bytes the value would require.</returns>
        public static uint Get7BitEncodedSize(this uint value) => value switch
        {
            < 0b1000_0000 => 1,
            < 0b100_0000_0000_0000 => 2,
            < 0b10_0000_0000_0000_0000_0000 => 3,
            < 0b10000_0000_0000_0000_0000_0000_0000 => 4,
            _ => 5
        };

        /// <summary>
        /// Computes the number of bytes required to represent the provided string as a binary formatted string.
        /// </summary>
        /// <param name="value">The string to measure.</param>
        /// <returns>The number of bytes.</returns>
        public static uint GetBinaryFormatterSize(this string value) => value.GetBinaryFormatterSize(Encoding.UTF8);

        /// <summary>
        /// Computes the number of bytes required to represent the provided string as a binary formatted string.
        /// </summary>
        /// <param name="value">The string to measure.</param>
        /// <param name="encoding">The encoding to use.</param>
        /// <returns>The number of bytes.</returns>
        public static uint GetBinaryFormatterSize(this string value, Encoding encoding)
        {
            uint count = (uint) encoding.GetByteCount(value);
            return count.Get7BitEncodedSize() + count;
        }

        /// <summary>
        /// Converts a string into an escaped string literal.
        /// </summary>
        /// <param name="literal">The string to convert.</param>
        /// <returns>The escaped string.</returns>
        public static string CreateEscapedString(this string literal)
        {
            _buffer ??= new StringBuilder(literal.Length + 2);
            _buffer.Clear();

            _buffer.Append('"');
            foreach (char currentChar in literal)
            {
                if (ReservedStringCharacters.Contains(currentChar))
                    _buffer.Append('\\');
                _buffer.Append(currentChar);
            }
            _buffer.Append('"');

            return _buffer.ToString();
        }

        /// <summary>
        /// Constructs a reference to the start of the segment.
        /// </summary>
        /// <param name="segment">The segment to reference.</param>
        /// <returns>The reference.</returns>
        public static ISegmentReference ToReference(this ISegment segment) => new SegmentReference(segment);

        /// <summary>
        /// Constructs a reference to an offset within the segment.
        /// </summary>
        /// <param name="segment">The segment to reference.</param>
        /// <param name="additive">The offset within the segment to reference.</param>
        /// <returns>The reference.</returns>
        public static ISegmentReference ToReference(this ISegment segment, int additive) => additive == 0
            ? new SegmentReference(segment)
            : new RelativeReference(segment, additive);

        /// <summary>
        /// Serializes the segment by calling <see cref="ISegment.Write"/> and writes the result into a byte array.
        /// </summary>
        /// <param name="segment">The segment to serialize to </param>
        /// <returns>The resulting byte array.</returns>
        public static byte[] WriteIntoArray(this ISegment segment)
        {
            using var stream = new MemoryStream();
            segment.Write(new BinaryStreamWriter(stream));
            return stream.ToArray();
        }

        /// <summary>
        /// Serializes the segment by calling <see cref="ISegment.Write"/> and writes the result into a byte array.
        /// </summary>
        /// <param name="segment">The segment to serialize to </param>
        /// <param name="pool">The memory stream writer pool to rent temporary writers from.</param>
        /// <returns>The resulting byte array.</returns>
        public static byte[] WriteIntoArray(this ISegment segment, MemoryStreamWriterPool pool)
        {
            using var rentedWriter = pool.Rent();
            segment.Write(rentedWriter.Writer);
            return rentedWriter.GetData();
        }

        /// <summary>
        /// Wraps the provided segment into a <see cref="PatchedSegment"/>, making it eligible for applying
        /// post-serialization patches.
        /// </summary>
        /// <param name="segment">The segment to wrap.</param>
        /// <returns>
        /// The wrapped segment, or <paramref name="segment"/> if it is already an instance of
        /// <see cref="PatchedSegment"/>.
        /// </returns>
        public static PatchedSegment AsPatchedSegment(this ISegment segment) => segment.AsPatchedSegment(false);

        /// <summary>
        /// Wraps the provided segment into a <see cref="PatchedSegment"/>, making it eligible for applying
        /// post-serialization patches.
        /// </summary>
        /// <param name="segment">The segment to wrap.</param>
        /// <param name="alwaysCreateNew">
        /// Indicates whether the segment should always be wrapped into a new instance of <see cref="PatchedSegment"/>,
        /// regardless of whether <paramref name="segment"/> is already an instance of
        /// <see cref="PatchedSegment"/> or not.
        /// </param>
        /// <returns>
        /// The wrapped segment, or <paramref name="segment"/> if it is already an instance of
        /// <see cref="PatchedSegment"/> and <paramref name="alwaysCreateNew"/> is set to <c>true</c>.
        /// </returns>
        public static PatchedSegment AsPatchedSegment(this ISegment segment, bool alwaysCreateNew)
        {
            if (alwaysCreateNew)
                return new PatchedSegment(segment);

            return segment as PatchedSegment ?? new PatchedSegment(segment);
        }
    }
}
