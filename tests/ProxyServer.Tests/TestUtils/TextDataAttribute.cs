using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit.Sdk;

namespace ProxyServer.Tests.TestUtils
{
    public class TextDataAttribute : DataAttribute
    {
        private readonly string _filePath;

        public TextDataAttribute(string filePath)
        {
            _filePath = filePath;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            if (testMethod == null)
            {
                throw new ArgumentNullException(nameof(testMethod));
            }

            // Load the file
            var fileText = TextDataAttributeHelpers.ReadFile(_filePath);

            var objectList = new List<object[]>();

            objectList.Add(new object[] {fileText});

            return objectList;
        }
    }
    
    public class TextData2Attribute : DataAttribute
    {
        private readonly string _filePath1;
        private readonly string _filePath2;

        public TextData2Attribute(string filePath1, string filePath2)
        {
            _filePath1 = filePath1;
            _filePath2 = filePath2;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            if (testMethod == null)
            {
                throw new ArgumentNullException(nameof(testMethod));
            }

            // Load the file
            var fileText1 = TextDataAttributeHelpers.ReadFile(_filePath1);
            var fileText2 = TextDataAttributeHelpers.ReadFile(_filePath2);

            var objectList = new List<object[]>();

            objectList.Add(new object[] {fileText1, fileText2});

            return objectList;
        }
    }

    internal static class TextDataAttributeHelpers
    {
        public static string ReadFile(string filePath)
        {
            // Get the absolute path to the file
            var path = Path.IsPathRooted(filePath)
                ? filePath
                : Path.GetRelativePath(Directory.GetCurrentDirectory(), filePath);

            if (!File.Exists(path))
            {
                throw new ArgumentException($"Could not find file at path: {path}");
            }

            // Load the file
            return File.ReadAllText(filePath);
        }
    }
}