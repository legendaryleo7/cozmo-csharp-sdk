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

namespace Anki.Cozmo.Sdk;

/// <summary>A named CLAD enumeration value, mirroring the Python SDK's <c>CladEnumWrapper</c>.</summary>
/// <typeparam name="TEnum">Generated CLAD enumeration type.</typeparam>
/// <remarks>
/// Python wraps CLAD enums as classes of named entries with a numeric id. C# keeps the
/// generated enum for wire use and exposes this lightweight name/id wrapper for the public
/// API, for example <c>CladEnum&lt;ExecutableBehaviorType&gt;.FromName("Idle")</c>.
/// </remarks>
public sealed class CladEnum<TEnum> : IEquatable<CladEnum<TEnum>>
    where TEnum : struct, Enum
{
    private static readonly Lazy<Catalog> Entries = new(Catalog.Create);

    private CladEnum(TEnum value, string name, long id)
    {
        Value = value;
        Name = name;
        Id = id;
    }

    /// <summary>Name of the CLAD enumeration entry.</summary>
    public string Name { get; }

    /// <summary>Generated enumeration value.</summary>
    public TEnum Value { get; }

    /// <summary>Numeric id of the entry, as used by the Python SDK.</summary>
    public long Id { get; }

    /// <summary>Every named entry of the enumeration, in declaration order.</summary>
    public static IReadOnlyList<CladEnum<TEnum>> All => Entries.Value.All;

    /// <summary>Looks up an entry by its enumeration value.</summary>
    /// <exception cref="KeyNotFoundException">The value is not a named entry.</exception>
    public static CladEnum<TEnum> FromValue(TEnum value)
    {
        if (Entries.Value.ByValue.TryGetValue(value, out CladEnum<TEnum>? entry))
        {
            return entry;
        }

        throw new KeyNotFoundException($"'{value}' is not a named {typeof(TEnum).Name} entry.");
    }

    /// <summary>Looks up an entry by name.</summary>
    /// <exception cref="KeyNotFoundException">The name is not a named entry.</exception>
    public static CladEnum<TEnum> FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Entries.Value.ByName.TryGetValue(name, out CladEnum<TEnum>? entry))
        {
            return entry;
        }

        throw new KeyNotFoundException($"'{name}' is not a {typeof(TEnum).Name} entry.");
    }

    /// <summary>Looks up an entry by numeric id.</summary>
    /// <exception cref="KeyNotFoundException">The id is not a named entry.</exception>
    public static CladEnum<TEnum> FromId(long id)
    {
        if (Entries.Value.ById.TryGetValue(id, out CladEnum<TEnum>? entry))
        {
            return entry;
        }

        throw new KeyNotFoundException($"'{id}' is not a {typeof(TEnum).Name} id.");
    }

    /// <summary>Tries to look up an entry by numeric id.</summary>
    public static bool TryFromId(long id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CladEnum<TEnum>? entry)
        => Entries.Value.ById.TryGetValue(id, out entry);

    /// <summary>Tries to look up an entry by name.</summary>
    public static bool TryFromName(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CladEnum<TEnum>? entry)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Entries.Value.ByName.TryGetValue(name, out entry);
    }

    /// <inheritdoc />
    public bool Equals(CladEnum<TEnum>? other)
        => other is not null && EqualityComparer<TEnum>.Default.Equals(Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CladEnum<TEnum>);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <summary>Compares two entries by enumeration value.</summary>
    public static bool operator ==(CladEnum<TEnum>? left, CladEnum<TEnum>? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>Compares two entries by enumeration value.</summary>
    public static bool operator !=(CladEnum<TEnum>? left, CladEnum<TEnum>? right) => !(left == right);

    private sealed class Catalog
    {
        private Catalog(
            IReadOnlyList<CladEnum<TEnum>> all,
            Dictionary<TEnum, CladEnum<TEnum>> byValue,
            Dictionary<string, CladEnum<TEnum>> byName,
            Dictionary<long, CladEnum<TEnum>> byId)
        {
            All = all;
            ByValue = byValue;
            ByName = byName;
            ById = byId;
        }

        public IReadOnlyList<CladEnum<TEnum>> All { get; }

        public Dictionary<TEnum, CladEnum<TEnum>> ByValue { get; }

        public Dictionary<string, CladEnum<TEnum>> ByName { get; }

        public Dictionary<long, CladEnum<TEnum>> ById { get; }

        public static Catalog Create()
        {
            var all = new List<CladEnum<TEnum>>();
            var byValue = new Dictionary<TEnum, CladEnum<TEnum>>();
            var byName = new Dictionary<string, CladEnum<TEnum>>(StringComparer.Ordinal);
            var byId = new Dictionary<long, CladEnum<TEnum>>();
            foreach (string name in Enum.GetNames<TEnum>())
            {
                TEnum value = Enum.Parse<TEnum>(name);
                var entry = new CladEnum<TEnum>(value, name, Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                all.Add(entry);
                byValue.TryAdd(value, entry);
                byName.TryAdd(name, entry);
                byId.TryAdd(entry.Id, entry);
            }

            return new Catalog(all, byValue, byName, byId);
        }
    }
}
