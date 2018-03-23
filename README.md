# dotnet-fixedwidth
A simple library to assist with parsing fixed-width data (files) in .NET Core (C#)

## Usage:

Create one or more classes implementing ```IFixedWidthInputRecordDefinition``` (defines the shape of the fixed-width line & how to parse):

```C#
public class TestInputRecordDefinition : IFixedWidthInputRecordDefinition
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
```

Define one or more types for output records (no specific base class is necessary) with public properties decorated with ```FixedWidthField```

```C#
public class TestOutputRecord
{
    [FixedWidthField("TestRecord", "NameField")]
    public string Name { get; set; }

    [FixedWidthField("TestRecord", "FamilySizeField")]
    public int NumberOfPeopleInFamily { get; set; }

    [FixedWidthField("TestRecord", "BirthdayField")]
    public DateTime Birthday { get; set; }
}
```

Note that the first parameter of the ```FixedWidthField``` corresponds to ```IFixedWidthInputRecordDefinition::RecordName```.

Instantiate the parser:
```C#
var parser = new FixedWidthParser<TestOutputRecord>(new TestInputRecordDefinition());
```

Parse:

```C#
IEnumerable<string> inputLines = ...

foreach (var record in parser.Parse(inputLines)) {
    Console.Out.WriteLine(record.Birthday.DayOfWeek);
}
```

There is an alternative method ```ParseIter``` which returns ```IEnumerator<TResultRecord>``` as well as a static method ``` DumpRecord(IFixedWidthInputRecordDefinition recordDefinition, string line)``` which can assist debugging (e.g. when defining ```IFixedWidthInputRecordDefinition```

