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

﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using FlakeyBit.Libraries.FixedWidth.Common;

namespace FlakeyBit.Libraries.FixedWidth.Parser
{
    /// <summary>
    /// Static helpers for use with <see cref="FixedWidthParser{TResultRecord}"/>
    /// </summary>
    public abstract class FixedWidthParser
    {
        /// <summary>
        /// Signature of function for field handlers
        /// </summary>
        /// <typeparam name="T">Type returned by the field handler</typeparam>
        /// <param name="value">Input string</param>
        /// <returns>Value of type <see cref="T"/></returns>
        public delegate T FixedWidthInputFieldHandler<out T>(string value);

        // Common field handlers
        public static FixedWidthInputFieldHandler<string> ParseString = rawValue => rawValue;
        public static FixedWidthInputFieldHandler<string> ParseTrimEnd = rawValue => rawValue.TrimEnd();
        public static FixedWidthInputFieldHandler<int> ParseInt = rawValue => int.Parse(rawValue);
        public static FixedWidthInputFieldHandler<DateTime> ParseIso8601Date = ParseIso8601DateImpl;

        /// <summary>
        /// Dumps the record out to a string for debugging purposes.
        /// </summary>
        /// <param name="recordDefinition">Input record definition containing the field definitions to use</param>
        /// <param name="line">Raw line data to be dumped</param>
        /// <returns>String describing the fixed-width parsing field-by-field.</returns>
        /// <remarks>Unlike Parse(), won't throw a spazz if the line length is wrong</remarks>
        public static string DumpRecord(IFixedWidthInputRecordDefinition recordDefinition, string line)
        {
            var parts = new List<string>();
            var totalChars = 0;
            foreach (var fieldDef in recordDefinition.FieldDefinitions) {
                if (fieldDef.Skip) {
                    totalChars += fieldDef.Width;
                    parts.Add(string.Format("(skip {0} bytes), chars so far: {1}", fieldDef.Width, totalChars));
                } else {
                    if (totalChars + fieldDef.Width > line.Length) {
                        parts.Add(string.Format("Field '{0}' ({1} bytes) would exceed the line length ({2})!", fieldDef.Name, fieldDef.Width, line.Length));
                        break;
                    }
                    var rawValue = line.Substring(totalChars, fieldDef.Width);
                    object parsedValue;
                    try {
                        parsedValue = fieldDef.Handler(rawValue);
                    } catch (Exception e) {
                        parts.Add(string.Format("Parsing failed for field '{0}', width: {1}. Raw value was '{2}'. Message: {3}",
                            fieldDef.Name, fieldDef.Width, rawValue, e.Message));
                        break;
                    }

                    totalChars += fieldDef.Width;
                    parts.Add(string.Format(CultureInfo.CurrentCulture, "Field '{0}', width: {1}. Parsed value: '{2}', chars so far: {3}", fieldDef.Name, fieldDef.Width, parsedValue, totalChars));
                }
            }

            if (line.Length > totalChars) {
                parts.Add(string.Format("({0} remaining characters) {1}", (line.Length - totalChars), line.Substring(totalChars)));
            } else {
                parts.Add(String.Format("{0} characters in total", line.Length));
            }

            return String.Join(Environment.NewLine, parts);
        }

        private static DateTime ParseIso8601DateImpl(string rawValue)
        {
            return DateTime.ParseExact(rawValue, "yyyy-MM-dd", null);
        }
    }

    /// <summary>
    /// Generic implementation of a fixed-width parser which processes a sequence of lines and yields a sequence of result records with the appropriate properties set,
    /// based on the mapping implied between the <see cref="IFixedWidthInputRecordDefinition"/> and the <typeparam name="TResultRecord"/>.
    /// The <see cref="IFixedWidthInputRecordDefinition"/> defines the fields that are available in the source fixed-width record; these are automatically wired up to
    /// the <typeparam name="TResultRecord"/> by looking for properties decorated with <see cref="FixedWidthFieldAttribute"/> such that the record name matches and the
    /// field exists in the FieldDefinitions list (of the <see cref="IFixedWidthInputRecordDefinition"/>).
    /// </summary>
    /// <typeparam name="TResultRecord">Type of result record to be populated.
    /// This should have one or more settable properties decorated with a <see cref="FixedWidthFieldAttribute"/> such that the record name matches that of
    /// the <see cref="IFixedWidthInputRecordDefinition"/> and the field name exists in the field definitions of the <see cref="IFixedWidthInputRecordDefinition"/>.</typeparam>
    /// <remarks>This has been designed to be as efficent as possible; delegates are created on construction so that the reflection is only performed once.
    /// Additionally, any fields defined on the <see cref="IFixedWidthInputRecordDefinition"/> which are not consumed by the <see cref="TResultRecord"/> will not be parsed.</remarks>
    public sealed class FixedWidthParser<TResultRecord> : FixedWidthParser where TResultRecord : new()
    {
        private delegate void PopulateRecordResult(TResultRecord record, string line);
        private delegate void FieldSetter(TResultRecord record, object value);
        private readonly int _lineWidth;
        // ReSharper disable once InconsistentNaming
        private readonly PopulateRecordResult PopulateRecord;

        public FixedWidthParser(IFixedWidthInputRecordDefinition inputRecordDefinition)
        {
            Initialise(inputRecordDefinition, out _lineWidth, out PopulateRecord);
        }

        public int LineWidth
        {
            get { return _lineWidth; }
        }

