using System;
using System.IO;
using ZuneSocialTagger.Core.IO.ID3Tagger;
using ZuneSocialTagger.Core.IO.WMATagger;
using File = System.IO.File;
using ZuneSocialTagger.Core.IO.Mp4Tagger;

namespace ZuneSocialTagger.Core.IO
{
    public static class ZuneTagContainerFactory
    {
        public static IZuneTagContainer GetContainer(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(String.Format("File does not exist: {0}", path), path);

            string extension = Path.GetExtension(path);

            if (extension.ToLower() == ".mp3")
            {
                try
                {
                    using (var container = TagLib.File.Create(path))
                    {
                        return new ZuneMP3TagContainer(container);
                    }
                }
                catch (Exception ex)
                {
                    Exception excep = ex;

                    if (ex.InnerException != null)
                        excep = ex.InnerException;

                    throw new AudioFileReadException("Couldn't read: " + path + " Error: " + ex.Message, excep);
                }
            }

            if (extension.ToLower() == ".wma")
            {
                try
                {
                    using (var container = TagLib.File.Create(path))
                    {
                        return new ZuneWMATagContainer(container);
                    }
                }
                catch (Exception ex)
                {
                    throw new AudioFileReadException("Couldn't read: " + path + " Error: " + ex.Message);
                }
            }

            if (extension.ToLower() == ".m4a")
            {
                try
                {
                    using (var container = TagLib.File.Create(path))
                    {
                        return new ZuneMp4TagContainer(container);
                    }
                }
                catch (Exception ex)
                {
                    throw new AudioFileReadException("Couldn't read: " + path + " Error: " + ex.Message);
                }
            }

            throw new AudioFileReadException("Couldn't read: " + path + " Error: " +
                "The " + Path.GetExtension(path) +
                " file extension is not supported with zune social tagger");
        }
    }
}