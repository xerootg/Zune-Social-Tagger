using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZuneSocialTagger.Core.IO.Mp4Tagger
{
    /// <summary>
    /// Reads / creates the zune Xtra part which the zune software creates inside of the .mp4 file
    /// </summary>
    public static class ZuneXtraParser
    {
        public static byte[] ConstructRawData(IEnumerable<IBasePart> parts)
        {
            var result = new List<byte>();

            foreach (IBasePart part in parts)
            {
                result.AddRange(part.Render());
            }

            return result.ToArray();
        }

        public static IEnumerable<IBasePart> ParseRawData(byte[] atomContents)
        {
            var parts = new List<IBasePart>();

            try
            {
                using (var memStream = new MemoryStream(atomContents))
                using (var binReader = new BinaryReader(memStream))
                {
                    while (binReader.BaseStream.Position < binReader.BaseStream.Length)
                    {
                        //at a minimum we should be able to parse the length, name length and name
                        int partLength = readInt(binReader); //first 4 bytes donates the length of the part

                        if (partLength > binReader.BaseStream.Length - (binReader.BaseStream.Position - 4))
                        {
                            // the length of the payload cannot be larger than the remaining bytes. this is corruption.
                            return parts;
                        }
                        int partNameLength = readInt(binReader); //get length of the part
                        string partName = getPartName(binReader, partNameLength); // get the name of the part

                        int toRead = partLength - (4 + 4 + partName.Length);
                        byte[] restOfPart = new byte[toRead];
                        binReader.Read(restOfPart, 0, toRead);

                        try
                        {
                            switch (GetPartType(restOfPart))
                            {
                                case 72:
                                    {
                                        parts.Add(new GuidPart(partName, restOfPart));
                                        break;
                                    }
                                case 8: // unicode
                                case 19: // int64
                                case 21: // filetime
                                default:
                                    {
                                        parts.Add(new RawPart { Name = partName, Content = restOfPart });
                                        break;
                                    }

                            }
                        }
                        catch (Exception ex)
                        {
                            parts.Add(new RawPart() { Name = partName, Content = restOfPart });
                        }
                    }
                }
            } catch (Exception e)
            {
                // I hate this format
                Console.WriteLine($"ERROR: Xtra atom is borked, nuking it into orbit: {e}");
                parts = new List<IBasePart>();
            }
            return parts;

        }

        private static int GetValueCount(byte[] partData)
        {
            using (var memStream = new MemoryStream(partData))
            using (var binReader = new BinaryReader(memStream))
            {
                return readInt(binReader); // number of underlying values
            }
        }

        private static int GetPartType(byte[] partData)
        {
            using (var memStream = new MemoryStream(partData))
            using (var binReader = new BinaryReader(memStream))
            {
                int partCount = readInt(binReader); // number of underlying values.

                // we only support single value entities, so -1 causes default in the switch, and therefore raw, which just doesnt care
                if (partCount == 1)
                {
                    _ = readInt(binReader); // this is the size of this value, which will be parsed in the type handler
                    return readShort(binReader); // this is a 2 byte id which tells us what it is
                }
            }
            return -1;
        }

        private static int readInt(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);

            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        private static short readShort(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(2);

            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        /// <summary>
        /// The atom name is always 4 bytes long and is a string
        /// </summary>
        /// <returns></returns>
        private static string getPartName(BinaryReader reader, int length)
        {
            var atomNameBuf = reader.ReadBytes(length);

            if (atomNameBuf.Count() > 0 && atomNameBuf[0] != 0)
            {
                return Encoding.Default.GetString(atomNameBuf);
            }
            else
            {
                return String.Empty;
            }
        }
    }
}