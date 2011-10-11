using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
#if UseFileSystem
using IVO.Implementation.FileSystem;
#else
using IVO.Implementation.SQL;
#endif
using IVO.Definition.Repositories;
using System.IO;

namespace IVO.CMS.API.Code
{
    public class CMSContext
    {
#if UseFileSystem
        private FileSystem system;
#else
        private Asynq.DataContext db;
#endif

        public readonly ITreeRepository trrepo;
        public readonly IStreamedBlobRepository blrepo;
        public readonly ITreePathStreamedBlobRepository tpsbrepo;
        public readonly ITagRepository tgrepo;
        public readonly IRefRepository rfrepo;
        public readonly IStageRepository strepo;
        public readonly ICommitRepository cmrepo;

        public DirectoryInfo RootDirectory { get; private set; }

        public CMSContext(DirectoryInfo rootDirectory)
        {
            this.RootDirectory = rootDirectory;

#if UseFileSystem
            FileSystem system = new FileSystem(this.RootDirectory);

            TreeRepository trrepo = new TreeRepository(system);
            StreamedBlobRepository blrepo = new StreamedBlobRepository(system);
            TreePathStreamedBlobRepository tpsbrepo = new TreePathStreamedBlobRepository(system, trrepo, blrepo);
            TagRepository tgrepo = new TagRepository(system);
            RefRepository rfrepo = new RefRepository(system);
            StageRepository strepo = new StageRepository(system);
            CommitRepository cmrepo = new CommitRepository(system, tgrepo, rfrepo);

            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.tpsbrepo = tpsbrepo;
            this.tgrepo = tgrepo;
            this.rfrepo = rfrepo;
            this.strepo = strepo;
            this.cmrepo = cmrepo;

            this.system = system;
#else
            Asynq.DataContext db = new Asynq.DataContext(@"Data Source=.\SQLEXPRESS;Initial Catalog=IVO;Integrated Security=SSPI");

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
#endif
        }

        public ContentEngine GetContentEngine(DateTimeOffset? viewDate = null)
        {
            return new ContentEngine(trrepo, blrepo, tpsbrepo, viewDate ?? DateTimeOffset.Now /* extra parameters here for custom providers */);
        }
    }
}