using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IVO.Definition.Models;

namespace IVO.CMS.API.Models
{
    public static class JSONTranslateExtensions
    {
        #region Date/Time handling

        private static string FromDate(DateTimeOffset value)
        {
            return value.ToString("s");
        }

        private static DateTimeOffset ToDate(string value)
        {
            return DateTimeOffset.ParseExact(value, "s", System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion

        #region Commit

        public static Commit.Builder FromJSON(this CommitModel cmj)
        {
            // Do conversions on the strings and detect any errors:
            cmj.parents = cmj.parents ?? new string[0];
            var maybeparentids = cmj.parents.SelectAsArray(s => CommitID.TryParse(s ?? String.Empty));
            var maybetreeid = TreeID.TryParse(cmj.treeid ?? String.Empty);

            // Which ones failed?
            var exceptions = (from m in maybeparentids where m.IsRight select m.Right)
                .Concat(from m in new[] { maybetreeid } where m.IsRight select m.Right);

            // Throw an exception if any conversions failed:
            if (exceptions.Any()) throw new AggregateException(exceptions);

            Commit.Builder cm = new Commit.Builder(
                pParents:       maybeparentids.SelectAsArray(id => id.Left).ToList(maybeparentids.Length),
                pTreeID:        maybetreeid.Left,
                pCommitter:     cmj.committer ?? String.Empty,
                pDateCommitted: String.IsNullOrWhiteSpace(cmj.date_committed) ? DateTimeOffset.Now : ToDate(cmj.date_committed),
                pMessage:       cmj.message ?? String.Empty
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
                date_committed  = FromDate(cm.DateCommitted),
                message         = cm.Message
            };
            return cmj;
        }

        #endregion

        #region Ref

        public static Ref.Builder FromJSON(this RefModel rfm)
        {
            // Do conversions on the strings and detect any errors:
            var maybecommitid = CommitID.TryParse(rfm.commitid ?? String.Empty);

            // Which ones failed?
            var exceptions = (from m in new[] { maybecommitid } where m.IsRight select m.Right);

            // Throw an exception if any conversions failed:
            if (exceptions.Any()) throw new AggregateException(exceptions);

            Ref.Builder rf = new Ref.Builder(
                pName:      (RefName)rfm.name,
                pCommitID:  maybecommitid.Left
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
            // Do conversions on the strings and detect any errors:
            var maybecommitid = CommitID.TryParse(tgm.commitid ?? String.Empty);

            // Which ones failed?
            var exceptions = (from m in new[] { maybecommitid } where m.IsRight select m.Right);

            // Throw an exception if any conversions failed:
            if (exceptions.Any()) throw new AggregateException(exceptions);

            Tag.Builder tg = new Tag.Builder(
                pName:          (TagName)tgm.name,
                pCommitID:      maybecommitid.Left,
                pTagger:        tgm.tagger ?? String.Empty,
                pDateTagged:    String.IsNullOrWhiteSpace(tgm.date_tagged) ? DateTimeOffset.Now : ToDate(tgm.date_tagged),
                pMessage:       tgm.message ?? String.Empty
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
                date_tagged     = FromDate(tg.DateTagged),
                message         = tg.Message
            };
            return tgm;
        }

        #endregion
    }
}