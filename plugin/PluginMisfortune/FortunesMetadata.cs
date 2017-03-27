using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;

namespace PluginMisfortune
{
    /// <summary>
    /// A kind of tiny database describing all of the fortune files available.
    /// This class is not thread safe.
    /// </summary>
    public class FortunesMetadata
    {
        /// <summary>
        /// Information for an individual fortune file.
        /// </summary>
        private struct FileMetadata
        {
            /// <summary>
            /// The last time the database for the file was compiled.
            /// If the file was updated more recently than this, we need to
            /// refresh our memory.
            /// </summary>
            public DateTime UpdatedAt;

            /// <summary>
            /// FortuneOffsets[i] is the offset of the i'th element in the file.
            /// </summary>
            public int[] FortuneOffsets;

            /// <summary>
            /// FortuneLengths[i] is the length of the i'th element in the file.
            /// </summary>
            public int[] FortuneLengths;
        }

        /// <summary>
        /// The directory of the fortune files.
        /// </summary>
        [NonSerialized()]
        private string Dir;

        /// <summary>
        /// A map from fileName (without $Dir prefix) to file information.
        /// </summary>
        private Dictionary<string, FileMetadata> FortuneFiles;

        /// <summary>
        /// Create a FortunesMetadata with a particular directory.
        /// </summary>
        /// <param name="dir"></param>
        public FortunesMetadata(string dir)
        {
            dir = Path.GetFullPath(dir);
            Log.Notice("Loading fortunes from '{0}'", dir);

            this.Dir = dir;
            this.FortuneFiles = new Dictionary<string, FileMetadata>();
            this.Refresh();
        }

        /// <summary>
        /// Refresh information about fortune data.
        /// </summary>
        public void Refresh()
        {
            Log.Notice("Refreshing fortunes");
            foreach (string fileName in Directory.GetFiles(this.Dir, "*", SearchOption.AllDirectories)) 
            {
                this.RefreshFile(fileName);
            }
            this.LogSize();
        }

        public void LogSize()
        {
            int totalSize = 0;
            foreach (FileMetadata metadata in this.FortuneFiles.Values)
            {
                totalSize += metadata.FortuneLengths.Sum();
            }
            Log.Notice("Total fortune size: {0}", totalSize);
        }

        /// <summary>
        /// Refresh metadata for an individual file.
        /// </summary>
        /// <param name="fileName"></param>
        public void RefreshFile(string fileName)
        {
            FileMetadata meta;
            if (this.FortuneFiles.ContainsKey(fileName))
            {
                meta = this.FortuneFiles[fileName];
            }
            else
            {
                meta = new FileMetadata();
                meta.UpdatedAt = DateTime.MinValue;
            }

            string fullPath = Path.Combine(this.Dir, fileName);
            FileInfo fileInfo = new FileInfo(fullPath);

            if (meta.UpdatedAt > fileInfo.LastWriteTimeUtc ||
                (fileInfo.Attributes & (FileAttributes.Hidden | FileAttributes.Device | FileAttributes.Directory)) != 0)
            {
                Log.Debug("Skipping refresh of '{0}'", fileName);
                return;
            }

            Log.Notice("Refreshing metadata for file '{0}'", fileName);

            byte[] text = File.ReadAllBytes(fullPath);

            List<int> offsets, lengths;

            TokenizeFortunes(text, out offsets, out lengths);

            meta.FortuneLengths = lengths.ToArray();
            meta.FortuneOffsets = offsets.ToArray();
            meta.UpdatedAt = DateTime.UtcNow;
            this.FortuneFiles[fileName] = meta;
        }

        /// <summary>
        /// A delegate that determines whether fortunes should be read from a
        /// particular file.
        /// </summary>
        /// <param name="fileName">The name of the file, without a directory prefix.</param>
        /// <returns></returns>
        public delegate bool FileMatcher(string fileName);

        /// <summary>
        /// Return the count of fortunes in files matching
        /// </summary>
        /// <param name="matcher"></param>
        /// <returns></returns>
        public int GetFortuneCount(FileMatcher matcher)
        {
            return FortuneFiles
                .Where((pair) => matcher(pair.Key))
                .Aggregate(0, (sum, next) => sum + next.Value.FortuneLengths.Length);
        }

