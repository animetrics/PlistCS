using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PlistCS;

namespace Testing
{
    [TestClass]
    public class plistTests
    {
        string targetXmlPath = "targetXml.plist";
        string targetBinPath = "targetBin.plist";
        string sourceXmlPath = "testXml.plist";
        string sourceBinPath = "testBin.plist";
        string sourceImage   = "testImage.jpg";

        private Dictionary<string, object> CreateDictionary()
        {
            const int largeCollectionSize = 18;

            Dictionary<string, object> dict = new Dictionary<string, object>();
            Dictionary<string, object> largeDict = new Dictionary<string, object>();
            List<object> largeArray = new List<object>();

            for (int i = 0; i < largeCollectionSize; i++)
            {
                largeArray.Add(i);
                string key = i.ToString();
                if (i < 10)
                    key = "0" + i.ToString();
                largeDict.Add(key, i);
            }

            using (BinaryReader br = new BinaryReader(File.OpenRead(sourceImage)))
            {
                dict.Add("testImage", br.ReadBytes((int)br.BaseStream.Length));
            }
            dict.Add("testDate", PlistDateConverter.ConvertFromAppleTimeStamp(338610664L));
            dict.Add("testInt", -3455);
            dict.Add("testDouble", 1.34223d);
            dict.Add("testBoolTrue", true);
            dict.Add("testBoolFalse", false);
            dict.Add("testString", "hello there");
            dict.Add("testArray", new List<object> { 34, "string item in array" });
            dict.Add("testArrayLarge", largeArray);
            dict.Add("testDict", new Dictionary<string, object> { { "test string", "inner dict item" } });
            dict.Add("testDictLarge", largeDict);

            return dict;
        }

        private void CheckDictionary(Dictionary<string, object> dict)
        {
            Dictionary<string, object> actualDict = CreateDictionary();
            Assert.AreEqual(dict["testDate"], actualDict["testDate"], "Dates do not correspond.");
            Assert.AreEqual(dict["testInt"], actualDict["testInt"], "Integers do not correspond.");
            Assert.AreEqual(dict["testDouble"], actualDict["testDouble"], "Reals do not correspond.");
            Assert.AreEqual(dict["testBoolTrue"], actualDict["testBoolTrue"], "BoolTrue's do not correspond.");
            Assert.AreEqual(dict["testBoolFalse"], actualDict["testBoolFalse"], "BoolFalse's do not correspond.");
            Assert.AreEqual(dict["testString"], actualDict["testString"], "Dates do not correspond.");
            CollectionAssert.AreEquivalent((byte[])dict["testImage"], (byte[])actualDict["testImage"], "Images do not correspond");
            CollectionAssert.AreEquivalent((List<object>)dict["testArray"], (List<object>)actualDict["testArray"], "Arrays do not correspond");
            CollectionAssert.AreEquivalent((List<object>)dict["testArrayLarge"], (List<object>)actualDict["testArrayLarge"], "Large arrays do not correspond.");
            CollectionAssert.AreEquivalent((Dictionary<string, object>)dict["testDict"], (Dictionary<string, object>)actualDict["testDict"], "Dictionaries do not correspond.");
            CollectionAssert.AreEquivalent((Dictionary<string, object>)dict["testDictLarge"], (Dictionary<string, object>)actualDict["testDictLarge"], "Large dictionaries do not correspond.");
        }

        [TestMethod]
        public void ReadBinary()
        {
            CheckDictionary((Dictionary<string, object>)Plist.readPlist(sourceBinPath));
        }

        [TestMethod]
        public void ReadXml()
        {
            CheckDictionary((Dictionary<string, object>)Plist.readPlist(sourceXmlPath));
        }

        [TestMethod]
        public void WriteBinary()
        {
            Plist.writeBinary(CreateDictionary(), targetBinPath);
            CheckDictionary((Dictionary<string, object>)Plist.readPlist(targetBinPath));
        }

        [TestMethod]
        public void WriteXml()
        {
            Plist.writeXml(CreateDictionary(), targetXmlPath);
            CheckDictionary((Dictionary<string, object>)Plist.readPlist(targetXmlPath));
        }

        [TestMethod]
        public void ReadWriteBinaryByteArray()
        {
            CheckDictionary((Dictionary<string, object>)Plist.readPlist(Plist.writeBinary(CreateDictionary())));
        }
    }
}