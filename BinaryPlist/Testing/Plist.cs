using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

public static class Plist
{
    private static List<int> offsetTable = new List<int>();
    private static List<byte> objectTable = new List<byte>();
    private static int refCount;
    private static int objRefSize;
    private static int offsetByteSize;
    private static long offsetTableOffset;

    #region Public Functions

    public static Dictionary<string, object> readPlist(string path)
    {
        using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read)))
        {
            long magicHeader = BitConverter.ToInt64(reader.ReadBytes(8), 0);
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            if (magicHeader == 3472403351741427810)
            {
                return readBinary(reader.ReadBytes((int)reader.BaseStream.Length));
            }
            else
            {
                XmlDocument xml = new XmlDocument();
                xml.LoadXml(System.Text.Encoding.UTF8.GetString(reader.ReadBytes((int)reader.BaseStream.Length)));
                return readXml(xml);
            }
        }
    }

    public static Dictionary<string, object> readPlist(Stream stream)
    {
        byte[] magicHeader = new byte[8];
        stream.Read(magicHeader, 0, 8);
        stream.Seek(0, SeekOrigin.Begin);
        if (BitConverter.ToInt64(magicHeader, 0) == 3472403351741427810)
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                byte[] data = reader.ReadBytes((int)reader.BaseStream.Length);
                return readBinary(data);
            }
        }
        else
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(stream);
            return readXml(xml);
        }
    }

    public static Dictionary<string, object> readPlist(byte[] data)
    {
        List<byte> byteList = data.ToList();
        if (BitConverter.ToInt64(byteList.GetRange(0, 8).ToArray(), 0) == 3472403351741427810)
        {
            return readBinary(data);
        }
        else
        {
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(System.Text.Encoding.UTF8.GetString(data));
            return readXml(xml);
        }
    }

    public static void writeXml(Dictionary<string, object> dictionary, string path)
    {
        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.Write(writeXml(dictionary));
        }
    }

    public static void writeXml(Dictionary<string, object> dictionary, Stream stream)
    {
        using (StreamWriter writer = new StreamWriter(stream))
        {
            writer.Write(writeXml(dictionary));
        }
    }

    public static string writeXml(Dictionary<string, object> dictionary)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.Encoding = new System.Text.UTF8Encoding(false);
            xmlWriterSettings.ConformanceLevel = ConformanceLevel.Document;
            xmlWriterSettings.Indent = true;

            using (XmlWriter xmlWriter = XmlWriter.Create(ms, xmlWriterSettings))
            {
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteComment("DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" " + "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"");
                xmlWriter.WriteStartElement("plist");
                xmlWriter.WriteAttributeString("version", "1.0");
                writeDictionaryValues(dictionary, xmlWriter);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();
                xmlWriter.Flush();
                xmlWriter.Close();
                return System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }

    public static void writeBinary(Dictionary<string, object> dictionary, string path)
    {
        using (BinaryWriter writer = new BinaryWriter(new FileStream(path, FileMode.Create)))
        {
            writer.Write(writeBinary(dictionary));
        }
    }

    public static void writeBinary(Dictionary<string, object> dictionary, Stream stream)
    {
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(writeBinary(dictionary));
        }
    }

    public static byte[] writeBinary(Dictionary<string, object> dictionary)
    {
        offsetTable.Clear();
        objectTable.Clear();
        refCount = 0;
        objRefSize = 0;
        offsetByteSize = 0;
        offsetTableOffset = 0;

        int totalRefs = countDictionary(dictionary);

        refCount = totalRefs;

        objRefSize = RegulateNullBytes(BitConverter.GetBytes(refCount)).Length;

        writeBinaryDictionary(dictionary);

        writeBinaryString("bplist00", false);

        offsetTableOffset = (long)objectTable.Count;

        offsetTable.Add(objectTable.Count - 8);

        offsetByteSize = RegulateNullBytes(BitConverter.GetBytes(offsetTable.Last())).Length;

        List<byte> offsetBytes = new List<byte>();

        offsetTable.Reverse();

        for (int i = 0; i < offsetTable.Count; i++)
        {
            offsetTable[i] = objectTable.Count - offsetTable[i];
            byte[] buffer = RegulateNullBytes(BitConverter.GetBytes(offsetTable[i]), offsetByteSize);
            Array.Reverse(buffer);
            offsetBytes.AddRange(buffer);
        }

        objectTable.AddRange(offsetBytes);

        objectTable.AddRange(new byte[6]);
        objectTable.Add(Convert.ToByte(offsetByteSize));
        objectTable.Add(Convert.ToByte(objRefSize));
        objectTable.AddRange(BitConverter.GetBytes((long)totalRefs + 1).Reverse());
        objectTable.AddRange(BitConverter.GetBytes((long)0));
        objectTable.AddRange(BitConverter.GetBytes(offsetTableOffset).Reverse());

        return objectTable.ToArray();
    }

    #endregion

    #region Private Functions

    private static Dictionary<string, object> readXml(XmlDocument xml)
    {
        XmlNode rootNode = xml.DocumentElement.ChildNodes[0];
        return (Dictionary<string, object>)parse(rootNode);
    }

    private static Dictionary<string, object> readBinary(byte[] data)
    {
        offsetTable.Clear();
        List<byte> offsetTableBytes = new List<byte>();
        objectTable.Clear();
        refCount = 0;
        objRefSize = 0;
        offsetByteSize = 0;
        offsetTableOffset = 0;

        List<byte> bList = data.ToList();

        List<byte> trailer = bList.GetRange(bList.Count - 32, 32);

        parseTrailer(trailer);

        objectTable = bList.GetRange(0, (int)offsetTableOffset);

        offsetTableBytes = bList.GetRange((int)offsetTableOffset, bList.Count - (int)offsetTableOffset - 32);

        parseOffsetTable(offsetTableBytes);

        return (Dictionary<string, object>)parseBinaryDictionary(0);
    }

    private static Dictionary<string, object> parseDictionary(XmlNode node)
    {
        XmlNodeList children = node.ChildNodes;
        if (children.Count % 2 != 0)
        {
            throw new DataMisalignedException("Dictionary elements must have an even number of child nodes");
        }

        Dictionary<string, object> dict = new Dictionary<string, object>();

        for (int i = 0; i < children.Count; i += 2)
        {
            XmlNode keynode = children[i];
            XmlNode valnode = children[i + 1];

            if (keynode.Name != "key")
            {
                throw new ApplicationException("expected a key node");
            }

            object result = parse(valnode);

            if (result != null)
            {
                dict.Add(keynode.InnerText, result);
            }
        }

        return dict;
    }

    private static List<object> parseArray(XmlNode node)
    {
        List<object> array = new List<object>();

        foreach (XmlNode child in node.ChildNodes)
        {
            object result = parse(child);
            if (result != null)
            {
                array.Add(result);
            }
        }

        return array;
    }

    private static void composeArray(List<object> value, XmlWriter writer)
    {
        writer.WriteStartElement("array");
        foreach (object obj in value)
        {
            compose(obj, writer);
        }
        writer.WriteEndElement();
    }

    private static object parse(XmlNode node)
    {
        switch (node.Name)
        {
            case "dict":
                return parseDictionary(node);
            case "array":
                return parseArray(node);
            case "string":
                return node.InnerText;
            case "integer":
                return Convert.ToInt32(node.InnerText);
            case "real":
                return Convert.ToDouble(node.InnerText);
            case "false":
                return false;
            case "true":
                return true;
            case "null":
                return null;
            case "date":
                return XmlConvert.ToDateTime(node.InnerText, XmlDateTimeSerializationMode.Utc);
            case "data":
                return Convert.FromBase64String(node.InnerText);
        }

        throw new ApplicationException(String.Format("Plist Node `{0}' is not supported", node.Name));
    }

    private static void compose(object value, XmlWriter writer)
    {
        switch (value.GetType().ToString())
        {
            case "System.Collections.Generic.Dictionary`2[System.String,System.Object]":
                writeDictionaryValues((Dictionary<string, object>)value, writer);
                break;

            case "System.Collections.Generic.List`1[System.Object]":
                composeArray((List<object>)value, writer);
                break;

            case "System.Byte[]":
                writer.WriteElementString("data", Convert.ToBase64String((Byte[])value));
                break;

            case "System.Double":
                writer.WriteElementString("real", value.ToString());
                break;

            case "System.Int32":
                writer.WriteElementString("integer", value.ToString());
                break;

            case "System.String":
                writer.WriteElementString("string", value.ToString());
                break;

            case "System.DateTime":
                DateTime time = (DateTime)value;
                string theString = XmlConvert.ToString(time, XmlDateTimeSerializationMode.Utc);
                writer.WriteElementString("date", theString);//, "yyyy-MM-ddTHH:mm:ssZ"));
                break;

            case "System.Boolean":
                writer.WriteElementString(value.ToString().ToLower(), "");
                break;

            default:
                throw new Exception(String.Format("Value type '{0}' is unhandled", value.GetType().ToString()));
        }
    }

    private static void writeDictionaryValues(Dictionary<string, object> dictionary, XmlWriter writer)
    {
        writer.WriteStartElement("dict");
        foreach (string key in dictionary.Keys)
        {
            object value = dictionary[key];
            writer.WriteElementString("key", key);
            compose(value, writer);
        }
        writer.WriteEndElement();
    }

    private static int countDictionary(Dictionary<string, object> dictionary)
    {
        int count = 0;
        foreach (string key in dictionary.Keys)
        {
            count++;
            switch (dictionary[key].GetType().ToString())
            {
                case "System.Collections.Generic.Dictionary`2[System.String,System.Object]":
                    count += (countDictionary((Dictionary<string, object>)dictionary[key]) + 1);
                    break;
                case "System.Collections.Generic.List`1[System.Object]":
                    count += (countArray((List<object>)dictionary[key]) + 1);
                    break;
                default:
                    count++;
                    break;
            }
        }
        return count;
    }

    private static int countArray(List<object> array)
    {
        int count = 0;
        foreach (object obj in array)
        {
            switch (obj.GetType().ToString())
            {
                case "System.Collections.Generic.Dictionary`2[System.String,System.Object]":
                    count += (countDictionary((Dictionary<string, object>)obj) + 1);
                    break;
                case "System.Collections.Generic.List`1[System.Object]":
                    count += (countArray((List<object>)obj) + 1);
                    break;
                default:
                    count++;
                    break;
            }
        }
        return count;
    }

    private static byte[] writeBinaryDictionary(Dictionary<string, object> dictionary)
    {
        List<byte> buffer = new List<byte>();
        List<byte> header = new List<byte>();
        List<int> refs = new List<int>();
        for (int i = dictionary.Count - 1; i >= 0; i--)
        {
            composeBinary(dictionary.Values.ToArray()[i]);
            offsetTable.Add(objectTable.Count);
            refs.Add(refCount);
            refCount--;
        }
        for (int i = dictionary.Count - 1; i >= 0; i--)
        {
            composeBinary(dictionary.Keys.ToArray()[i]);//);
            offsetTable.Add(objectTable.Count);
            refs.Add(refCount);
            refCount--;
        }

        if (dictionary.Count < 15)
        {
            header.Add(Convert.ToByte(0xD0 | Convert.ToByte(dictionary.Count)));
        }
        else
        {
            header.Add(0xD0 | 0xf);
            header.AddRange(writeBinaryInteger(dictionary.Count, false));
        }


        foreach (int val in refs)
        {
            byte[] refBuffer = RegulateNullBytes(BitConverter.GetBytes(val), objRefSize);
            Array.Reverse(refBuffer);
            buffer.InsertRange(0, refBuffer);
        }

        buffer.InsertRange(0, header);


        objectTable.InsertRange(0, buffer);

        return buffer.ToArray();
    }

    private static byte[] composeBinaryArray(List<object> objects)
    {
        List<byte> buffer = new List<byte>();
        List<byte> header = new List<byte>();
        List<int> refs = new List<int>();

        for (int i = objects.Count - 1; i >= 0; i--)
        {
            composeBinary(objects[i]);
            offsetTable.Add(objectTable.Count);
            refs.Add(refCount);
            refCount--;
        }

        if (objects.Count < 15)
        {
            header.Add(Convert.ToByte(0xA0 | Convert.ToByte(objects.Count)));
        }
        else
        {
            header.Add(0xA0 | 0xf);
            header.AddRange(writeBinaryInteger(objects.Count, false));
        }

        foreach (int val in refs)
        {
            byte[] refBuffer = RegulateNullBytes(BitConverter.GetBytes(val), objRefSize);
            Array.Reverse(refBuffer);
            buffer.InsertRange(0, refBuffer);
        }

        buffer.InsertRange(0, header);

        objectTable.InsertRange(0, buffer);

        return buffer.ToArray();
    }

    private static byte[] composeBinary(object obj)
    {
        byte[] value;
        switch (obj.GetType().ToString())
        {
            case "System.Collections.Generic.Dictionary`2[System.String,System.Object]":
                value = writeBinaryDictionary((Dictionary<string, object>)obj);
                return value;

            case "System.Collections.Generic.List`1[System.Object]":
                value = composeBinaryArray((List<object>)obj);
                return value;

            case "System.Byte[]":
                value = writeBinaryByteArray((byte[])obj);
                return value;

            case "System.Double":
                value = writeBinaryDouble((double)obj);
                return value;

            case "System.Int32":
                value = writeBinaryInteger((int)obj, true);
                return value;

            case "System.String":
                value = writeBinaryString((string)obj, true);
                return value;

            case "System.DateTime":
                value = writeBinaryDate((DateTime)obj);
                return value;

            case "System.Boolean":
                value = writeBinaryBool((bool)obj);
                return value;

            default:
                return new byte[0];
        }
    }

    public static byte[] writeBinaryDate(DateTime obj)
    {
        List<byte> buffer = RegulateNullBytes(BitConverter.GetBytes(PlistDateConverter.ConvertToAppleTimeStamp(obj)), 8).ToList();
        if (BitConverter.IsLittleEndian)
            buffer.Reverse();
        buffer.Insert(0, 0x33);
        objectTable.InsertRange(0, buffer);
        return buffer.ToArray();
    }

    public static byte[] writeBinaryBool(bool obj)
    {
        List<byte> buffer = new byte[1] { (bool)obj ? (byte)9 : (byte)8 }.ToList();
        objectTable.InsertRange(0, buffer);
        return buffer.ToArray();
    }

    private static byte[] writeBinaryInteger(int value, bool write)
    {
        List<byte> buffer = BitConverter.GetBytes((long)value).ToList();
        buffer = RegulateNullBytes(buffer.ToArray()).ToList();
        while (buffer.Count != Math.Pow(2, Math.Log(buffer.Count) / Math.Log(2)))
            buffer.Add(0);
        int header = 0x10 | (int)(Math.Log(buffer.Count) / Math.Log(2));

        if (BitConverter.IsLittleEndian)
            buffer.Reverse();

        buffer.Insert(0, Convert.ToByte(header));

        if (write)
            objectTable.InsertRange(0, buffer);

        return buffer.ToArray();
    }

    private static byte[] writeBinaryDouble(double value)
    {
        List<byte> buffer = RegulateNullBytes(BitConverter.GetBytes(value), 4).ToList();
        while (buffer.Count != Math.Pow(2, Math.Log(buffer.Count) / Math.Log(2)))
            buffer.Add(0);
        int header = 0x20 | (int)(Math.Log(buffer.Count) / Math.Log(2));

        if (BitConverter.IsLittleEndian)
            buffer.Reverse();

        buffer.Insert(0, Convert.ToByte(header));

        objectTable.InsertRange(0, buffer);

        return buffer.ToArray();
    }

    private static byte[] writeBinaryByteArray(byte[] value)
    {
        List<byte> buffer = value.ToList();
        List<byte> header = new List<byte>();
        if (value.Length < 15)
        {
            header.Add(Convert.ToByte(0x40 | Convert.ToByte(value.Length)));
        }
        else
        {
            header.Add(0x40 | 0xf);
            header.AddRange(writeBinaryInteger(buffer.Count, false));
        }

        buffer.InsertRange(0, header);

        objectTable.InsertRange(0, buffer);

        return buffer.ToArray();
    }

    private static byte[] writeBinaryString(string value, bool head)
    {
        List<byte> buffer = new List<byte>();
        List<byte> header = new List<byte>();
        foreach (char chr in value.ToCharArray())
            buffer.Add(Convert.ToByte(chr));

        if (head)
        {
            if (value.Length < 15)
            {
                header.Add(Convert.ToByte(0x50 | Convert.ToByte(value.Length)));
            }
            else
            {
                header.Add(0x50 | 0xf);
                header.AddRange(writeBinaryInteger(buffer.Count, false));
            }
        }

        buffer.InsertRange(0, header);

        objectTable.InsertRange(0, buffer);

        return buffer.ToArray();
    }

    private static byte[] RegulateNullBytes(byte[] value)
    {
        return RegulateNullBytes(value, 1);
    }

    private static byte[] RegulateNullBytes(byte[] value, int minBytes)
    {
        Array.Reverse(value);
        List<byte> bytes = value.ToList();
        for (int i = 0; i < bytes.Count; i++)
        {
            if (bytes[i] == 0 && bytes.Count > minBytes)
            {
                bytes.Remove(bytes[i]);
                i--;
            }
            else
                break;
        }

        if (bytes.Count < minBytes)
        {
            int dist = minBytes - bytes.Count;
            for (int i = 0; i < dist; i++)
                bytes.Insert(0, 0);
        }

        value = bytes.ToArray();
        Array.Reverse(value);
        return value;
    }

    private static void parseTrailer(List<byte> trailer)
    {
        offsetByteSize = BitConverter.ToInt32(RegulateNullBytes(trailer.GetRange(6, 1).ToArray(), 4), 0);
        objRefSize = BitConverter.ToInt32(RegulateNullBytes(trailer.GetRange(7, 1).ToArray(), 4), 0);
        byte[] refCountBytes = trailer.GetRange(12, 4).ToArray();
        Array.Reverse(refCountBytes);
        refCount = BitConverter.ToInt32(refCountBytes, 0);
        byte[] offsetTableOffsetBytes = trailer.GetRange(24, 8).ToArray();
        Array.Reverse(offsetTableOffsetBytes);
        offsetTableOffset = BitConverter.ToInt64(offsetTableOffsetBytes, 0);
    }

    private static void parseOffsetTable(List<byte> offsetTableBytes)
    {
        for (int i = 0; i < offsetTableBytes.Count; i += offsetByteSize)
        {
            byte[] buffer = offsetTableBytes.GetRange(i, offsetByteSize).ToArray();
            Array.Reverse(buffer);
            offsetTable.Add(BitConverter.ToInt32(RegulateNullBytes(buffer, 4), 0));
        }
    }

    private static object parseBinaryDictionary(int objRef)
    {
        Dictionary<string, object> buffer = new Dictionary<string, object>();
        List<int> refs = new List<int>();
        int refCount = 0;

        byte dictByte = objectTable[offsetTable[objRef]];

        refCount = getCount(offsetTable[objRef], dictByte);

        int refStartPosition;

        if (refCount < 15)
            refStartPosition = offsetTable[objRef] + 1;
        else
            refStartPosition = offsetTable[objRef] + 2 + RegulateNullBytes(BitConverter.GetBytes(refCount), 1).Length;

        for (int i = refStartPosition; i < refStartPosition + refCount * 2 * objRefSize; i += objRefSize)
        {
            byte[] refBuffer = objectTable.GetRange(i, objRefSize).ToArray();
            Array.Reverse(refBuffer);
            refs.Add(BitConverter.ToInt32(RegulateNullBytes(refBuffer, 4), 0));
        }

        for (int i = 0; i < refCount; i++)
        {
            buffer.Add((string)parseBinary(refs[i]), parseBinary(refs[i + refCount]));
        }

        return buffer;
    }

    private static object parseBinaryArray(int objRef)
    {
        List<object> buffer = new List<object>();
        List<int> refs = new List<int>();
        int refCount = 0;

        byte arrayByte = objectTable[offsetTable[objRef]];

        refCount = getCount(offsetTable[objRef], arrayByte);

        int refStartPosition;

        if (refCount < 15)
            refStartPosition = offsetTable[objRef] + 1;
        else
            //The following integer has a header aswell so we increase the refStartPosition by two to account for that.
            refStartPosition = offsetTable[objRef] + 2 + RegulateNullBytes(BitConverter.GetBytes(refCount), 1).Length;

        for (int i = refStartPosition; i < refStartPosition + refCount * objRefSize; i += objRefSize)
        {
            byte[] refBuffer = objectTable.GetRange(i, objRefSize).ToArray();
            Array.Reverse(refBuffer);
            refs.Add(BitConverter.ToInt32(RegulateNullBytes(refBuffer, 4), 0));
        }

        for (int i = 0; i < refCount; i++)
        {
            buffer.Add(parseBinary(refs[i]));
        }

        return buffer;
    }

    private static int getCount(int bytePosition, byte headerByte)
    {
        byte headerByteTrail = Convert.ToByte(headerByte & 0xf);
        if (headerByteTrail < 15)
            return headerByteTrail;
        else
        {
            return (int)parseBinaryInt(bytePosition + 1);
        }
    }

    private static object parseBinary(int objRef)
    {
        byte header = objectTable[offsetTable[objRef]];
        switch (header & 0xF0)
        {
            case 0:
                {
                    //If the byte is
                    //0 return null
                    //9 return true
                    //8 return false
                    return (objectTable[offsetTable[objRef]] == 0) ? (object)null : ((objectTable[offsetTable[objRef]] == 9) ? true : false);
                }
            case 0x10:
                {
                    return parseBinaryInt(offsetTable[objRef]);
                }
            case 0x20:
                {
                    return parseBinaryReal(offsetTable[objRef]);
                }
            case 0x30:
                {
                    return parseBinaryDate(offsetTable[objRef]);
                }
            case 0x40:
                {
                    return parseBinaryByteArray(offsetTable[objRef]);
                }
            case 0x50:
                {
                    return parseBinaryString(offsetTable[objRef]);
                }
            case 0xD0:
                {
                    return parseBinaryDictionary(objRef);
                }
            case 0xA0:
                {
                    return parseBinaryArray(objRef);
                }
        }
        throw new Exception("This type is not supported");
    }

    public static object parseBinaryDate(int headerPosition)
    {
        byte[] buffer = objectTable.GetRange(headerPosition + 1, 8).ToArray();
        if(BitConverter.IsLittleEndian)
            Array.Reverse(buffer);
        double appleTime = BitConverter.ToDouble(buffer, 0);
        DateTime result = PlistDateConverter.ConvertFromAppleTimeStamp(appleTime);
        return result;
    }

    private static object parseBinaryInt(int headerPosition)
    {
        byte header = objectTable[headerPosition];
        int byteCount = (int)Math.Pow(2, header & 0xf);
        byte[] buffer = objectTable.GetRange(headerPosition + 1, byteCount).ToArray();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(buffer);

        return BitConverter.ToInt32(RegulateNullBytes(buffer, 4), 0);
    }

    private static object parseBinaryReal(int headerPosition)
    {
        byte header = objectTable[headerPosition];
        int byteCount = (int)Math.Pow(2, header & 0xf);
        byte[] buffer = objectTable.GetRange(headerPosition + 1, byteCount).ToArray();
        Array.Reverse(buffer);

        return BitConverter.ToDouble(RegulateNullBytes(buffer, 8), 0);
    }

    private static object parseBinaryString(int headerPosition)
    {
        byte headerByte = objectTable[headerPosition];
        int charCount = getCount(headerPosition, headerByte);
        int charStartPosition;
        if (charCount < 15)
            charStartPosition = headerPosition + 1;
        else
            charStartPosition = headerPosition + 2 + RegulateNullBytes(BitConverter.GetBytes(charCount), 1).Length;
        string buffer = "";
        foreach (byte byt in objectTable.GetRange(charStartPosition, charCount))
        {
            buffer += Convert.ToChar(byt);
        }
        return buffer;
    }

    private static object parseBinaryByteArray(int headerPosition)
    {
        byte headerByte = objectTable[headerPosition];
        int byteCount = getCount(headerPosition, headerByte);
        int byteStartPosition;
        if (byteCount < 15)
            byteStartPosition = headerPosition + 1;
        else
            byteStartPosition = headerPosition + 2 + RegulateNullBytes(BitConverter.GetBytes(byteCount), 1).Length;
        return objectTable.GetRange(byteStartPosition, byteCount).ToArray();
    }

    #endregion
}

public static class PlistDateConverter
{
    public static long timeDifference = 978307200;

    public static long GetAppleTime(long unixTime)
    {
        return unixTime - timeDifference;
    }

    public static DateTime ConvertFromAppleTimeStamp(double timestamp)
    {
        DateTime origin = new DateTime(2001, 1, 1, 0, 0, 0, 0);
        return origin.AddSeconds(timestamp);
    }

    public static double ConvertToAppleTimeStamp(DateTime date)
    {
        DateTime begin = new DateTime(2001, 1, 1, 0, 0, 0, 0);
        TimeSpan diff = date - begin;
        return Math.Floor(diff.TotalSeconds);
    }

    public static long GetUnixTime(long appleTime)
    {
        return appleTime + timeDifference;
    }
}