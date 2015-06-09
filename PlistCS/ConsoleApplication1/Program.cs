using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlistCS;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var dict = (Dictionary<String, object>) Plist.readPlist("C:\\Users\\Justin\\Code Projects\\PlistCS\\iTunes Music Library.xml");
            dict = (Dictionary<String, object>) dict["Tracks"];
            foreach(KeyValuePair<String, Object> track in dict)
            {
                var trackDict = (Dictionary<String, object>) track.Value;
                foreach(KeyValuePair<String, object> entry in trackDict)
                {
                    Console.WriteLine(entry.Value);
                }
            }
            Console.ReadKey();

            Plist.writeXml(dict, "C:\\Users\\Justin\\Code Projects\\PlistCS\\test.xml");
        }
    }
}
