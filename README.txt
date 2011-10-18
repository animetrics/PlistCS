-----------------
DESCRIPTION
-----------------

This is a C# Property List (plist) serialization library (MIT license).
It supports both XML and binary versions of the plist format.

plist           C#
__________________________________________________________________________________

string          string 
integer         short, int, long
real            double
dictionary      Dictionary<string, object>
array           List<object>
date            DateTime
data            List<byte>
boolean         bool

-----------------
USAGE
-----------------

See PlistCS/PlistCS/plistTests.cs for examples of reading and
writing all types to both XML and binary.  E.g. to read a plist from disk whose
root node is a dictionary:

		Dictionary<string, object> dict = (Dictionary<string, object>)Plist.readPlist("testBin.plist");

The plist format (binary or XML) is automatically detected so call the same
readPlist method for XML

		Dictionary<string, object> dict = (Dictionary<string, object>)Plist.readPlist("testXml.plist");

To write a plist, e.g. dictionary


            Dictionary<string, object> dict = new Dictionary<string, object>
		    {
			    {"String Example", "Hello There"},
			    {"Integer Example", 1234}
		    };
            Plist.writeXml(dict, "xmlTarget.plist");

and for a binary plist

            Dictionary<string, object> dict = new Dictionary<string, object>
		    {
			    {"String Example", "Hello There"},
			    {"Integer Example", 1234}
		    };
            Plist.writeBinary(dict, "xmlTarget.plist");

The other public methods allow for reading and writing from streams and byte
arrays.  Again, see the test suite code PlistCS/PlistCS/plistTests.cs
for comprehensive examples. 

---------------
AUTHOR
---------------
Mark Tilton, Animetrics Inc.