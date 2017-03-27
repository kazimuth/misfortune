using System.Text;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginMisfortune;
using System.Collections.Generic;

namespace PluginMisfortuneTest
{
    [TestClass]
    public class Tokenize
    {
        [TestMethod]
        public void TestSingle()
        {
            byte[] text = Encoding.UTF8.GetBytes("There is only one fortune.");
            List<int> offsets, lengths;

            FortunesMetadata.TokenizeFortunes(text, out offsets, out lengths);

            Assert.AreEqual(1, offsets.Count, "There should be one fortune offset");
            Assert.AreEqual(1, lengths.Count, "There should be one fortune length");
            Assert.AreEqual(0, offsets[0], "The fortune should start at 0");
            Assert.AreEqual(text.Length, lengths[0], "The fortune should use the whole text length");
        }

        [TestMethod]
        public void TestNone()
        {
            byte[] text = Encoding.UTF8.GetBytes("");
            List<int> offsets, length;
            FortunesMetadata.TokenizeFortunes(text, out offsets, out length);

            Assert.AreEqual(0, offsets.Count, "There should be no offsets");
            Assert.AreEqual(0, length.Count, "There should be no lengths");
        }

        [TestMethod]
        public void TestAllSeparators()
        {
            byte[] text = Encoding.UTF8.GetBytes("\n%\n%%%\r\n%\n");
            List<int> offsets, length;
            FortunesMetadata.TokenizeFortunes(text, out offsets, out length);

            Assert.AreEqual(0, offsets.Count, "There should be no offsets");
            Assert.AreEqual(0, length.Count, "There should be no lengths");
        }

        [TestMethod]
        public void TestMultiple()
        {
            byte[] text = Encoding.UTF8.GetBytes("123\n%\n456\n%\n789");
            List<int> offsets, lengths;
            FortunesMetadata.TokenizeFortunes(text, out offsets, out lengths);

            Assert.AreEqual(3, offsets.Count, "There should be three fortune offsets");
            Assert.AreEqual(3, lengths.Count, "There should be three fortune lengths");
            Assert.AreEqual(0, offsets[0]);
            Assert.AreEqual(6, offsets[1]);
            Assert.AreEqual(12, offsets[2]);
            Assert.AreEqual(3, lengths[0]);
            Assert.AreEqual(3, lengths[1]);
            Assert.AreEqual(3, lengths[2]);
        }

        [TestMethod]
        public void TestUnicode()
        {
            byte[] text = Encoding.UTF8.GetBytes(
                "👌👀 good shit\n%\n (chorus: ʳᶦᵍʰᵗ ᵗʰᵉʳᵉ) mMMMMᎷМ💯");
            byte[] goodChunk1 = Encoding.UTF8.GetBytes("👌👀 good shit");
            byte[] goodChunk2 = Encoding.UTF8.GetBytes(" (chorus: ʳᶦᵍʰᵗ ᵗʰᵉʳᵉ) mMMMMᎷМ💯");
            List<int> offsets, lengths;
            FortunesMetadata.TokenizeFortunes(text, out offsets, out lengths);

            Assert.AreEqual(2, offsets.Count, "There should be two fortune offsets");
            Assert.AreEqual(2, lengths.Count, "There should be two fortune lengths");

            byte[] outChunk1 = text.Skip(offsets[0]).Take(lengths[0]).ToArray();
            CollectionAssert.AreEqual(goodChunk1, outChunk1, "Correct unicode should be extracted");
            byte[] outChunk2 = text.Skip(offsets[1]).Take(lengths[1]).ToArray();
            CollectionAssert.AreEqual(goodChunk2, outChunk2, "Correct unicode should be extracted");
        }

        [TestMethod]
        public void TestContainingNewlines()
        {
            byte[] text = Encoding.UTF8.GetBytes("1\n3\n%\n4\n6\n%\n7\n9");
            List<int> offsets, lengths;
            FortunesMetadata.TokenizeFortunes(text, out offsets, out lengths);

            Assert.AreEqual(3, offsets.Count, "There should be three fortune offsets");
            Assert.AreEqual(3, lengths.Count, "There should be three fortune lengths");
            Assert.AreEqual(0, offsets[0]);
            Assert.AreEqual(6, offsets[1]);
            Assert.AreEqual(12, offsets[2]);
            Assert.AreEqual(3, lengths[0]);
            Assert.AreEqual(3, lengths[1]);
            Assert.AreEqual(3, lengths[2]);
        }
    }
}
