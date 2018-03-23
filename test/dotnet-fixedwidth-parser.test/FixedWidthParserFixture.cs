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
using FlakeyBit.Libraries.FixedWidth.Common;
using FlakeyBit.Libraries.FixedWidth.Parser;
using NUnit.Framework;
using Moq;
using System.Globalization;

namespace FlakeyBit.Libraries.Tests.FixedWidth.Parser
{
    [TestFixture]
    public class FixedWidthParserFixture
    {
        [Test]
        public void TestBasicUsage()
        {
            var parser = new FixedWidthParser<TestOutputRecord>(new TestInputRecordDefinition());
            Assert.That(parser.LineWidth, Is.EqualTo(32));

            // Note that splitting the input into lines isn't the responsibility of the fixed width parser. How you want to split the
            // input into lines depends on the exact nature of the file that is being parsed. Thus FixedWidthParser expects to be
            // provided with an IEnumerable<string> of lines.
            var inputLines = new[] {
                "Eddie Stanley       091983-11-07",
                "Cassie              3 1987-03-21"
            };

            IEnumerator<TestOutputRecord> recordIterator = parser.ParseIter(inputLines);

            // Iteration not yet started
            Assert.That(recordIterator.Current, Is.Null);
            // Record 1
            Assert.That(recordIterator.MoveNext(), Is.True);
            var currentRecord = recordIterator.Current;
            Assert.That(currentRecord.Name, Is.EqualTo("Eddie Stanley"));
            Assert.That(currentRecord.NumberOfPeopleInFamily, Is.EqualTo(9));
            Assert.That(currentRecord.Birthday, Is.EqualTo(new DateTime(1983, 11, 7)));
            // Record 2
            Assert.That(recordIterator.MoveNext(), Is.True);
            currentRecord = recordIterator.Current;
            Assert.That(currentRecord.Name, Is.EqualTo("Cassie"));
            Assert.That(currentRecord.NumberOfPeopleInFamily, Is.EqualTo(3));
            Assert.That(currentRecord.Birthday, Is.EqualTo(new DateTime(1987, 3, 21)));
            // End of iteration
            Assert.That(recordIterator.MoveNext(), Is.False);
        }

        [Test]
        public void TestSkipFields()
        {
            var parser = new FixedWidthParser<TestSkipFieldsOutputRecord>(new TestSkipFieldsInputRecord());
            List<TestSkipFieldsOutputRecord> records = new List<TestSkipFieldsOutputRecord>(parser.Parse(new[] { "Eddie Stanley       091983-11-07" }));
            Assert.That(records.Count, Is.EqualTo(1));
            var record = records[0];
            Assert.That(record.Name, Is.EqualTo("Eddie Stanley"));
            Assert.That(record.Birthday, Is.EqualTo(new DateTime(1983, 11, 7)));
        }

        #region Parsing Errors

        [Test]
        public void TestFailureToConvertField()
        {
            var parser = new FixedWidthParser<TestOutputRecord>(new TestInputRecordDefinition());

            Assert.That(() => {
                // ReSharper disable once IteratorMethodResultIsIgnored
                Exhaust(parser.Parse(new [] { "Eddie Stanley       097th Nov 83" }));
            }, Throws.InstanceOf<FixedWidthFormatException>().With.Message.Contains("Failed to parse input field 'BirthdayField' value '7th Nov 83' (assigned output field: 'Birthday', type: DateTime)"));
        }

        [Test]
        public void TestFailureToAssignConvertedValue()
        {
            var parser = new FixedWidthParser<TestFailedAssignmentOutputRecord>(new TestInputRecordDefinition());
            Assert.That(() => {
                // ReSharper disable once IteratorMethodResultIsIgnored
                Exhaust(parser.Parse(new [] { "Eddie Stanley       091983-11-07" }));
            }, Throws.InstanceOf<FixedWidthFormatException>().With.Message.Contains("Failed to assign input field 'NameField' (parsed type: String) to output field 'Birthday' (type: DateTime)"));
        }

        #endregion

        #region Input Record Validation

