using System.Text;
using System.Linq;

namespace ZuneSocialTagger.Core.IO.Mp4Tagger
{
    public class RawPart : IBasePart
    {
        public byte[] Content { get; set; }
        public string Name { get; set; }

        public virtual byte[] Render()
        {
            var keyNameLength = ByteHelpers.GetBytesAsLi(Name.Length);
            var keyName = Encoding.Default.GetBytes(Name);

            var partContentWithoutLength = new byte[keyNameLength.Length + keyName.Length + Content.Length];
            keyNameLength.CopyTo(partContentWithoutLength, 0);
            keyName.CopyTo(partContentWithoutLength, keyNameLength.Length);
            Content.CopyTo(partContentWithoutLength, keyNameLength.Length + keyName.Length);

            //+4 is for the part length
            byte[] keySize = ByteHelpers.GetBytesAsLi(partContentWithoutLength.Length + 4);

            var completeKvp = new byte[partContentWithoutLength.Length + keySize.Length];
            keySize.CopyTo(completeKvp, 0);
            partContentWithoutLength.CopyTo(completeKvp, keySize.Length);
            
            return completeKvp;
        }
    }
}