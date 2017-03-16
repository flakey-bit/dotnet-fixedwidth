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

namespace FlakeyBit.Libraries.FixedWidth.Parser
{
    public class FixedWidthInputFieldDefinition
    {
        private readonly bool _skip;
        private readonly string _name;
        private readonly int _width;
        private readonly FixedWidthParser.FixedWidthInputFieldHandler<object> _handler;

        private FixedWidthInputFieldDefinition(string name, int width, FixedWidthParser.FixedWidthInputFieldHandler<object> handler)
        {
            _name = name;
            _width = width;
            _handler = handler;
        }

        private FixedWidthInputFieldDefinition(int width)
        {
            _skip = true;
            _width = width;
        }

        /// <summary>
        /// Creates a fixed-width field definition with value transformation/conversion
        /// </summary>
        /// <typeparam name="T">The type the raw string value will be converted into</typeparam>
        /// <param name="name">The name of the field</param>
        /// <param name="width">The width of the field (measured in characters, not bytes)</param>
        /// <param name="handler">The handler used to convert the raw string value</param>
        /// <remarks>If no conversion (raw string value) is desired, use the alternative overload</remarks>
        public static FixedWidthInputFieldDefinition Create<T>(string name, int width, FixedWidthParser.FixedWidthInputFieldHandler<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException("handler");
            return new FixedWidthInputFieldDefinition(name, width, value => handler(value));
        }

        /// <summary>
        /// Creates a fixed-width field definition where the value is parsed as a string with trailing whitespace removed
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="width">The width of the field (measured in characters, not bytes)</param>
        public static FixedWidthInputFieldDefinition Create(string name, int width)
        {
            return new FixedWidthInputFieldDefinition(name, width, FixedWidthParser.ParseTrimEnd);
        }

        public static FixedWidthInputFieldDefinition SkipField(int width)
        {
            return new FixedWidthInputFieldDefinition(width);
        }

        public bool Skip
        {
            get { return _skip; }
        }

        public string Name
        {
            get { return _name; }
        }

        public int Width
        {
            get { return _width; }
        }

        public FixedWidthParser.FixedWidthInputFieldHandler<object> Handler
        {
            get { return _handler; }
        }
    }
}
