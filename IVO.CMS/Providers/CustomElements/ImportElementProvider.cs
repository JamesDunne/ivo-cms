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

        public bool ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-import") return false;

            processImportElement(state);

            return true;
        }

        #endregion

        private void processImportElement(RenderState st)
        {
            // Imports content directly from another blob, addressable by a relative path or an absolute path.
            // Relative path is always relative to the current blob's absolute path.
            // In the case of nested imports, relative paths are relative to the absolute path of the importee's parent blob.

            // <cms-import relative-path="../templates/main" />
            // <cms-import absolute-path="/templates/main" />

            // Absolute paths are canonicalized. An exception will be thrown if the path contains too many '..' references that
            // bring the canonicalized path above the root of the tree (which is impossible).

            // Recursively call RenderBlob on the imported blob and include the rendered HTMLFragment into this rendering.

            // st.Reader is pointing to "cms-import" Element.
            if (!st.Reader.IsEmptyElement) st.Error("cms-import element must be empty");

            if (st.Reader.HasAttributes && st.Reader.MoveToFirstAttribute())
            {
                string relPath = st.Reader.GetAttribute("relative-path");
                string absPath = st.Reader.GetAttribute("absolute-path");

                // Check mutual exclusion of attributes:
                if ((absPath == null) == (relPath == null)) st.Error("cms-import must have either 'relative-path' or 'absolute-path' attribute but not both");

                // Canonicalize the path:
                CanonicalBlobPath path;
                if (absPath != null)
                {
                    path = ((AbsoluteBlobPath)absPath).Canonicalize();
                }
                else
                {
                    // Apply the relative path to the current item's absolute path:
                    RelativeBlobPath rbp = (RelativeBlobPath)relPath;
                    CanonicalTreePath ctp = (st.Item.Path.Tree + rbp.Tree).Canonicalize();
                    path = new CanonicalBlobPath(ctp, rbp.Name);
                }

                // Fetch the Blob given the absolute path constructed:
                Task<BlobTreePath> tBlob = st.Engine.Blobs.GetBlobByAbsolutePath(st.Item.RootTreeID, path);

                // TODO: we could probably asynchronously load blobs and render their contents
                // then at a final sync point go in and inject their contents into the proper
                // places in each imported blob's parent StringBuilder.
                tBlob.Wait();

                // No blob? Put up an error:
                if (tBlob.Result == null)
                {
                    st.Error("path '{0}' not found", path);
                    return;
                }

                // Render the blob inline:
                RenderState rsInner = new RenderState(st);
                rsInner.Render(tBlob.Result);
                string innerResult = rsInner.Writer.ToString();
                st.Writer.Append(innerResult);
            }
        }
    }
}
