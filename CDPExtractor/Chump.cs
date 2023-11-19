using System.Text;

namespace CDPExtractor
{
    public class Chump
    {
        public byte[] SIGNATURE = Encoding.ASCII.GetBytes("ACS$");
        private BinaryReader _reader;
        public int FileLength { get; set; }
        public string CurrentAsset { get; set; }

        public Chump(string cdpFile)
        {
            using(FileStream fs = new FileStream(cdpFile, FileMode.Open, FileAccess.Read))
            {
                _reader = new BinaryReader(fs);
                ReadHeader();

                int depth = 0;
                long currentPosition = _reader.BaseStream.Position;
                StringBuilder stringBuilder = new StringBuilder();

                CurrentAsset = "root";
                while (_reader.BaseStream.Position != currentPosition + FileLength)
                {
                    ParseSubTags(depth, stringBuilder, CurrentAsset);
                }

                // write to file
                string output = Path.GetFileNameWithoutExtension(cdpFile) + ".txt";
                Console.WriteLine($"Writing to {output}");
                File.WriteAllText(output, stringBuilder.ToString());
            }
        }

        private void ParseSubTags(int depth, StringBuilder root, string parent)
        {
            string spacing = new string('\t', depth);
            uint tagLength = _reader.ReadUInt32();
            int tagNameLength = _reader.ReadByte();
            string tagName = Encoding.ASCII.GetString(_reader.ReadBytes(tagNameLength - 1));
            _reader.ReadByte(); // null terminator
            ETagType tagType = (ETagType)_reader.ReadByte();

            int dataLength = (int)(tagLength - tagNameLength - 2);

            switch(tagType)
            {
                case ETagType.Container:
                    StringBuilder container = new StringBuilder();
                    container.Append(spacing + tagName + Environment.NewLine + spacing +  "{" + Environment.NewLine);
                    long currentPosition = _reader.BaseStream.Position;
                    

                    while (_reader.BaseStream.Position != currentPosition + tagLength - tagNameLength - 2)
                    {
                        ParseSubTags(depth + 1, container, tagName);
                    }

                    container.Append(spacing + "}" + Environment.NewLine);
                    root.Append(container.ToString());
                    break;
                case ETagType.Integer:
                    for (int i = 0; i < dataLength - 1; i += 4)
                    {
                        int value = _reader.ReadInt32();
                        root.Append(spacing + $"{tagName}\t{value}");
                    }
                    break;
                case ETagType.Float:
                    for (int i = 0; i < dataLength; i += 4)
                    {
                        float value = _reader.ReadSingle();
                        root.Append(spacing + $"{tagName}\t{value}");
                    }
                    break;
                case ETagType.String:
                    string tagString = Encoding.UTF8.GetString(_reader.ReadBytes(dataLength - 1));
                    _reader.ReadByte(); // null terminator
                    
                    root.Append(spacing + $"{tagName}\t{tagString}");
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
                    string filename = Path.Join(CurrentAsset, tagName);
                    Console.WriteLine($"Writing {filename}");
                    Directory.CreateDirectory(Path.GetDirectoryName(filename));
                    File.WriteAllBytes(filename, data);

                    root.Append(spacing + $"{tagName}\tSNIPPED");
                    break;
                case ETagType.Null:
                    _reader.ReadBytes(dataLength);
                    root.Append(spacing + $"{tagName}\tNULL");
                    break;
                case ETagType.KUID:
                    byte[] d = _reader.ReadBytes(dataLength);
                    string kuid = BitConverter.ToString(d).Replace("-", "");
                    root.Append(spacing + $"{tagName}\t{kuid}");
                    break;
                default:
                    throw new Exception("Unknown tag type");
            }

            root.Append(Environment.NewLine);


            if (parent == "assets")
            {
                CurrentAsset = tagName.Replace("<", "").Replace(">", "").Replace(":", "_");
                Console.WriteLine($"Found asset: {tagName}");
            }
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
