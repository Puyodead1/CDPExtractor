using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace CDPExtractor
{
    public class Chump
    {
        public byte[] SIGNATURE = Encoding.ASCII.GetBytes("ACS$");
        private BinaryReader _reader;
        public int FileLength { get; set; }
        public string CurrentAsset { get; set; }
        public string RootPath { get; set; }

        public Chump(string cdpFile)
        {
            using(FileStream fs = new FileStream(cdpFile, FileMode.Open, FileAccess.Read))
            {
                string text = "\r\n";
                RootPath = Path.GetDirectoryName(cdpFile);

                _reader = new BinaryReader(fs);
                ReadHeader();

                int depth = 0;
                long currentPosition = _reader.BaseStream.Position;

                CurrentAsset = "root";
                while (_reader.BaseStream.Position != currentPosition + FileLength)
                {
                    ParseSubTags(depth, ref text, CurrentAsset);
                }

                // write to file
                string output = Path.Join(RootPath, Path.GetFileNameWithoutExtension(cdpFile) + ".txt");
                Console.WriteLine($"Writing to {output}");
                File.WriteAllText(output, text);
            }
        }

        private void ParseSubTags(int depth, ref string text, string parent)
        {
            uint tagLength = _reader.ReadUInt32();
            int tagNameLength = _reader.ReadByte();
            string tagName = Encoding.ASCII.GetString(_reader.ReadBytes(tagNameLength - 1));
            _reader.ReadByte(); // null terminator
            ETagType tagType = (ETagType)_reader.ReadByte();

            int dataLength = (int)(tagLength - tagNameLength - 2);

            text = text + Strings.Space(depth * 2) + tagName;

            switch(tagType)
            {
                case ETagType.Container:
                    text = text + "\r\n" + Strings.Space(depth * 2) + "{\r\n";
                    long currentPosition = _reader.BaseStream.Position;

                    if(parent == "assets")
                    {
                        CurrentAsset = tagName;
                    }
    

                    while (_reader.BaseStream.Position != currentPosition + tagLength - tagNameLength - 2)
                    {
                        ParseSubTags(depth + 1, ref text, tagName);
                    }
                    text = text + Strings.Space(depth * 2) + "}";
                    break;
                case ETagType.Integer:
                    text += Strings.Space(Conversions.ToInteger(Interaction.IIf((int)(40 - tagNameLength - 1) - depth * 2 < 2, 2, (int)(40 - tagNameLength - 1) - depth * 2)));
                    bool flag = false;
                    for (int i = 0; i < dataLength - 1; i += 4)
                    {
                        int value = _reader.ReadInt32();
                        text = Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject(text, Interaction.IIf(flag, ",", "")), value.ToString("G", CultureInfo.InvariantCulture)));
                        flag = true;
                    }
                    break;
                case ETagType.Float:
                    text += Strings.Space(Conversions.ToInteger(Interaction.IIf((int)(40 - tagNameLength - 1) - depth * 2 < 2, 2, (int)(40 - tagNameLength - 1) - depth * 2)));
                    bool flag2 = false;
                    for (int i = 0; i < dataLength; i += 4)
                    {
                        float value = _reader.ReadSingle();
                        text = Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject(text, Interaction.IIf(flag2, ",", "")), value.ToString("G", CultureInfo.InvariantCulture)));
                        flag2 = true;
                    }
                    break;
                case ETagType.String:
                    byte[] bytes = _reader.ReadBytes(dataLength - 1);
                    _reader.ReadByte(); // null terminator

                    text = string.Concat(new string[]
                        {
                            text,
                            Strings.Space(Conversions.ToInteger(Interaction.IIf((int)(40 - tagNameLength - 1) - depth * 2 < 2, 2, (int)(40 - tagNameLength - 1) - depth * 2))),
                            "\"",
                            Encoding.UTF8.GetString(bytes),
                            "\""
                        });
                    break;
                case ETagType.Binary:
                    byte[] data = _reader.ReadBytes(dataLength);
                    //using (LzssStream lzss = new LzssStream(new MemoryStream(data), LzssMode.Decompress, false))
                    //{
                    //    // write to file CurrentAsset + tagName
                    //    string filename = Path.Join(CurrentAsset, tagName);
                    //    Console.WriteLine($"Writing {filename}");
                    //    Directory.CreateDirectory(Path.GetDirectoryName(filename));

                    //    using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
                    //    {
                    //        lzss.CopyTo(fs);
                    //    }
                    //}

                    // write to file CurrentAsset + tagName
                    //string filename = Path.Join(RootPath, CurrentAsset, tagName);
                    //Console.WriteLine($"Writing {filename}");
                    //Directory.CreateDirectory(Path.GetDirectoryName(filename));
                    //File.WriteAllBytes(filename, data);

                    if (CurrentAsset != "root")
                    {
                        string kuid_dir = Path.Join(RootPath, CurrentAsset.Replace(">", "").Replace("<", "").Replace(":", "_"));
                        string path = Path.Join(kuid_dir, tagName);
                        Directory.CreateDirectory(kuid_dir);
                        File.WriteAllBytes(path, data);
                    }

                    text = text + Strings.Space(Conversions.ToInteger(Interaction.IIf((int)(40 - tagNameLength - 1) - depth * 2 < 2, 2, (int)(40 - tagNameLength - 1) - depth * 2))) + "SNIPPED";
                    break;
                case ETagType.Null:
                    _reader.ReadBytes(dataLength);
                    text = text + Strings.Space(Conversions.ToInteger(Interaction.IIf((int)(40 - tagNameLength - 1) - depth * 2 < 2, 2, (int)(40 - tagNameLength - 1) - depth * 2))) + "NULL";
                    break;
                case ETagType.KUID:
                    byte[] d = _reader.ReadBytes(dataLength);
                    string kuid = BitConverter.ToString(d).Replace("-", "");
                    text = text + Strings.Space(Conversions.ToInteger(Interaction.IIf((int)(40 - tagNameLength - 1) - depth * 2 < 2, 2, (int)(40 - tagNameLength - 1) - depth * 2))) + kuid;
                    break;
                default:
                    throw new Exception("Unknown tag type");
            }

            text += "\r\n";
        }

        private void ReadHeader()
        {
            byte[] magic = _reader.ReadBytes(4);
            if (!magic.SequenceEqual(SIGNATURE))
                throw new Exception("Invalid ACS$ magic");

            // skip 8 bytes
            _reader.ReadBytes(8);
            FileLength = _reader.ReadInt32();
        }
    }
}
