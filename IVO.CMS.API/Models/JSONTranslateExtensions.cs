using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IVO.Definition.Models;

namespace IVO.CMS.API.Models
{
    public static class JSONTranslateExtensions
    {
        #region Commit

        public static Commit.Builder FromJSON(this CommitModel cmj)
        {
            Commit.Builder cm = new Commit.Builder(
                pParents:       cmj.parents == null ? new List<CommitID>(0) : cmj.parents.Select(s => new CommitID(s)).ToList(cmj.parents.Length),
                pTreeID:        new TreeID(cmj.treeid),
                pCommitter:     cmj.committer,
                pDateCommitted: cmj.date_committed,
                pMessage:       cmj.message
            );
            return cm;
        }

        public static CommitModel ToJSON(this ICommit cm)
        {
            CommitModel cmj = new CommitModel()
            {
                id              = cm.ID.ToString(),
                is_complete     = cm.IsComplete,
                parents         = cm.Parents.SelectAsArray(s => s.ToString()),
                treeid          = cm.TreeID.ToString(),
                committer       = cm.Committer,
                date_committed  = cm.DateCommitted,
                message         = cm.Message
            };
            return cmj;
        }

        #endregion

        #region Ref

        public static Ref.Builder FromJSON(this RefModel rfm)
        {
            Ref.Builder rf = new Ref.Builder(
                pName:      (RefName)rfm.name,
                pCommitID:  new CommitID(rfm.commitid)
            );
            return rf;
        }

        public static RefModel ToJSON(this Ref rf)
        {
            RefModel rfm = new RefModel()
            {
                name        = rf.Name.ToString(),
                commitid    = rf.CommitID.ToString()
            };
            return rfm;
        }

        #endregion

        #region Tag

        public static Tag.Builder FromJSON(this TagModel tgm)
        {
            Tag.Builder tg = new Tag.Builder(
                pName:          (TagName)tgm.name,
                pCommitID:      new CommitID(tgm.commitid),
                pTagger:        tgm.tagger,
                pDateTagged:    tgm.date_tagged,
                pMessage:       tgm.message
            );
            return tg;
        }

        public static TagModel ToJSON(this Tag tg)
        {
            TagModel tgm = new TagModel()
            {
                id              = tg.ID.ToString(),
                name            = tg.Name.ToString(),
                commitid        = tg.CommitID.ToString(),
                tagger          = tg.Tagger,
                date_tagged     = tg.DateTagged,
                message         = tg.Message
            };
            return tgm;
        }

        #endregion
    }
}