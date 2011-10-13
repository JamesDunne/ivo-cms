using System;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Models;
using IVO.Definition.Repositories;
using IVO.Implementation.SQL;

namespace IVO.CMS.Web
{
    public sealed class SQLSystemContext : ISystemContext
    {
        private Asynq.DataContext db;

        public ITreeRepository trrepo { get; private set; }
        public IStreamedBlobRepository blrepo { get; private set; }
        public ITreePathStreamedBlobRepository tpsbrepo { get; private set; }
        public ITagRepository tgrepo { get; private set; }
        public IRefRepository rfrepo { get; private set; }
        public IStageRepository strepo { get; private set; }
        public ICommitRepository cmrepo { get; private set; }

        public SQLSystemContext(string dataSource)
        {
            // @"Data Source=.\SQLEXPRESS;Initial Catalog=IVO;Integrated Security=SSPI"
            Asynq.DataContext db = new Asynq.DataContext(dataSource);

            TreeRepository trrepo = new TreeRepository(db);
            StreamedBlobRepository blrepo = new StreamedBlobRepository(db);
            TreePathStreamedBlobRepository tpsbrepo = new TreePathStreamedBlobRepository(db, blrepo);
            TagRepository tgrepo = new TagRepository(db);
            RefRepository rfrepo = new RefRepository(db);
            StageRepository strepo = new StageRepository(db);
            CommitRepository cmrepo = new CommitRepository(db);

            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.tpsbrepo = tpsbrepo;
            this.tgrepo = tgrepo;
            this.rfrepo = rfrepo;
            this.strepo = strepo;
            this.cmrepo = cmrepo;

            this.db = db;
        }
    }
}