using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IVO.Implementation.FileSystem;
using IVO.Definition.Repositories;
using System.IO;

namespace IVO.CMS.API.Code
{
    public class CMSContext
    {
        private FileSystem system;

        public readonly ITreeRepository trrepo;
        public readonly IStreamedBlobRepository blrepo;
        public readonly ITreePathStreamedBlobRepository tpsbrepo;
        public readonly ITagRepository tgrepo;
        public readonly IRefRepository rfrepo;
        public readonly ICommitRepository cmrepo;

        public DirectoryInfo RootDirectory { get; private set; }

        public CMSContext(DirectoryInfo rootDirectory)
        {
            this.RootDirectory = rootDirectory;

            FileSystem system = new FileSystem(this.RootDirectory);

            TreeRepository trrepo = new TreeRepository(system);
            StreamedBlobRepository blrepo = new StreamedBlobRepository(system);
            TreePathStreamedBlobRepository tpsbrepo = new TreePathStreamedBlobRepository(system, trrepo, blrepo);
            TagRepository tgrepo = new TagRepository(system);
            RefRepository rfrepo = new RefRepository(system);
            CommitRepository cmrepo = new CommitRepository(system, tgrepo, rfrepo);

            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.tpsbrepo = tpsbrepo;
            this.tgrepo = tgrepo;
            this.rfrepo = rfrepo;
            this.cmrepo = cmrepo;

            this.system = system;
        }

        public ContentEngine GetContentEngine(DateTimeOffset? viewDate = null)
        {
            return new ContentEngine(trrepo, blrepo, tpsbrepo, viewDate ?? DateTimeOffset.Now /* extra parameters here for custom providers */);
        }
    }
}