        [Test]
        public void TestDuplicateFieldDefinitionsOnInputRecord()
        {
            // Tests that duplicate field definitions are not allowed on the input record type,
            // regardless of whether that field is mapped onto the output record or not.

            // Case #1 - Duplicated field (Field1) is not mapped onto the output record
            var faultyInputRecordDefinition1 = new Mock<IFixedWidthInputRecordDefinition>();
            faultyInputRecordDefinition1.Setup(x => x.RecordName).Returns("TestRecord");
            faultyInputRecordDefinition1.Setup(x => x.FieldDefinitions).Returns(
              new[] {
                  FixedWidthInputFieldDefinition.Create("Field1", 10, FixedWidthParser.ParseString),
                  FixedWidthInputFieldDefinition.Create("Field1", 20, FixedWidthParser.ParseIso8601Date),
                  FixedWidthInputFieldDefinition.Create("NameField", 20, FixedWidthParser.ParseString),
                  FixedWidthInputFieldDefinition.Create("FamilySizeField", 2, FixedWidthParser.ParseInt),
                  FixedWidthInputFieldDefinition.Create("BirthdayField", 10, FixedWidthParser.ParseIso8601Date)
            });

            Assert.That(() => {
                // ReSharper disable once ObjectCreationAsStatement
                new FixedWidthParser<TestOutputRecord>(faultyInputRecordDefinition1.Object);

            }, Throws.ArgumentException.With.Message.Contains("Duplicate field definition at index 1 (Field1)"));

            // Case #2 - Duplicated field (NameField) *is* mapped onto the output record (more serious)
            var faultyInputRecordDefinition2 = new Mock<IFixedWidthInputRecordDefinition>();
            faultyInputRecordDefinition2.Setup(x => x.RecordName).Returns("TestRecord");
            faultyInputRecordDefinition2.Setup(x => x.FieldDefinitions).Returns(
                new[] {
                    FixedWidthInputFieldDefinition.Create("NameField", 20, FixedWidthParser.ParseString),
                    FixedWidthInputFieldDefinition.Create("FamilySizeField", 2, FixedWidthParser.ParseInt),
                    FixedWidthInputFieldDefinition.Create("BirthdayField", 10, FixedWidthParser.ParseIso8601Date),
                    FixedWidthInputFieldDefinition.Create("NameField", 20, FixedWidthParser.ParseIso8601Date)
                });
            Assert.That(() => {
                // ReSharper disable once ObjectCreationAsStatement
                new FixedWidthParser<TestOutputRecord>(faultyInputRecordDefinition2.Object);
            }, Throws.ArgumentException.With.Message.Contains("Duplicate field definition at index 3 (NameField)"));
        }

        [Test]
        public void TestInputRecordDefinesAllFields()
        {
            // Tests that the input record has a field definition for every property on the
            // output record that has been marked with the FixedWidthFieldAttribute for the input
            // record defintion's record name.
            var inputRecordDefinition = new Mock<IFixedWidthInputRecordDefinition>();
            inputRecordDefinition.Setup(x => x.RecordName).Returns("TestRecord");
            inputRecordDefinition.Setup(x => x.FieldDefinitions).Returns(
                new[] {
                        FixedWidthInputFieldDefinition.Create("NameField", 20, x => x.Trim()),
                        // No family size field
                        FixedWidthInputFieldDefinition.Create("BirthdayField", 10, FixedWidthParser.ParseIso8601Date)
                });

            Assert.That(() => {
                // ReSharper disable once ObjectCreationAsStatement
                new FixedWidthParser<TestOutputRecord>(inputRecordDefinition.Object);
            }, Throws.ArgumentException.With.Message.Contains(
                    "The field(s) 'FamilySizeField' on the output record TestOutputRecord were not mapped from the input record TestRecord"));
        }

        #endregion

        #region Output Record Validation (FixedWidthFieldAttribute usage)

        [Test]
        public void TestOnlyConsiderFixedWidthFieldAttributesForRecordType()
        {
            // Tests that we only consider FixedWidthField attributes which match our result record type (name)
            Assert.That(() => {
                // ReSharper disable once ObjectCreationAsStatement
                new FixedWidthParser<TestMultipleRecordTypesOutputRecord>(new TestInputRecordDefinition());
            }, Throws.Nothing);
        }

        [Test]
        public void TestDuplicateFieldNamesToSameOutputProperty()
        {
            Assert.That(() => {
                // ReSharper disable once ObjectCreationAsStatement
                new FixedWidthParser<TestDuplicateFieldNamesToSameOutputPropertyOutputRecord>(new TestInputRecordDefinition());
            },
                Throws.ArgumentException.With.Message.Contains(
                    "Property 'Name' on record type 'TestDuplicateFieldNamesToSameOutputPropertyOutputRecord' is decorated multiple times with FixedWidthFieldAttribute for 'TestRecord'"));
        }

