using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Asynq;
using IVO.Definition.Repositories;
using IVO.CMS;
using IVO.CMS.Providers.CustomElements;
using IVO.Implementation.FileSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace TestCMS
{
    [TestClass]
    public class FileSystemTests : CommonTest.ContentEngineTestMethods
    {
        public void cleanUp(FileSystem system)
        {
            // Clean up:
            if (system.Root.Exists)
                system.Root.Delete(recursive: true);
        }

        private FileSystem getFileSystem()
        {
            string tmpPath = System.IO.Path.GetTempPath();
            string tmpRoot = System.IO.Path.Combine(tmpPath, "ivo");

            // Delete our temporary 'ivo' folder:
            var tmpdi = new DirectoryInfo(tmpRoot);
            if (tmpdi.Exists)
                tmpdi.Delete(recursive: true);

            FileSystem system = new FileSystem(new DirectoryInfo(tmpRoot));
            return system;
        }

        protected override ContentEngine getContentEngine(DateTimeOffset? viewDate = null, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null)
        {
            DateTimeOffset realDate = viewDate ?? DateTimeOffset.Now;

            FileSystem system = getFileSystem();

            StreamedBlobRepository fblrepo;
            TreeRepository ftrrepo;

            trrepo = ftrrepo = new TreeRepository(system);
            blrepo = fblrepo = new StreamedBlobRepository(system);
            tpsbrepo = new TreePathStreamedBlobRepository(system, ftrrepo, fblrepo);

            return new ContentEngine(trrepo, blrepo, tpsbrepo, realDate, evaluator, provider);
        }
    }
}
