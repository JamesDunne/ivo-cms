using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IVO.Definition.Models;

namespace IVO.CMS.API.Models
{
    public static class JSONTranslateExtensions
    {
        public static Commit FromJSON(this CommitModel cmj)
        {
            Commit cm = new Commit.Builder(
                pParents:       cmj.parents == null ? new List<CommitID>(0) : cmj.parents.Select(s => new CommitID(s)).ToList(cmj.parents.Length),
                pTreeID:        new TreeID(cmj.treeid),
                pCommitter:     cmj.committer,
                pDateCommitted: cmj.date_committed,
                pMessage:       cmj.message
            );
            return cm;
        }

        public static CommitModel ToJSON(this Commit cm)
        {
            CommitModel cmj = new CommitModel()
            {
                id              = cm.ID.ToString(),
                parents         = cm.Parents.SelectAsArray(s => s.ToString()),
                treeid          = cm.TreeID.ToString(),
                committer       = cm.Committer,
                date_committed  = cm.DateCommitted,
                message         = cm.Message
            };
            return cmj;
        }
    }
}