using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

public class Kv3Parser
{
    private string content;
    private int index;
    private object parsedData;

    public class ValueStruct
    {
        public object Value { get; set; }
    }

    public Kv3Parser()
    {
        content = string.Empty;
        index = 0;
        parsedData = null;
    }

    public void Parse(string content)
    {
        try
        {
            Console.WriteLine("Starting parse...");
            this.content = content;
            index = 0;
            Console.WriteLine($"Content length: {content.Length}, Initial index: {index}");
            var stopwatch = Stopwatch.StartNew();

            SkipCommentsAndMetadata();
            Console.WriteLine($"After SkipCommentsAndMetadata, index: {index}");

            parsedData = ParseValue().Value;
            Console.WriteLine("Parsing completed.");

            stopwatch.Stop();
            Console.WriteLine($"Parsing took {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parse error at index {index}: {ex.Message}");
            throw;
        }
    }

    public string GetValue(string path)
    {
        try
        {
            Console.WriteLine($"Getting value for path: {path}");
            object currentValue = parsedData;
            var segments = path.Split('.');

            foreach (var segment in segments)
            {
                Console.WriteLine($"Processing segment: {segment}");
                string key = segment;
                int? arrayIndex = null;

                int bracketPos = segment.IndexOf('[');
                if (bracketPos != -1)
                {
                    key = segment.Substring(0, bracketPos);
                    int endBracketPos = segment.IndexOf(']', bracketPos);
                    arrayIndex = int.Parse(segment.Substring(bracketPos + 1, endBracketPos - bracketPos - 1));
                    Console.WriteLine($"Key: {key}, Array Index: {arrayIndex}");
                }

                if (currentValue is Dictionary<string, ValueStruct> obj)
                {
                    Console.WriteLine($"Current value is object with keys: {string.Join(", ", obj.Keys)}");
                    if (obj.TryGetValue(key, out var value))
                    {
                        currentValue = value.Value;
                    }
                    else
                    {
                        Console.WriteLine($"Key not found: {key}");
                        return string.Empty;
                    }
                }

                if (arrayIndex.HasValue && currentValue is List<ValueStruct> arr)
                {
                    Console.WriteLine($"Current value is array of length: {arr.Count}");
                    if (arrayIndex.Value < arr.Count)
                    {
                        currentValue = arr[arrayIndex.Value].Value;
                    }
                    else
                    {
                        Console.WriteLine($"Array index out of bounds: {arrayIndex.Value}");
                        return string.Empty;
                    }
                }
            }

            string result = currentValue is string str ? str : string.Empty;
            Console.WriteLine($"Returning value: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetValue error for path {path}: {ex.Message}");
            return string.Empty;
        }
    }

    private void SkipCommentsAndMetadata()
    {
        Console.WriteLine("Skipping comments and metadata...");
        int iteration = 0;
        while (index < content.Length && content[index] != '{')
        {
            if (iteration++ > 1000000) // Prevent infinite loop
            {
                throw new InvalidOperationException("Infinite loop detected in SkipCommentsAndMetadata");
            }
            index = content.IndexOf('\n', index) + 1;
            if (index == 0) // If no newline found, break to avoid infinite loop
            {
                index = content.Length;
                break;
            }
        }
        Console.WriteLine($"Finished SkipCommentsAndMetadata, index: {index}");
    }

    private void SkipWhitespace()
    {
        int iteration = 0;
        while (index < content.Length && char.IsWhiteSpace(content[index]))
        {
            if (iteration++ > 1000000)
            {
                throw new InvalidOperationException("Infinite loop detected in SkipWhitespace");
            }
            index++;
        }
    }

    private void SkipComments()
    {
        int iteration = 0;
        while (index < content.Length && content[index] == '/')
        {
            if (iteration++ > 1000000)
            {
                throw new InvalidOperationException("Infinite loop detected in SkipComments");
            }
            index = content.IndexOf('\n', index) + 1;
            if (index == 0)
            {
                index = content.Length;
                break;
            }
        }
    }

    private int GetKeyOrValueEnd()
    {
        string delimiters = "= \n{[}],";
        int end = content.IndexOfAny(delimiters.ToCharArray(), index);
        return end == -1 ? content.Length : end;
    }

