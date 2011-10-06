using System.Collections.Generic;
using System.IO;

namespace BinaryPlist
{
    class Program
    {
        static string xmlPath = "C:\\Users\\mark\\BinaryPlists\\BinaryPlist\\Testing\\bin\\Debug\\xmlTest.plist";
        static string binPath = "C:\\Users\\mark\\BinaryPlists\\BinaryPlist\\Testing\\bin\\Debug\\binTest.plist";

        static void Main(string[] args)
        {
            /*Dictionary<string, object> plist = new Dictionary<string, object> { { "Negative Integers", -654 }, 
                                                                                { "Dictionary", new Dictionary<string, object> { {"STRINGS!", "THIS IS A STRING"},
                                                                                                                                 {"More integers...", 1203},
                                                                                                                                 {"Doubles???", -0.001d}} }
                                                                                };*/
            using (StreamWriter writer = new StreamWriter(xmlPath))
            {
                writer.Write(Plist.createXmlFromDictionary(Plist.crateDictionaryFromBinaryFile(binPath)));
            }
            System.Threading.Thread.Sleep(1000);
        }
    }
}