        public string GetRandomMatching(FileMatcher matcher)
        {
            int fortuneCount = this.GetFortuneCount(matcher);
            if (fortuneCount == 0)
            {
                throw new InvalidOperationException("No matching fortune files found.");
            }

            int fortune = new Random().Next(fortuneCount);

            Log.Debug("Selected fortune index {0} ({1} total)", fortune, fortuneCount);

            int currentStart = 0;
            foreach (var pair in this.FortuneFiles.Where((pair) => matcher(pair.Key)))
            {
                var fileName = pair.Key;
                var meta = pair.Value;
                int fileFortuneCount = meta.FortuneLengths.Length;
                if (currentStart <= fortune && fortune < currentStart + fileFortuneCount)
                {
                    int index = fortune - currentStart;
                    return this.GetFortune(fileName, index);
                }
                else
                {
                    currentStart += pair.Value.FortuneLengths.Length;
                }
            }

            throw new InvalidOperationException("Random fortune selection was not contained in a file. This is impossible.");
        }

        /// <summary>
        /// Get the fortune with a particular index from a particular file.
        /// </summary>
        /// <param name="fileName">The fortune filename, unprefixed.</param>
        /// <param name="index">The index of the fortune to return.</param>
        /// <returns>The fortune from the file.</returns>
        public string GetFortune(string fileName, int index)
        {
            var meta = FortuneFiles[fileName];
            Log.Debug("Reading from '{0}', index {1}, offset {2}, length {3}",
                fileName, index, meta.FortuneOffsets[index], meta.FortuneLengths[index]);

            using (var file = new FileStream(Path.Combine(this.Dir, fileName), FileMode.Open, FileAccess.Read))
            {
                byte[] data = new byte[meta.FortuneLengths[index]];
                file.Seek(meta.FortuneOffsets[index], SeekOrigin.Begin);
                file.Read(data, 0, meta.FortuneLengths[index]);
                return Encoding.UTF8.GetString(data);
            }
        }

        /// <summary>
        /// Used for tokenization.
        /// </summary>
        private enum State : byte
        {
            TEXT,
            NEWLINE1,
            PERCENT,
            NEWLINE2
        }

        /// <summary>
        /// Compute the offsets and lengths of all fortunes in the file.
        /// Fortunes are delimited by one of the *utf-8* text sequences "\n%\n","\r\n%\r\n",
        /// "\r\n%\n", "\n%\r\n". (Covering all the bases of split text.)
        /// We work with a byte[] to correctly compute byte offsets.
        /// </summary>
        /// <param name="text">The text to read.</param>
        /// <param name="offsets">The offsets of all fortunes in the file.</param>
        /// <param name="lengths">The lengths of all fortunes in the file.</param>
        public static void TokenizeFortunes(byte[] text, out List<int> offsets, out List<int> lengths)
        {
            offsets = new List<int>();
            lengths = new List<int>();

            if (text.Length == 0)
            {
                return;
            }

            int textStart = 0, newlineStart = 0;
            State state = State.TEXT;
            for (int i = 0; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case (byte)'\r':
                    case (byte)'\n':
                    case (byte)'\f': // why not
                        if (state == State.TEXT)
                        {
                            state = State.NEWLINE1;
                            newlineStart = i;
                        }
                        else if (state == State.PERCENT)
                        {
                            state = State.NEWLINE2;
                        }
                        break;
                    case (byte)'%':
                        if (state == State.NEWLINE1 || state == State.NEWLINE2)
                        {
                            state = State.PERCENT;
                            break;
                        }
                        // If we're text, we're still text
                        // If we're percent, we're still percent
                        break;
                    default:
                        if (state == State.NEWLINE1)
                        {
                            state = State.TEXT;
                        }
                        else if (state == State.NEWLINE2)
                        {
                            // We've gone newline -> percent -> newline!
                            // Starting a new fortune now.
                            offsets.Add(textStart);
                            lengths.Add(newlineStart - textStart);

                            textStart = i;
                            state = State.TEXT;
                        }
                        break;
                }
            }
            // Grab the last fortune in the file.
            if (state == State.TEXT)
            {
                offsets.Add(textStart);
                lengths.Add(text.Length - textStart);
            }
            else if ((state == State.NEWLINE1 || state == State.NEWLINE2)
                && textStart != newlineStart)
            {
                offsets.Add(textStart);
                lengths.Add(newlineStart - textStart);
            }
        }
    }
}