    private ValueStruct ParseValue()
    {
        try
        {
            Console.WriteLine($"ParseValue at index: {index}");
            SkipComments();
            SkipWhitespace();

            if (index >= content.Length)
            {
                throw new InvalidOperationException("Unexpected end of content in ParseValue");
            }

            if (content[index] == '{')
            {
                Console.WriteLine("Parsing object...");
                return new ValueStruct { Value = ParseObject() };
            }
            else if (content[index] == '[')
            {
                Console.WriteLine("Parsing array...");
                return new ValueStruct { Value = ParseArray() };
            }
            else if (content[index] == '#' && index + 1 < content.Length && content[index + 1] == '[')
            {
                Console.WriteLine("Parsing byte array...");
                index++;
                return ParseByteArray();
            }
            else
            {
                int valueStart = index;
                int valueEnd = GetKeyOrValueEnd();
                Console.WriteLine($"Parsing string value from {valueStart} to {valueEnd}");
                index = valueEnd;
                return new ValueStruct { Value = content.Substring(valueStart, valueEnd - valueStart) };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ParseValue error at index {index}: {ex.Message}");
            throw;
        }
    }

    private ValueStruct ParseByteArray()
    {
        try
        {
            Console.WriteLine($"ParseByteArray at index: {index}");
            SkipWhitespace();
            index++;

            int valueStart = index;
            int valueEnd = content.IndexOf(']', index);
            if (valueEnd == -1)
            {
                throw new InvalidOperationException("Unclosed byte array");
            }
            string rawByteData = content.Substring(valueStart, valueEnd - valueStart);
            Console.WriteLine($"Raw byte data: {rawByteData.Substring(0, Math.Min(rawByteData.Length, 50))}...");
            var bytes = rawByteData.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cleanedByteData = string.Join(" ", bytes);
            index = valueEnd + 1;
            return new ValueStruct { Value = cleanedByteData };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ParseByteArray error at index {index}: {ex.Message}");
            throw;
        }
    }

    private object ParseObject()
    {
        try
        {
            Console.WriteLine($"ParseObject at index: {index}");
            var obj = new Dictionary<string, ValueStruct>();
            SkipWhitespace();
            index++;
            int iteration = 0;

            while (index < content.Length)
            {
                if (iteration++ > 1000000)
                {
                    throw new InvalidOperationException("Infinite loop detected in ParseObject");
                }
                SkipComments();
                SkipWhitespace();

                if (content[index] == '}')
                {
                    index++;
                    Console.WriteLine($"Finished parsing object with {obj.Count} keys");
                    return obj;
                }

                int keyStart = index;
                int keyEnd = GetKeyOrValueEnd();
                if (keyEnd == index)
                {
                    throw new InvalidOperationException("Invalid key at index " + index);
                }
                string key = content.Substring(keyStart, keyEnd - keyStart);
                Console.WriteLine($"Parsed key: {key}");
                index = keyEnd;

                SkipWhitespace();
                if (index < content.Length && content[index] == '=')
                {
                    index++;
                }

                var value = ParseValue();
                obj[key] = value;

                SkipWhitespace();
                if (index < content.Length && content[index] == ',')
                {
                    index++;
                }
            }

            throw new InvalidOperationException("Unclosed object at index " + index);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ParseObject error at index {index}: {ex.Message}");
            throw;
        }
    }

    private object ParseArray()
    {
        try
        {
            Console.WriteLine($"ParseArray at index: {index}");
            var arr = new List<ValueStruct>();
            SkipWhitespace();
            index++;
            int iteration = 0;

            while (index < content.Length)
            {
                if (iteration++ > 1000000)
                {
                    throw new InvalidOperationException("Infinite loop detected in ParseArray");
                }
                SkipComments();
                SkipWhitespace();

                if (content[index] == ']')
                {
                    index++;
                    Console.WriteLine($"Finished parsing array with {arr.Count} elements");
                    return arr;
                }

                var value = ParseValue();
                arr.Add(value);

                SkipWhitespace();
                if (index < content.Length && content[index] == ',')
                {
                    index++;
                }
            }

            throw new InvalidOperationException("Unclosed array at index " + index);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ParseArray error at index {index}: {ex.Message}");
            throw;
        }
    }
}