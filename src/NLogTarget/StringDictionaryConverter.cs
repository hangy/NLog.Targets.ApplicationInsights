// -----------------------------------------------------------------------
// <copyright file="StringDictionaryConverter.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. 
// All rights reserved.  2013
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.ApplicationInsights.NLogTarget
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Converts from NLog Object-properties to ApplicationInsight String-properties
    /// </summary>
    class StringDictionaryConverter(IDictionary<string, string> wrapped) : IDictionary<string, object>
    {
        public object this[string key] { get => wrapped[key]; set => wrapped[key] = SafeValueConverter(value); }

        public ICollection<string> Keys => wrapped.Keys;

        public ICollection<object> Values => new List<object>(wrapped.Values);

        public int Count => wrapped.Count;

        public bool IsReadOnly => wrapped.IsReadOnly;

        public void Add(string key, object value)
        {
            wrapped.Add(key, SafeValueConverter(value));
        }

        public void Add(KeyValuePair<string, object> item)
        {
            wrapped.Add(new KeyValuePair<string, string>(item.Key, SafeValueConverter(item.Value)));
        }

        public void Clear()
        {
            wrapped.Clear();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return wrapped.Contains(new KeyValuePair<string, string>(item.Key, SafeValueConverter(item.Value)));
        }

        public bool ContainsKey(string key)
        {
            return wrapped.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            foreach (var item in wrapped)
            {
                array[arrayIndex++] = new KeyValuePair<string, object>(item.Key, item.Value);
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return this.AsEnumerable().GetEnumerator();
        }

        public bool Remove(string key)
        {
            return wrapped.Remove(key);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return wrapped.Remove(new KeyValuePair<string, string>(item.Key, SafeValueConverter(item.Value)));
        }

        public bool TryGetValue(string key, out object value)
        {
            if (wrapped.TryGetValue(key, out var stringValue))
            {
                value = stringValue;
                return true;
            }
            
            value = null!;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)wrapped).GetEnumerator();
        }

        private IEnumerable<KeyValuePair<string, object>> AsEnumerable()
        {
            foreach (var item in wrapped)
                yield return new KeyValuePair<string, object>(item.Key, item.Value);
        }

        private static string SafeValueConverter(object value)
        {
            try
            {
                if (value == null)
                {
                    return string.Empty;
                }

                // Handle primitive types and strings directly
                if (value is string str)
                {
                    return str;
                }

                // Check if the value is a primitive type or a simple type that Convert.ToString handles well
                var type = value.GetType();
                if (type.IsPrimitive || type.IsEnum || value is decimal || value is DateTime || value is DateTimeOffset || value is Guid || value is TimeSpan)
                {
                    return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                }

                // For complex objects, serialize to JSON
                return JsonSerializer.Serialize(value, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
                });
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
