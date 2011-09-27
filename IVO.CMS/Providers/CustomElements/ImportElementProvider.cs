using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;
using System.Threading.Tasks;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class ImportElementProvider : ICustomElementProvider
    {
        public ImportElementProvider(ICustomElementProvider next = null)
        {
            this.Next = next;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public async Task<bool> ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-import") return false;

            await processImportElement(state).ConfigureAwait(continueOnCapturedContext: false);

            return true;
        }

        #endregion

#if ImportCache
        private readonly Dictionary<string, TreePathStreamedBlob> tpsbByPath = new Dictionary<string, TreePathStreamedBlob>(8);
        private readonly Dictionary<BlobID, string> blobContents = new Dictionary<BlobID, string>(8);
#endif

        private async Task processImportElement(RenderState st)
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
#if ImportCache
                if (!tpsbByPath.TryGetValue(ncpath, out tpsBlob))
                {
#endif
                    // Canonicalize the absolute or relative path relative to the current item's path:
                    var abspath = PathObjectModel.ParseBlobPath(ncpath);
                    CanonicalBlobPath path = abspath.Collapse(abs => abs, rel => (st.Item.TreeBlobPath.Path.Tree + rel)).Canonicalize();

                    // Fetch the Blob given the absolute path constructed:
                    TreeBlobPath tbp = new TreeBlobPath(st.Item.TreeBlobPath.RootTreeID, path);
                    var etpsBlob = await st.Engine.TreePathStreamedBlobs.GetBlobByTreePath(tbp).ConfigureAwait(continueOnCapturedContext: false);
                    if (etpsBlob.HasErrors)
                    {
                        foreach (var err in etpsBlob.Errors)
                            st.Error(err.Message);
                        return;
                    }

                    tpsBlob = etpsBlob.Value;
#if false
                    if (tpsBlob != null)
                        Console.WriteLine("Found blob for '{0}'", path.ToString());
                    else
                        Console.WriteLine("No blob found for '{0}'", path.ToString());
#endif
#if ImportCache
                    tpsbByPath.Add(ncpath, tpsBlob);
                }
#endif
                // No blob? Put up an error:
                if (tpsBlob == null)
                {
#if ImportCache
                    blobContents.Add(tpsBlob.StreamedBlob.ID, (string)null);
#endif
                    st.Error("cms-import path '{0}' not found", ncpath);
                    return;
                }

                // Fetch the contents for the given TreePathStreamedBlob:
#if ImportCache
                if (!blobContents.TryGetValue(tpsBlob.StreamedBlob.ID, out blob))
                {
                    Console.WriteLine("Contents not cached for BlobID {0}", tpsBlob.StreamedBlob.ID);
#endif
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
                        return;
                    }

                    blob = einnerSb.Value.ToString();
#if ImportCache

                    // Cache the rendered blob:

                    // TODO: is this dangerous? Perhaps we should build up some state during rendering that indicates whether or
                    // not content caching per BlobID should be done.

                    blobContents.Add(tpsBlob.StreamedBlob.ID, blob);
                }

                // FIXME: this should never occur.
                if (blob == null)
                {
                    st.Error("cms-import path '{0}' not found", ncpath);
                    return;
                }
#endif

                st.Writer.Append(blob);

                // Move the reader back to the element node:
                st.Reader.MoveToElement();
            }
        }
    }
}
