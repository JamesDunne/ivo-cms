using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;
using System.Threading.Tasks;
using System.Xml;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class ImportTemplateElementProvider : ICustomElementProvider
    {
        public ImportTemplateElementProvider(ICustomElementProvider next = null)
        {
            this.Next = next;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public async Task<bool> ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-import-template") return false;

            await processImportTemplateElement(state).ConfigureAwait(continueOnCapturedContext: false);

            return true;
        }

        #endregion

        private async Task processImportTemplateElement(RenderState st)
        {
            // Imports content directly from another blob, addressable by a relative path or an absolute path.
            // Relative path is always relative to the current blob's absolute path.
            // In the case of nested imports, relative paths are relative to the absolute path of the importee's parent blob.

            // <cms-import-template path="/template/main">
            //   <area id="head">
            //     <link rel="" />
            //   </area>
            //   <area id="body">
            //     <div>
            //       ...
            //     </div>
            //   </area>
            // </cms-import-template>

            // Absolute paths are canonicalized. An exception will be thrown if the path contains too many '..' references that
            // bring the canonicalized path above the root of the tree (which is impossible).

            // Recursively call RenderBlob on the imported blob and include the rendered HTMLFragment into this rendering.

            var xr = st.Reader;

            // st.Reader is pointing to "cms-import-template" Element.

            if (!xr.HasAttributes || !xr.MoveToAttribute("path"))
            {
                st.Error("cms-import-template requires a 'path' attribute");
                st.SkipElementAndChildren("cms-import-template");
                return;
            }

            string ncpath = xr.Value;
            st.Reader.MoveToElement();

            TreePathStreamedBlob tmplBlob;

            // Canonicalize the absolute or relative path relative to the current item's path:
            var abspath = PathObjectModel.ParseBlobPath(ncpath);
            CanonicalBlobPath path = abspath.Collapse(abs => abs, rel => (st.Item.TreeBlobPath.Path.Tree + rel)).Canonicalize();

            // Fetch the Blob given the absolute path constructed:
            TreeBlobPath tbp = new TreeBlobPath(st.Item.TreeBlobPath.RootTreeID, path);
            tmplBlob = await st.Engine.TreePathStreamedBlobs.GetBlobByTreePath(tbp).ConfigureAwait(continueOnCapturedContext: false);

#if false
            if (tpsBlob != null)
                Console.WriteLine("Found blob for '{0}'", path.ToString());
            else
                Console.WriteLine("No blob found for '{0}'", path.ToString());
#endif

            // No blob? Put up an error:
            if (tmplBlob == null)
            {
                st.Error("cms-import-template path '{0}' not found", path.ToString());
                st.SkipElementAndChildren("cms-import-template");
                return;
            }

            // This lambda processes the entire imported template:
            Func<RenderState, Task<bool>> processElements = null;
            
            processElements = (Func<RenderState, Task<bool>>)(async sst =>
            {
                // Make sure cms-template is the first element from the imported template blob:
                if (sst.Reader.LocalName != "cms-template")
                {
                    sst.Error("cms-import-template expected cms-template as first element of imported template");
                    sst.SkipElementAndChildren(sst.Reader.LocalName);
                    st.SkipElementAndChildren("cms-import-template");
                    return false;
                }

                // Don't move the st.Reader yet until we know the cms-import-template has a cms-template-area in it:
                string fillerAreaId = null;
                bool isFirstArea = !st.Reader.IsEmptyElement;

                // Create a new RenderState that reads from the parent blob and writes to the template's renderer:
                var stWriter = new RenderState(st.Engine, st.Item, st.Reader, sst.Writer);

                Func<RenderState, Task<bool>> processTemplateAreaElements = null;
                processTemplateAreaElements = (Func<RenderState, Task<bool>>)(async tst =>
                {
                    // Only process cms-template-area elements:
                    if (tst.Reader.LocalName != "cms-template-area")
                    {
                        // Use DefaultProcessElements to handle processing other cms- custom elements from the template:
                        return await RenderState.DefaultProcessElements(tst);
                    }

                    // Read the cms-template-area's id attribute:
                    if (!tst.Reader.MoveToAttribute("id"))
                    {
                        tst.Error("cms-template-area needs an 'id' attribute");
                        tst.Reader.MoveToElement();
                        tst.SkipElementAndChildren("cms-template-area");
                        return false;
                    }

                    // Assign the template's area id:
                    string tmplAreaId = tst.Reader.Value;

                    // Move to the first area if we have to:
                    if (isFirstArea)
                    {
                        if (!st.Reader.IsEmptyElement)
                        {
                            fillerAreaId = moveToNextAreaElement(st);
                        }
                        isFirstArea = false;
                    }

                    // Do the ids match?
                    if ((fillerAreaId != null) && (tmplAreaId == fillerAreaId))
                    {
                        // Skip the cms-template-area in the template:
                        tst.Reader.MoveToElement();
                        tst.SkipElementAndChildren("cms-template-area");
                        // Move the filler reader to the element node:
                        st.Reader.MoveToElement();
                        // Copy the elements:
                        await stWriter.CopyElementChildren("area");

                        // Move to the next area element, if available:
                        fillerAreaId = moveToNextAreaElement(st);
                    }
                    else
                    {
                        // Insert the default content from the template:
                        tst.Reader.MoveToElement();
                        // Recurse into children, allowing processing of embedded cms-template-areas:
                        await tst.CopyElementChildren("cms-template-area", null, processTemplateAreaElements);
                    }

                    // We handled this:
                    return false;
                });

                // Now continue on stream-copying child elements until we find a cms-template-area:
                await
                    sst.CopyElementChildren("cms-template", null, processTemplateAreaElements)
                    .ConfigureAwait(continueOnCapturedContext: false);

                // We missed some <area />s in the cms-import-template:
                while (!((st.Reader.NodeType == XmlNodeType.EndElement) && (st.Reader.LocalName == "cms-import-template")) &&
                       !((st.Reader.NodeType == XmlNodeType.Element) && st.Reader.IsEmptyElement && (st.Reader.LocalName == "cms-import-template")))
                {
                    // Move to the next <area /> start element:
                    fillerAreaId = moveToNextAreaElement(st);
                    if (fillerAreaId != null)
                    {
                        st.Warning("area '{0}' unused by the template", fillerAreaId);
                        st.SkipElementAndChildren("area");
                    }
                }

                return false;
            });

            // Process the imported cms-template and inject the <area /> elements from the current <cms-import-template /> element:
            RenderState importedTemplate = new RenderState(
                st.Engine,
                tmplBlob,

                earlyExit: (Func<RenderState, bool>)(sst =>
                {
                    return false;
                }),

                processElements: processElements,
                previous: st
            );

            // Render the template:
            StringBuilder sbResult = await importedTemplate.Render().ConfigureAwait(continueOnCapturedContext: false);
            string blob = sbResult.ToString();

            // Write the template to our current writer:
            st.Writer.Append(blob);
        }

        private static string moveToNextAreaElement(RenderState st, bool suppressErrors = false)
        {
            // Now, back on the cms-import-template element from the parent blob, read up to the first child element:
            do
            {
                // Skip the opening element:
                if ((st.Reader.NodeType == System.Xml.XmlNodeType.Element) && (st.Reader.LocalName == "cms-import-template"))
                {
                    if (!st.Reader.IsEmptyElement) continue;
                    // Empty?
                    return null;
                }

                // Early out case:
                if ((st.Reader.NodeType == System.Xml.XmlNodeType.EndElement) && (st.Reader.LocalName == "cms-import-template"))
                    return null;

                if (st.Reader.NodeType == System.Xml.XmlNodeType.Element)
                {
                    // Only <area /> elements are allowed within <cms-import-template />.
                    if (st.Reader.LocalName != "area")
                    {
                        if (!suppressErrors) st.Error("cms-import-template may only contain 'area' elements");
                        st.SkipElementAndChildren(st.Reader.LocalName);
                        return null;
                    }

                    // Need an 'id' attribute:
                    if (!st.Reader.MoveToAttribute("id"))
                    {
                        if (!suppressErrors) st.Error("area element must have an 'id' attribute");
                        st.Reader.MoveToElement();
                        st.SkipElementAndChildren("area");
                        return null;
                    }

                    string id = st.Reader.Value;
                    st.Reader.MoveToElement();

                    // Return the new area's id:
                    return id;
                }
            } while (st.Reader.Read());

            return null;
        }
    }
}
