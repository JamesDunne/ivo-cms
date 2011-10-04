using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;
using System.Threading.Tasks;
using IVO.Definition.Errors;
using System.Diagnostics;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class ImportElementProvider : ICustomElementProvider
    {
        public ImportElementProvider(ICustomElementProvider next = null)
        {
            this.Next = next;
        }

        public ICustomElementProvider Next { get; private set; }

        public async Task<Errorable<bool>> ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-import") return false;

            var err = await processImportElement(state).ConfigureAwait(continueOnCapturedContext: false);
            if (err.HasErrors) return err.Errors;

            return true;
        }

        private async Task<Errorable> processImportElement(RenderState st)
        {
            // Imports content directly from another blob, addressable by a relative path or an absolute path.
            // Relative path is always relative to the current blob's absolute path.
            // In the case of nested imports, relative paths are relative to the absolute path of the importee's parent blob.

            // <cms-import path="../templates/main" />
            // <cms-import path="/templates/main" />

            // Absolute paths are canonicalized. An exception will be thrown if the path contains too many '..' references that
            // bring the canonicalized path above the root of the tree (which is impossible).

            // Recursively call RenderBlob on the imported blob and include the rendered HTMLFragment into this rendering.

            // st.Reader is pointing to "cms-import" Element.
            if (!st.Reader.IsEmptyElement) st.Error("cms-import element must be empty");

            if (st.Reader.HasAttributes && st.Reader.MoveToFirstAttribute())
            {
                string ncpath = st.Reader.GetAttribute("path");
                string blob;
                TreePathStreamedBlob tpsBlob;

                // Fetch the TreePathStreamedBlob for the given path:
                // Canonicalize the absolute or relative path relative to the current item's path:
                var abspath = PathObjectModel.ParseBlobPath(ncpath);
                CanonicalBlobPath path = abspath.Collapse(abs => abs, rel => (st.Item.TreeBlobPath.Path.Tree + rel)).Canonicalize();

                // Fetch the Blob given the absolute path constructed:
                TreeBlobPath tbp = new TreeBlobPath(st.Item.TreeBlobPath.RootTreeID, path);
                var etpsBlob = await st.Engine.TreePathStreamedBlobs.GetBlobByTreePath(tbp).ConfigureAwait(continueOnCapturedContext: false);
                if (etpsBlob.HasErrors)
                {
                    st.SkipElementAndChildren("cms-import");

                    // Check if the error is a simple blob not found error:
                    bool notFound = etpsBlob.Errors.Any(er => er is BlobNotFoundByPathError);
                    if (notFound)
                    {
                        st.Error("cms-import could not find blob by path '{0]' off tree '{1}'", tbp.Path, tbp.RootTreeID);
                        return Errorable.NoErrors;
                    }

                    // Error was more serious:
                    foreach (var err in etpsBlob.Errors)
                        st.Error(err.Message);
                    return etpsBlob.Errors;
                }
                else tpsBlob = etpsBlob.Value;

                Debug.Assert(tpsBlob != null);

                // Fetch the contents for the given TreePathStreamedBlob:
                // TODO: we could probably asynchronously load blobs and render their contents
                // then at a final sync point go in and inject their contents into the proper
                // places in each imported blob's parent StringBuilder.

                // Render the blob inline:
                RenderState rsInner = new RenderState(st.Engine, tpsBlob);
                var einnerSb = await rsInner.Render().ConfigureAwait(continueOnCapturedContext: false);
                if (einnerSb.HasErrors)
                {
                    foreach (var err in einnerSb.Errors)
                        st.Error(err.Message);
                    return einnerSb.Errors;
                }

                blob = einnerSb.Value.ToString();

                st.Writer.Append(blob);

                // Move the reader back to the element node:
                st.Reader.MoveToElement();
            }

            return Errorable.NoErrors;
        }
    }
}
