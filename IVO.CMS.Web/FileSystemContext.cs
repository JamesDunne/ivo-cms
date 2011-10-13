using System;
using System.IO;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Models;
using IVO.Definition.Repositories;
using IVO.Implementation.FileSystem;

namespace IVO.CMS.Web
{
    public sealed class FileSystemContext : ISystemContext
    {
        private FileSystem system;

        public ITreeRepository trrepo { get; private set; }
        public IStreamedBlobRepository blrepo { get; private set; }
        public ITreePathStreamedBlobRepository tpsbrepo { get; private set; }
        public ITagRepository tgrepo { get; private set; }
        public IRefRepository rfrepo { get; private set; }
        public IStageRepository strepo { get; private set; }
        public ICommitRepository cmrepo { get; private set; }
        
        public DirectoryInfo RootDirectory { get; private set; }

        public FileSystemContext(DirectoryInfo rootDirectory)
        {
            this.RootDirectory = rootDirectory;

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
        }
    }
}