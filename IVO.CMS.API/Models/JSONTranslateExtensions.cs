using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IVO.Definition.Models;
using IVO.Definition.Errors;

namespace IVO.CMS.API.Models
{
    public static class JSONTranslateExtensions
    {
        #region Date/Time handling

        internal static string FromDate(DateTimeOffset value)
        {
            return value.ToString("s");
        }

        internal static DateTimeOffset ToDate(string value)
        {
            return DateTimeOffset.ParseExact(value, "s", System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion

        #region Commit

        public static Errorable<Commit.Builder> FromJSON(this CommitRequest cmj)
        {
            // Do conversions on the strings and detect any errors:
            cmj.parents = cmj.parents ?? new string[0];
            var maybeparentids = cmj.parents.SelectAsArray(s => CommitID.TryParse(s ?? String.Empty));
            var maybetreeid = TreeID.TryParse(cmj.treeid ?? String.Empty);

            // Which ones failed?
            var errors =
                (from m in maybeparentids where m.HasErrors select m.Errors)
                .Concat(from m in new[] { maybetreeid } where m.HasErrors select m.Errors)
                .Aggregate(new ErrorContainer(), (acc, err) => acc + err);

            // Return any errors encountered:
            if (errors.HasAny) return errors;

            Commit.Builder cm = new Commit.Builder(
                pParents:       maybeparentids.SelectAsArray(id => id.Value).ToList(maybeparentids.Length),
                pTreeID:        maybetreeid.Value,
                pCommitter:     cmj.committer ?? String.Empty,
                pDateCommitted: String.IsNullOrWhiteSpace(cmj.date_committed) ? DateTimeOffset.Now : ToDate(cmj.date_committed),
                pMessage:       cmj.message ?? String.Empty
            );
            return cm;
        }

        public static CommitResponse ToJSON(this ICommit cm)
        {
            CommitResponse cmj = new CommitResponse()
            {
                id                  = cm.ID.ToString(),
                parents_retrieved   = cm.IsComplete,
                parents             = cm.Parents.SelectAsArray(s => s.ToString()),
                treeid              = cm.TreeID.ToString(),
                committer           = cm.Committer,
                date_committed      = FromDate(cm.DateCommitted),
                message             = cm.Message
            };
            return cmj;
        }

        #endregion

        #region Ref

        public static Errorable<Ref.Builder> FromJSON(this RefRequest rfm)
        {
            // Do conversions on the strings and detect any errors:
            var maybecommitid = CommitID.TryParse(rfm.commitid ?? String.Empty);
            if (maybecommitid.HasErrors) return maybecommitid.Errors;

            Ref.Builder rf = new Ref.Builder(
                pName:      (RefName)rfm.name,
                pCommitID:  maybecommitid.Value
            );
            return rf;
        }

        public static RefResponse ToJSON(this Ref rf)
        {
            RefResponse rfm = new RefResponse()
            {
                name        = rf.Name.ToString(),
                commitid    = rf.CommitID.ToString()
            };
            return rfm;
        }

        #endregion

        #region Tag

        public static Errorable<Tag.Builder> FromJSON(this TagRequest tgm)
        {
            // Do conversions on the strings and detect any errors:
            var maybecommitid = CommitID.TryParse(tgm.commitid ?? String.Empty);
            if (maybecommitid.HasErrors) return maybecommitid.Errors;

            Tag.Builder tg = new Tag.Builder(
                pName:          (TagName)tgm.name,
                pCommitID:      maybecommitid.Value,
                pTagger:        tgm.tagger ?? String.Empty,
                pDateTagged:    String.IsNullOrWhiteSpace(tgm.date_tagged) ? DateTimeOffset.Now : ToDate(tgm.date_tagged),
                pMessage:       tgm.message ?? String.Empty
            );
            return tg;
        }

        public static TagResponse ToJSON(this Tag tg)
        {
            TagResponse tgm = new TagResponse()
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

        #region Errors

        public static object ToJSON(this ErrorBase err)
        {
            return new { message = err.Message };
        }

        #endregion
    }
}