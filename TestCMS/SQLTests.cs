using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Asynq;
using IVO.Definition.Repositories;
using IVO.CMS;
using IVO.CMS.Providers.CustomElements;
using IVO.Implementation.SQL;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestCMS
{
    [TestClass]
    public class SQLTests : CommonTest.ContentEngineTestMethods
    {
        private DataContext db;

        private DataContext getDataContext()
        {
            return new DataContext(@"Data Source=.\SQLEXPRESS;Initial Catalog=IVO;Integrated Security=SSPI");
        }

        protected override ContentEngine getContentEngine(DateTimeOffset? viewDate = null, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null)
        {
            DateTimeOffset realDate = viewDate ?? DateTimeOffset.Now;

            db = getDataContext();

            StreamedBlobRepository rblrepo;

            trrepo = new TreeRepository(db);
            blrepo = rblrepo = new StreamedBlobRepository(db);
            tpsbrepo = new TreePathStreamedBlobRepository(db, rblrepo);
            return new ContentEngine(trrepo, blrepo, tpsbrepo, realDate, evaluator, provider);
        }
    }
}
