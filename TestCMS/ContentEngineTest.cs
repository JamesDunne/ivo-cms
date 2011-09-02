using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IVO.Definition.Models;
using IVO.CMS;

namespace TestCMS
{
    [TestClass]
    public class ContentEngineTest
    {
        [TestMethod]
        public void TestRenderBlob()
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<a><b/><c/></a><b></b>"));
            var ce = new ContentEngine();
            var frag = ce.RenderBlob(bl);
            Console.WriteLine((string)frag);
        }
    }
}
