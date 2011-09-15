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

            await processImportElement(state);

            return true;
        }

        #endregion

        private readonly Dictionary<string, string> blobByPath = new Dictionary<string, string>(8);

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

                if (!blobByPath.TryGetValue(ncpath, out blob))
                {
                    // Canonicalize the absolute or relative path relative to the current item's path:
                    var abspath = PathObjectModel.ParseBlobPath(ncpath);
                    CanonicalBlobPath path = abspath.Collapse(abs => abs, rel => (st.Item.TreeBlobPath.Path.Tree + rel)).Canonicalize();

                    // Fetch the Blob given the absolute path constructed:
                    TreePathStreamedBlob[] tBlobs = await st.Engine.TreePathStreamedBlobs.GetBlobsByTreePaths(new TreeBlobPath(st.Item.TreeBlobPath.RootTreeID, path));

                    // TODO: we could probably asynchronously load blobs and render their contents
                    // then at a final sync point go in and inject their contents into the proper
                    // places in each imported blob's parent StringBuilder.

                    // No blob? Put up an error:
                    if (tBlobs == null)
                    {
                        blobByPath.Add(ncpath, (string)null);
                        st.Error("path '{0}' not found", ncpath);
                        return;
                    }

                    // Render the blob inline:
                    RenderState rsInner = new RenderState(st);
                    var innerSb = await rsInner.Render(tBlobs[0]);

                    // Cache the output:
                    // TODO: is this dangerous?
                    blob = innerSb.ToString();
                    blobByPath.Add(ncpath, blob);
                }

                if (blob == null)
                {
                    st.Error("path '{0}' not found", ncpath);
                    return;
                }

                st.Writer.Append(blob);

                // Move the reader back to the element node:
                st.Reader.MoveToElement();
            }
        }
    }
}
