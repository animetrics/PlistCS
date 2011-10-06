using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Testing
{
    [TestClass]
    public class plistTests
    {
        string targetXmlPath = "C:\\Users\\mark\\BinaryPlists\\BinaryPlist\\Testing\\bin\\Debug\\targetXml.plist";
        string targetBinPath = "C:\\Users\\mark\\BinaryPlists\\BinaryPlist\\Testing\\bin\\Debug\\targetBin.plist";
        string sourceXmlPath = "C:\\Users\\mark\\BinaryPlists\\BinaryPlist\\Testing\\bin\\Debug\\sourceXml.plist";
        string sourceBinPath = "C:\\Users\\mark\\BinaryPlists\\BinaryPlist\\Testing\\bin\\Debug\\sourceBin.plist";

        /*[TestMethod]
        public void ReadWriteTest()
        {
            Dictionary<string, object> expected = Plist.readPlist(xmlPath);
            Plist.writeBinary(expected, binPath);
            Dictionary<string, object> actual = Plist.readPlist(binPath);
            CollectionAssert.AreEqual(expected, actual);
        }
        */
        [TestMethod]
        public void ConvertTest()
        {
            Dictionary<string, object> xmlDict = Plist.readPlist(sourceXmlPath);
            Plist.writeBinary(xmlDict, targetBinPath);
            Dictionary<string, object> binDict = Plist.readPlist(targetBinPath);
            Plist.writeXml(binDict, targetXmlPath);
        }
    }
}

