// Copyright (c) 2016-2017 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License in the file LICENSE.txt or at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Anki.Cozmo.Sdk;

/// <summary>Property-list encoding used by the usbmux daemon protocol.</summary>
/// <remarks>
/// Dictionary keys are sorted and XML is tab-indented. The writer supports the scalar,
/// dictionary and array values used by the protocol.
/// </remarks>
internal static class UsbMuxPlist
{
    private const string Prefix =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
        "<plist version=\"1.0\">\n";

    private const string Suffix = "</plist>\n";

    /// <summary>Serializes a dictionary as an XML property list.</summary>
    public static byte[] Serialize(IReadOnlyDictionary<string, object?> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var text = new StringBuilder(Prefix);
        WriteValue(text, value, 0);
        text.Append(Suffix);
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    /// <summary>Parses an XML property list whose root value is a dictionary.</summary>
    public static Dictionary<string, object?> Deserialize(ReadOnlySpan<byte> payload)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        try
        {
            using var stream = new MemoryStream(payload.ToArray(), writable: false);
            using var reader = XmlReader.Create(stream, settings);
            XDocument document = XDocument.Load(reader);
            XElement? plist = document.Root;
            if (plist is null || !string.Equals(plist.Name.LocalName, "plist", StringComparison.Ordinal))
            {
                throw new UsbMuxProtocolException("The usbmux plist root element is missing.");
            }

            XElement? root = plist.Elements().FirstOrDefault();
            if (root is null)
            {
                throw new UsbMuxProtocolException("The usbmux plist has no root value.");
            }

            object? value = ReadValue(root);
            return value as Dictionary<string, object?> ??
                   throw new UsbMuxProtocolException("The usbmux plist root must be a dictionary.");
        }
        catch (UsbMuxProtocolException)
        {
            throw;
        }
        catch (XmlException ex)
        {
            throw new UsbMuxProtocolException("Failed to parse a usbmux property list.", ex);
        }
        catch (FormatException ex)
        {
            throw new UsbMuxProtocolException("Failed to parse a usbmux property list value.", ex);
        }
        catch (OverflowException ex)
        {
            throw new UsbMuxProtocolException("A usbmux property list value is out of range.", ex);
        }
    }

    private static void WriteValue(StringBuilder text, object? value, int indent)
    {
        switch (value)
        {
            case null:
                throw new ArgumentException("A property list cannot contain null values.", nameof(value));

            case string stringValue:
                WriteIndent(text, indent);
                text.Append("<string>").Append(Escape(stringValue)).Append("</string>\n");
                return;

            case bool boolValue:
                WriteIndent(text, indent);
                text.Append(boolValue ? "<true/>\n" : "<false/>\n");
                return;

            case byte[] dataValue:
                WriteIndent(text, indent);
                text.Append("<data>").Append(Convert.ToBase64String(dataValue)).Append("</data>\n");
                return;

            case DateTime dateValue:
                WriteIndent(text, indent);
                text.Append("<date>")
                    .Append(dateValue.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                    .Append("</date>\n");
                return;

            case sbyte or byte or short or ushort or int or uint or long or ulong:
                WriteIndent(text, indent);
                text.Append("<integer>")
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture))
                    .Append("</integer>\n");
                return;

            case float or double or decimal:
                WriteIndent(text, indent);
                text.Append("<real>")
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture))
                    .Append("</real>\n");
                return;

            case IReadOnlyDictionary<string, object?> dictionary:
                WriteDictionary(text, dictionary.Select(pair => pair), indent);
                return;

            case IDictionary<string, object?> dictionary:
                WriteDictionary(text, dictionary, indent);
                return;

            case IEnumerable<object?> sequence:
                WriteArray(text, sequence, indent);
                return;

            default:
                throw new ArgumentException(
                    $"Unsupported property list value type {value.GetType().Name}.",
                    nameof(value));
        }
    }

    private static void WriteDictionary(
        StringBuilder text,
        IEnumerable<KeyValuePair<string, object?>> dictionary,
        int indent)
    {
        List<KeyValuePair<string, object?>> entries = dictionary
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        WriteIndent(text, indent);
        if (entries.Count == 0)
        {
            text.Append("<dict/>\n");
            return;
        }

        text.Append("<dict>\n");
        foreach (KeyValuePair<string, object?> entry in entries)
        {
            WriteIndent(text, indent + 1);
            text.Append("<key>").Append(Escape(entry.Key)).Append("</key>\n");
            WriteValue(text, entry.Value, indent + 1);
        }

        WriteIndent(text, indent);
        text.Append("</dict>\n");
    }

    private static void WriteArray(StringBuilder text, IEnumerable<object?> sequence, int indent)
    {
        List<object?> items = sequence.ToList();
        WriteIndent(text, indent);
        if (items.Count == 0)
        {
            text.Append("<array/>\n");
            return;
        }

        text.Append("<array>\n");
        foreach (object? item in items)
        {
            WriteValue(text, item, indent + 1);
        }

        WriteIndent(text, indent);
        text.Append("</array>\n");
    }

    private static object? ReadValue(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "dict":
                var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
                List<XElement> children = element.Elements().ToList();
                if (children.Count % 2 != 0)
                {
                    throw new UsbMuxProtocolException("A usbmux dictionary has a key without a value.");
                }

                for (int i = 0; i < children.Count; i += 2)
                {
                    XElement key = children[i];
                    if (!string.Equals(key.Name.LocalName, "key", StringComparison.Ordinal))
                    {
                        throw new UsbMuxProtocolException("A usbmux dictionary entry does not start with a key.");
                    }

                    dictionary[key.Value] = ReadValue(children[i + 1]);
                }

                return dictionary;

            case "array":
                return element.Elements().Select(ReadValue).ToList();

            case "string":
                return element.Value;

            case "integer":
                if (!long.TryParse(
                        element.Value.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out long integer))
                {
                    throw new UsbMuxProtocolException($"Invalid usbmux integer '{element.Value}'.");
                }

                return integer;

            case "real":
                if (!double.TryParse(
                        element.Value.Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double real))
                {
                    throw new UsbMuxProtocolException($"Invalid usbmux real '{element.Value}'.");
                }

                return real;

            case "true":
                return true;

            case "false":
                return false;

            case "data":
                return Convert.FromBase64String(element.Value.Trim());

            case "date":
                if (!DateTime.TryParse(
                        element.Value.Trim(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out DateTime date))
                {
                    throw new UsbMuxProtocolException($"Invalid usbmux date '{element.Value}'.");
                }

                return date;

            default:
                throw new UsbMuxProtocolException(
                    $"Unsupported usbmux property list element '{element.Name.LocalName}'.");
        }
    }

    private static string Escape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static void WriteIndent(StringBuilder text, int indent)
    {
        text.Append('\t', indent);
    }
}