        [Test]
        public void TestMultipleInputFieldNamesToSameOutputProperty()
        {
            Assert.That(() => {
                // ReSharper disable once ObjectCreationAsStatement
                new FixedWidthParser<TestMultipleInputFieldNamesToSameOutputPropertyOutputRecord>(new TestInputRecordDefinition());
            },
                Throws.ArgumentException.With.Message.Contains(
                    "Property 'Name' on record type 'TestMultipleInputFieldNamesToSameOutputPropertyOutputRecord' is decorated multiple times with FixedWidthFieldAttribute for 'TestRecord'"));
        }

        [Test]
        public void TestSameInputFieldToMultipleOutputProperties()
        {
            // Tests that we disallow setting two (or more) output properties from the same input field definition.
            Assert.That(() => {
                // ReSharper disable once ObjectCreationAsStatement
                new FixedWidthParser<TestSameInputFieldToMultipleOutputPropertiesOutputRecord>(new TestInputRecordDefinition());
            }, Throws.ArgumentException.With.Message.Contains(
                    "More than one property on record type 'TestSameInputFieldToMultipleOutputPropertiesOutputRecord' is decorated with FixedWidthFieldAttribute for 'TestRecord' fixed-width field 'NameField'"));
        }

        [Test]
        public void TestPropertyWithoutSetter()
        {
            // Tests that we throw an error if a property has been decorated with FixedWidthFieldAttribute but the property has no setter.
            Assert.That(() => {
                // ReSharper disable once ObjectCreationAsStatement
                new FixedWidthParser<TestPropertyMissingSetterOutputRecord>(new TestInputRecordDefinition());
            }, Throws.ArgumentException.With.Message.Contains("Property 'Name' on result record type 'TestPropertyMissingSetterOutputRecord' is decorated with FixedWidthFieldAttribute for 'TestRecord' input field 'NameField' but has no setter!"));
        }

        #endregion

        #region Dump Tests

        [Test]
        public void TestDumpLineRecordOk()
        {
            var dumpText = FixedWidthParser.DumpRecord(new TestInputRecordDefinition(), "Eddie Stanley       071983-11-07");
            var expected = ("Field 'NameField', width: 20. Parsed value: 'Eddie Stanley', chars so far: 20\nField 'FamilySizeField', width: 2. Parsed value: '7', chars so far: 22\nField 'BirthdayField', width: 10. Parsed value: '" + new DateTime(1983, 11, 7).ToString(CultureInfo.CurrentCulture) + "', chars so far: 32\n32 characters in total").Replace("\n", Environment.NewLine);
            Assert.That(dumpText, Is.EqualTo(expected));
        }

        [Test]
        public void TestDumpLineTooShort()
        {
            var dumpText = FixedWidthParser.DumpRecord(new TestInputRecordDefinition(), "Eddie Stanley       091983-11-");
            var expected = "Field 'NameField', width: 20. Parsed value: 'Eddie Stanley', chars so far: 20\nField 'FamilySizeField', width: 2. Parsed value: '9', chars so far: 22\nField 'BirthdayField' (10 bytes) would exceed the line length (30)!\n(8 remaining characters) 1983-11-".Replace("\n", Environment.NewLine);
            Assert.That(dumpText, Is.EqualTo(expected));
        }

        [Test]
        public void TestDumpFieldParsingFailed()
        {
            var dumpText = FixedWidthParser.DumpRecord(new TestInputRecordDefinition(), "Eddie Stanley       XX1983-11-07");
            var expected = "Field 'NameField', width: 20. Parsed value: 'Eddie Stanley', chars so far: 20\nParsing failed for field 'FamilySizeField', width: 2. Raw value was 'XX'. Message: Input string was not in a correct format.\n(12 remaining characters) XX1983-11-07".Replace("\n", Environment.NewLine);
            Assert.That(dumpText, Is.EqualTo(expected));
        }

