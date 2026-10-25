using System.Collections.Generic;
using System.Linq;
using Imcodec.IO;

namespace Imview.Core.Common.Extensions;

public static class ByteStringExtensions {

    public static string OrIfEmpty(this ByteString value, string fallback)
        => value.IsEmpty ? fallback : value.ToString();

    public static List<ByteString> ToByteStrings(this IEnumerable<string> values)
        => values.Select(value => (ByteString) value).ToList();

}