        public TResultRecord Parse(string line)
        {
            TResultRecord record = new TResultRecord();
            PopulateRecord(record, line);
            return record;
        }

        public IEnumerable<TResultRecord> Parse(IEnumerable<string> lineIterator)
        {
            foreach (var line in lineIterator) {
                yield return Parse(line);
            }
        }

        public IEnumerator<TResultRecord> ParseIter(IEnumerable<string> lineIterator)
        {
            return Parse(lineIterator).GetEnumerator();
        }

        #region Implementation

        private struct RangeHandler
        {
            public int Start;
            public int Length;
            public FixedWidthInputFieldHandler<object> HandleValue;
            public FieldSetter SetField;
            public string InputFieldName;
            public string OutputPropertyName;
            public string OutputPropertyTypeName;
        }

        private void Initialise(IFixedWidthInputRecordDefinition inputRecordDefinition, out int lineWidth, out PopulateRecordResult populateRecord)
        {
            Type resultRecordType = typeof(TResultRecord);

            var fieldLookup = FixedWidthFieldAttribute.ReflectFixedWidthFieldProperties(resultRecordType, inputRecordDefinition.RecordName);

            lineWidth = 0;
            HashSet<string> resolvedFields = new HashSet<string>();
            IList<RangeHandler> rangeHandlers = new List<RangeHandler>(inputRecordDefinition.FieldDefinitions.Count);
            for (var i=0; i < inputRecordDefinition.FieldDefinitions.Count; i++) {
                var fieldDefinition = inputRecordDefinition.FieldDefinitions[i];

                if (fieldDefinition.Skip) {
                    lineWidth += fieldDefinition.Width;
                    continue;
                }

                if (resolvedFields.Contains(fieldDefinition.Name)) {
                    throw new ArgumentException(string.Format("Duplicate field definition at index {0} ({1})", i, fieldDefinition.Name), "inputRecordDefinition");
                }

                resolvedFields.Add(fieldDefinition.Name);

                // Similar to the skip case in that don't do anything with the field value.
                // But we still want to keep the checking for duplicate field names.
                if (!fieldLookup.ContainsKey(fieldDefinition.Name)) {
                    lineWidth += fieldDefinition.Width;
                    continue;
                }

                var prop = fieldLookup[fieldDefinition.Name];
                var setMethod = prop.GetSetMethod(true);
                if (setMethod == null) {
                    throw new ArgumentException(string.Format("Property '{0}' on result record type '{1}' is decorated with {2} for '{3}' input field '{4}' but has no setter!",
                        prop.Name, resultRecordType.Name, typeof(FixedWidthFieldAttribute).Name, inputRecordDefinition.RecordName, fieldDefinition.Name));
                }

                // ReSharper disable once AssignNullToNotNullAttribute
                var instance = Expression.Parameter(prop.DeclaringType);
                ParameterExpression parameter = Expression.Parameter(typeof(object), "param");
                var convert = Expression.Convert(parameter, prop.PropertyType);
                var castAndSet = Expression.Call(instance, setMethod, convert);

                var parameters = new [] { instance, parameter };

                var fieldSetter = Expression.Lambda<FieldSetter>(castAndSet, parameters).Compile();

                rangeHandlers.Add(new RangeHandler {
                    Start = LineWidth,
                    Length = fieldDefinition.Width,
                    HandleValue = fieldDefinition.Handler,
                    SetField = fieldSetter,
                    InputFieldName = fieldDefinition.Name,
                    OutputPropertyName = prop.Name,
                    OutputPropertyTypeName = prop.PropertyType.Name
                });
                lineWidth += fieldDefinition.Width;
            }

            // Check for any ouput fields that are unmapped. Unmapped input fields is OK.
            var unmappedOutputFields = new HashSet<string>(fieldLookup.Keys).Except(resolvedFields).ToArray();
            if (unmappedOutputFields.Any()) {
                throw new ArgumentException(string.Format("The field(s) {0} on the output record {1} were not mapped from the input record {2}", string.Join(", ", unmappedOutputFields.Select(x => "'" + x + "'")), resultRecordType.Name, inputRecordDefinition.RecordName), "inputRecordDefinition");
            }

            // Finalise the list since we're creating a closure around it
            rangeHandlers = rangeHandlers.ToArray();

            // Return the final mapping function (from a line to a populated result record)
            populateRecord = (record, line) => {
                foreach (var rangeHandler in rangeHandlers) {
                    var rawValue = line.Substring(rangeHandler.Start, rangeHandler.Length);
                    object value;
                    try {
                        value = rangeHandler.HandleValue(rawValue);
                    } catch (Exception e) {
                        throw new FixedWidthFormatException(string.Format("Failed to parse input field '{0}' value '{1}' (assigned output field: '{2}', type: {3})",
                            rangeHandler.InputFieldName, rawValue, rangeHandler.OutputPropertyName, rangeHandler.OutputPropertyTypeName), e);
                    } try {
                        rangeHandler.SetField(record, value);
                    } catch (InvalidCastException e) {
                        throw new FixedWidthFormatException(string.Format("Failed to assign input field '{0}' (parsed type: {1}) to output field '{2}' (type: {3})",
                            rangeHandler.InputFieldName, value.GetType().Name, rangeHandler.OutputPropertyName, rangeHandler.OutputPropertyTypeName), e);
                    }
                }
            };
        }

        #endregion
    }
}