        [Test]
        public void TestDumpLineTooLong()
        {
            var dumpText = FixedWidthParser.DumpRecord(new TestInputRecordDefinition(), "Eddie Stanley       071983-11-07potatoapplepear");
            var expected = ("Field 'NameField', width: 20. Parsed value: 'Eddie Stanley', chars so far: 20\nField 'FamilySizeField', width: 2. Parsed value: '7', chars so far: 22\nField 'BirthdayField', width: 10. Parsed value: '" + new DateTime(1983, 11, 7).ToString(CultureInfo.CurrentCulture) + "', chars so far: 32\n(15 remaining characters) potatoapplepear").Replace("\n", Environment.NewLine);
            Assert.That(dumpText, Is.EqualTo(expected));
        }

        #endregion

        #region Test Helpers

        private static void Exhaust<T>(IEnumerable<T> enumerable)
        {
            // ReSharper disable once UnusedVariable
            foreach (var item in enumerable) {
            }
        }

        #region Input Record Definitions

        private class TestInputRecordDefinition : IFixedWidthInputRecordDefinition
        {
            public string RecordName
            {
                get { return "TestRecord"; }
            }

            public IList<FixedWidthInputFieldDefinition> FieldDefinitions
            {
                get {
                    return new[] {
                        FixedWidthInputFieldDefinition.Create("NameField", 20, x => x.Trim()),
                        FixedWidthInputFieldDefinition.Create("FamilySizeField", 2, FixedWidthParser.ParseInt),
                        FixedWidthInputFieldDefinition.Create("BirthdayField", 10, FixedWidthParser.ParseIso8601Date)
                    };
                }
            }
        }

        private class TestSkipFieldsInputRecord : IFixedWidthInputRecordDefinition
        {
            public string RecordName
            {
                get { return "TestRecord"; }
            }

            public IList<FixedWidthInputFieldDefinition> FieldDefinitions
            {
                get {
                    return new[] {
                        FixedWidthInputFieldDefinition.Create("NameField", 20, x => x.Trim()),
                        FixedWidthInputFieldDefinition.SkipField(2),
                        FixedWidthInputFieldDefinition.Create("BirthdayField", 10, FixedWidthParser.ParseIso8601Date)
                    };
                }
            }
        }

        #endregion

        #region Output Class Definitions

        // ReSharper disable UnusedMember.Local
        // ReSharper disable UnusedAutoPropertyAccessor.Local

        private class TestOutputRecord
        {
            [FixedWidthField("TestRecord", "NameField")]
            public string Name { get; set; }


            [FixedWidthField("TestRecord", "FamilySizeField")]
            public int NumberOfPeopleInFamily { get; set; }

            [FixedWidthField("TestRecord", "BirthdayField")]
            public DateTime Birthday { get; set; }
        }

        private class TestSkipFieldsOutputRecord
        {
            [FixedWidthField("TestRecord", "NameField")]
            public string Name { get; set; }

            [FixedWidthField("TestRecord", "BirthdayField")]
            public DateTime Birthday { get; set; }
        }

        private class TestDuplicateFieldNamesToSameOutputPropertyOutputRecord
        {
            [FixedWidthField("TestRecord", "NameField")]
            [FixedWidthField("TestRecord", "NameField")]
            public string Name { get; set; }
        }

        private class TestMultipleInputFieldNamesToSameOutputPropertyOutputRecord
        {
            [FixedWidthField("TestRecord", "NameField")]
            [FixedWidthField("TestRecord", "TheName")]
            public string Name { get; set; }
        }

        private class TestFailedAssignmentOutputRecord
        {
            [FixedWidthField("TestRecord", "NameField")]
            public DateTime Birthday { get; set; }
        }

        private class TestMultipleRecordTypesOutputRecord
        {
            [FixedWidthField("TestRecord", "NameField")]
            [FixedWidthField("TestRecord2", "NameField")]
            public string Name { get; set; }
        }

        private class TestSameInputFieldToMultipleOutputPropertiesOutputRecord
        {
            [FixedWidthField("TestRecord", "NameField")]
            public string Name1 { get; set; }


            [FixedWidthField("TestRecord", "NameField")]
            public string Name2 { get; set; }
        }

        private class TestPropertyMissingSetterOutputRecord
        {
            [FixedWidthField("TestRecord", "NameField")]
            public string Name { get { return "NoSetter"; } }
        }

        // ReSharper restore UnusedAutoPropertyAccessor.Local
        // ReSharper restore UnusedMember.Local

        #endregion

        #endregion
    }
}
