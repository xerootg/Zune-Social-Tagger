using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZuneSocialTagger.Core.IO;

internal class Program
{
    static int filesProcessed = 0;
    static int partialTagged = 0;
    static int noTags = 0;
    private static string[] KnownExtension = new string[] { "mp3", "wma", "m4a" };
    private static int Main(string[] args)
    {
        var rootFolder = args.Count() == 1 ? args[0] : null;
        if (string.IsNullOrEmpty(rootFolder))
        {
            Console.WriteLine("ERROR: syntax: AutoTaggerCLI.exe {root music path}");
            return 1;
        }
        if (Directory.Exists(args[0]))
        {
            var fileList = GetFiles(rootFolder).ToArray();
            Parallel.ForEach(fileList,HandleFile);

            Console.WriteLine($"INFO: Complete! applied all tags to {filesProcessed}, partial to {partialTagged}, and skipped {noTags} files.");
        }
        else
        {
            Console.WriteLine("ERROR: Root folder does not exist.");
            return 1;
        }
        return 0;
    }

    private static void HandleFile(string file)
    {
        if (KnownExtension.Any(x => file.EndsWith(x, StringComparison.InvariantCultureIgnoreCase)))
        {
            // Console.WriteLine($"DEBUG: opening {file}");
            if (TryConvertMBtoZuneTagObject(file, out var attributes))
            {
                var container = ZuneTagContainerFactory.GetContainer(file);
                container.RemoveZuneAttribute("WM/WMContentID");
                container.RemoveZuneAttribute("WM/WMCollectionID");
                container.RemoveZuneAttribute("WM/WMCollectionGroupID");
                container.RemoveZuneAttribute("ZuneCollectionID");
                container.RemoveZuneAttribute("WM/UniqueFileIdentifier");
                foreach (var attribute in attributes)
                {
                    try
                    {
                        container.RemoveZuneAttribute(attribute.Name);
                        container.AddZuneAttribute(attribute);
                    }
                    catch
                    {
                        Console.WriteLine($"WARN: Tag writing exception for: {file}");
                        partialTagged++;
                    }

                }
                if (attributes.Count != 3)
                {
                    Console.WriteLine($"WARN: wrote {attributes.Count} tags: {file}");
                }
                else { filesProcessed++; }
            }
            else
            {
                noTags++;
                Console.WriteLine($"WARN: No MB tags found: {file}");
            }
            // Console.WriteLine($"DEBUG: Done with {file}");
        }
    }

    static bool TryConvertMBtoZuneTagObject(string file, out List<ZuneAttribute> attributesToAdd)
    {
        attributesToAdd = new List<ZuneAttribute>();
        try
        {
            using (var container = TagLib.File.Create(file))
            {
                var artistId = container.Tag.MusicBrainzArtistId;
                if (!string.IsNullOrEmpty(artistId))
                {
                    attributesToAdd.Add(new ZuneAttribute(ZuneIds.Artist, new Guid(artistId)));
                }

                var albumId = container.Tag.MusicBrainzReleaseId;
                if (!string.IsNullOrEmpty(albumId))
                {
                    attributesToAdd.Add(new ZuneAttribute(ZuneIds.Album, new Guid(albumId)));
                }

                var trackId = container.Tag.MusicBrainzTrackId;
                if (!string.IsNullOrEmpty(trackId))
                {
                    attributesToAdd.Add(new ZuneAttribute(ZuneIds.Track, new Guid(trackId)));
                }
                else if (file.EndsWith(".m4a"))
                {
                    var appleTags = (TagLib.Mpeg4.AppleTag)container.GetTag(TagLib.TagTypes.Apple);
                    trackId = appleTags.GetDashBox("com.apple.iTunes", "MusicBrainz Track Id");
                    if (!string.IsNullOrEmpty(trackId))
                    {
                        attributesToAdd.Add(new ZuneAttribute(ZuneIds.Track, new Guid(trackId)));
                    }
                }
            }
            return attributesToAdd.Count > 0;
        }
        catch { }
        return false;
    }

    static IEnumerable<string> GetFiles(string path)
    {
        Queue<string> queue = new Queue<string>();
        queue.Enqueue(path);
        while (queue.Count > 0)
        {
            path = queue.Dequeue();
            try
            {
                foreach (string subDir in Directory.GetDirectories(path))
                {
                    queue.Enqueue(subDir);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            string[] files = null;
            try
            {
                files = Directory.GetFiles(path);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            if (files != null)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    yield return files[i];
                }
            }
        }
    }
}