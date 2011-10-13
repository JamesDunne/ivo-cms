using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IVO.Definition.Models;
using IVO.Definition.Repositories;

namespace IVO.CMS.Web
{
    public interface ISystemContext
    {
        ITreeRepository trrepo { get; }
        IStreamedBlobRepository blrepo { get; }
        ITreePathStreamedBlobRepository tpsbrepo { get; }
        ITagRepository tgrepo { get; }
        IRefRepository rfrepo { get; }
        IStageRepository strepo { get; }
        ICommitRepository cmrepo { get; }
    }
}
