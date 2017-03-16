/*
MIT License

Copyright (c) 2017 Eddie Stanley

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FlakeyBit.Libraries.FixedWidth.Common
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class FixedWidthFieldAttribute : Attribute
    {
        private readonly string _recordName;
        private readonly string _fieldDefinitionName;

        /// <summary>
        /// Marks this property as a fixed-width field in a given record type
        /// </summary>
        /// <param name="recordName">The name of the fixed-width record</param>
        /// <param name="fieldDefinitionName">The name of the field within the fixed-width record</param>
        /// <remarks>This attribute can be used multiple times (to allow using the same result class with multiple fixed-width record types)</remarks>
        public FixedWidthFieldAttribute(string recordName, string fieldDefinitionName)
        {
            _recordName = recordName;
            _fieldDefinitionName = fieldDefinitionName; // This could be made optional (use the reflected property name as the default)
        }

        public string RecordName
        {
            get { return _recordName; }
        }

        public string FieldDefinitionName
        {
            get { return _fieldDefinitionName; }
        }

        public static Dictionary<string, PropertyInfo> ReflectFixedWidthFieldProperties(Type recordType, string recordName)
        {
            // Process properties on the record type marked with our attribute,
            // Build a lookup from field name to source/target property.
            Dictionary<string, PropertyInfo> fieldLookup = new Dictionary<string, PropertyInfo>();

            foreach (var prop in recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)) {
                FixedWidthFieldAttribute[] fixedWidthFieldAttributes = (FixedWidthFieldAttribute[]) prop.GetCustomAttributes(typeof (FixedWidthFieldAttribute), true);
                var matches = fixedWidthFieldAttributes.Where(x => x.RecordName == recordName).ToArray();
                if (matches.Length > 1) {
                    throw new ArgumentException(
                        string.Format(
                            "Property '{0}' on record type '{1}' is decorated multiple times with {2} for '{3}'",
                            prop.Name, recordType.Name,
                            typeof (FixedWidthFieldAttribute).Name, recordName));
                }

                if (matches.Length == 1) {
                    FixedWidthFieldAttribute attr = matches[0];
                    if (fieldLookup.ContainsKey(attr.FieldDefinitionName)) {
                        throw new ArgumentException(
                            string.Format(
                                "More than one property on record type '{0}' is decorated with {1} for '{2}' fixed-width field '{3}'",
                                recordType.Name, typeof (FixedWidthFieldAttribute).Name,
                                recordName, attr.FieldDefinitionName));
                    }

                    fieldLookup[attr.FieldDefinitionName] = prop;
                }
            }

            return fieldLookup;
        }
    }
}
