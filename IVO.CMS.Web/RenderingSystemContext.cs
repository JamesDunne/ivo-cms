using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Errors;
using IVO.Definition.Models;

namespace IVO.CMS.Web
{
    public sealed class RenderingSystemContext
    {
        public RenderingSystemContext(ISystemContext systemContext, DateTimeOffset? viewDate = null, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null, bool throwOnError = false, bool injectErrorComments = true, bool injectWarningComments = false)
        {
            this.SystemContext = systemContext;
            this.Engine = new ContentEngine(
                systemContext.trrepo,
                systemContext.blrepo,
                systemContext.tpsbrepo,
                viewDate ?? DateTimeOffset.Now,
                evaluator,
                provider,
                throwOnError,
                injectErrorComments,
                injectWarningComments
            );
        }

        public ISystemContext SystemContext { get; private set; }
        public ContentEngine Engine { get; private set; }

        private Task<Maybe<TreeID>> _root;
        public Task<Maybe<TreeID>> GetRoot()
        {
            // Caching Task<T> is much better for performance than caching the result and recreating the Task<T> each time:
            if (_root == null) _root = getRoot();
            return _root;
        }

        private async Task<Maybe<TreeID>> getRoot()
        {
            // First, attempt to get the current user's staging area and use its TreeID:
            // TODO.

            // Fall back on the commit from 'system/published' ref and use its TreeID:
            var erfcm = await SystemContext.cmrepo.GetCommitByRefName((RefName)"system/published");
            if (erfcm.HasErrors) return Maybe<TreeID>.Nothing;

            return erfcm.Value.Item2.TreeID;
        }

        public async Task<Errorable<HtmlFragment>> RenderBlobAsync(CanonicalBlobPath path)
        {
            var mroot = await GetRoot();
            // The most popular error message will be...
            if (!mroot.HasValue) return new ConsistencyError("Either the site is not published or we have no frame of reference on which ");

            // Create a TreeBlobPath to root the blob path in the current root TreeID:
            var tbpath = new TreeBlobPath(mroot.Value, path);

            // Try getting the blob by its path:
            var eblob = await SystemContext.tpsbrepo.GetBlobByTreePath(tbpath);
            if (eblob.HasErrors) return eblob.Errors;
            var blob = eblob.Value.StreamedBlob;

            // Render the blob:
            var efragment = await Engine.RenderBlob(new TreePathStreamedBlob(tbpath, blob));
            if (efragment.HasErrors) return efragment.Errors;

            // Return its output:
            return efragment.Value;
        }
    }
}
