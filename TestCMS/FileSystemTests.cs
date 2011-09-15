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
    public class FileSystemTests
    {
        private static void cleanUp(FileSystem system)
        {
            // Clean up:
            if (system.Root.Exists)
                system.Root.Delete(recursive: true);
        }

        private static FileSystem getFileSystem()
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

        private static TestContext getTestContext(DateTimeOffset? viewDate = null, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null)
        {
            DateTimeOffset realDate = viewDate ?? DateTimeOffset.Now;

            FileSystem system = getFileSystem();

            var trrepo = new TreeRepository(system);
            var blrepo = new StreamedBlobRepository(system);
            var tpsbrepo = new TreePathStreamedBlobRepository(system, trrepo, blrepo);

            return new TestContext(new ContentEngine(trrepo, blrepo, tpsbrepo, realDate, evaluator, provider), trrepo, blrepo, tpsbrepo);
        }

        private CommonTest.ContentEngineTestMethods getTestMethods()
        {
            return new CommonTest.ContentEngineTestMethods(getTestContext);
        }

        [TestMethod]
        public void TestRenderBlob() { getTestMethods().TestRenderBlob(); }

        [TestMethod]
        public void TestRenderBlobAttributes() { getTestMethods().TestRenderBlobAttributes(); }

        [TestMethod]
        public void TestRenderBlobWithContent() { getTestMethods().TestRenderBlobWithContent(); }

        [TestMethod]
        public void TestRenderBlobWithEmptyElements() { getTestMethods().TestRenderBlobWithEmptyElements(); }

        [TestMethod]
        public void TestImportAbsolute() { getTestMethods().TestImportAbsolute().Wait(); }

        [TestMethod]
        public void TestImportRelative() { getTestMethods().TestImportRelative().Wait(); }

        [TestMethod]
        public void TestScheduled() { getTestMethods().TestScheduled(); }

        [TestMethod]
        public void TestScheduled2() { getTestMethods().TestScheduled2(); }

        [TestMethod]
        public void TestScheduled3() { getTestMethods().TestScheduled3(); }

        [TestMethod]
        public void TestScheduledNot() { getTestMethods().TestScheduledNot(); }

        [TestMethod]
        public void TestScheduleFail1() { getTestMethods().TestScheduleFail1(); }

        [TestMethod]
        public void TestScheduleFail2() { getTestMethods().TestScheduleFail2(); }

        [TestMethod]
        public void TestScheduleFail3() { getTestMethods().TestScheduleFail3(); }

        [TestMethod]
        public void TestScheduleFail4() { getTestMethods().TestScheduleFail4(); }

        [TestMethod]
        public void TestScheduleFail5() { getTestMethods().TestScheduleFail5(); }

        [TestMethod]
        public void TestScheduleFail6() { getTestMethods().TestScheduleFail6(); }

        [TestMethod]
        public void TestScheduleFail7() { getTestMethods().TestScheduleFail7(); }

        [TestMethod]
        public void TestScheduleFail8() { getTestMethods().TestScheduleFail8(); }

        [TestMethod]
        public void TestConditionalIfElse_IfWins() { getTestMethods().TestConditionalIfElse_IfWins(); }

        [TestMethod]
        public void TestConditionalIfElse_ElseWins() { getTestMethods().TestConditionalIfElse_ElseWins(); }

        [TestMethod]
        public void TestConditionalIfElifElse_IfWins() { getTestMethods().TestConditionalIfElifElse_IfWins(); }

        [TestMethod]
        public void TestConditionalIfElifElse_ElifWins() { getTestMethods().TestConditionalIfElifElse_ElifWins(); }

        [TestMethod]
        public void TestConditionalIfElifElse_ElseWins() { getTestMethods().TestConditionalIfElifElse_ElseWins(); }

        [TestMethod]
        public void TestConditionalIfMultipleElifElse_ElifWins() { getTestMethods().TestConditionalIfMultipleElifElse_ElifWins(); }

        [TestMethod]
        public void TestConditionalFail1() { getTestMethods().TestConditionalFail1(); }

        [TestMethod]
        public void TestConditionalFail2() { getTestMethods().TestConditionalFail2(); }

        [TestMethod]
        public void TestConditionalFail3() { getTestMethods().TestConditionalFail3(); }

        [TestMethod]
        public void TestConditionalOkay1() { getTestMethods().TestConditionalOkay1(); }

        [TestMethod]
        public void TestConditionalOkay2() { getTestMethods().TestConditionalOkay2(); }

        [TestMethod]
        public void TestCMSLinkAbsolute() { getTestMethods().TestCMSLinkAbsolute(); }

        [TestMethod]
        public void TestCMSLinkAttributes() { getTestMethods().TestCMSLinkAttributes(); }

        [TestMethod]
        public void TestCMSLinkRelative() { getTestMethods().TestCMSLinkRelative(); }

        [TestMethod]
        public void TestCMSLinkEmpty() { getTestMethods().TestCMSLinkEmpty(); }

        [TestMethod]
        public void TestCMSLinkFail1() { getTestMethods().TestCMSLinkFail1(); }

        [TestMethod]
        public void TestCMSLinkFail2() { getTestMethods().TestCMSLinkFail2(); }

        [TestMethod]
        public void TestCMSLinkFail3() { getTestMethods().TestCMSLinkFail3(); }

        [TestMethod]
        public void TestCMSLinkFail4() { getTestMethods().TestCMSLinkFail4(); }

        [TestMethod]
        public void TestNestedElements() { getTestMethods().TestNestedElements().Wait(); }

        [TestMethod]
        public void TestNestedElementsSkip() { getTestMethods().TestNestedElementsSkip().Wait(); }

        [TestMethod]
        public void TestUnknownSkipped() { getTestMethods().TestUnknownSkipped(); }

        [TestMethod]
        public void SpeedTestRenderBlob() { getTestMethods().SpeedTestRenderBlob().Wait(); }
    }
}
