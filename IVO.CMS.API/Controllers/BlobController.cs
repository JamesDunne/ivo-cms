using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using DiffPlex;
using DiffPlex.DiffBuilder;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Errors;
using IVO.Definition.Models;
using IVO.Definition.Containers;

namespace IVO.CMS.API.Controllers
{
    public class BlobController : TaskAsyncController
    {
        #region Private implementation

        private CMSContext cms;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            this.cms = new CMSContext(new DirectoryInfo(Server.MapPath("~/ivo/")));

            base.OnActionExecuting(filterContext);
        }

        private JsonResult ErrorJson<T>(Errorable<T> errored)
        {
            return Json(new { errors = errored.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        [HttpGet]
        [ActionName("get")]
        [JsonHandleError]
        public async Task<ActionResult> GetBlob(Errorable<BlobID.Partial> id)
        {
            if (id.HasErrors) return ErrorJson(id);

            var eid = await cms.blrepo.ResolvePartialID(id.Value);
            if (eid.HasErrors) return ErrorJson(eid);

            var eblob = await cms.blrepo.GetBlob(eid.Value);
            if (eblob.HasErrors) return ErrorJson(eblob);

            return new StreamedBlobResult(eblob.Value);
        }

        [HttpGet]
        [ActionName("getByPath")]
        [JsonHandleError]
        public async Task<ActionResult> GetBlobByPath(Errorable<TreeBlobPath> rootedPath)
        {
            if (rootedPath.HasErrors) return ErrorJson(rootedPath);

            var eblob = await cms.tpsbrepo.GetBlobByTreePath(rootedPath.Value);
            if (eblob.HasErrors) return ErrorJson(eblob);

            TreePathStreamedBlob blob = eblob.Value;
            Debug.Assert(blob != null);

            return new StreamedBlobResult(blob.StreamedBlob);
        }

        [HttpPost]
        [ActionName("create")]
        [JsonHandleError]
        public async Task<ActionResult> CreateBlob(Errorable<TreeBlobPath> path, Errorable<StageName> stage = null)
        {
            if (path.HasErrors) return ErrorJson(path);
            if (stage != null && stage.HasErrors) return ErrorJson(stage);

            PersistingBlob pbl = new PersistingBlob(Request.InputStream);

            // Persist the blob from the input stream:
            var eblob = await cms.blrepo.PersistBlob(pbl);
            if (eblob.HasErrors) return ErrorJson(eblob);

            // Now update the given root TreeID:
            var tbp = path.Value;

            // 1) get minimal set of tree nodes recursively from root to leaf where the BlobID should be updated
            var etrnodes = await cms.trrepo.GetTreeNodesAlongPath(new TreeTreePath(tbp.RootTreeID, tbp.Path.Tree));
            if (etrnodes.HasErrors) return ErrorJson(etrnodes);
            var trnodes = etrnodes.Value;
            // NOTE: trnodes[0] is the root TreeNode.
            //       trnodes[1] is the first named path component.

            // 2) update leaf tree node with new blob information
            //    add or update blob
            //    add new tree nodes as appropriate
            TreeID newRootTreeID;

            // Check if we have the full tree path already:
            if (trnodes.Length == tbp.Path.Tree.Parts.Count + 1)
            {
                // Easy case - update blob info in-place and update TreeIDs on the way back up to root:
                TreeNode.Builder tnb;

                // Jump to the last tree node since that one contains the blob reference and make a builder from it:
                tnb = new TreeNode.Builder(trnodes[trnodes.Length]);

                // Find the index of the blob reference with the blob name:
                int blidx = tnb.Blobs.FindIndex(trbl => trbl.Name == tbp.Path.Name);
                if (blidx != -1)
                {
                    // Update the blob reference for our TreeNode builder in-place over the existing blob reference:
                    var trblb = new TreeBlobReference.Builder(tnb.Blobs[blidx]);
                    trblb.BlobID = eblob.Value.ID;
                    tnb.Blobs[blidx] = trblb;
                }
                else
                {
                    // Add the new blob reference:
                    tnb.Blobs.Add(new TreeBlobReference.Builder(tbp.Path.Name, eblob.Value.ID));
                }

                // Now let's keep track of which new TreeNodes we will persist:
                List<TreeNode> updateNodes = new List<TreeNode>(tbp.Path.Tree.Parts.Count + 1);
                
                TreeNode lastNode = tnb;
                updateNodes.Add(lastNode);

                // 3) update each TreeNode with new TreeID of child TreeNode that was mutated
                for (int i = tbp.Path.Tree.Parts.Count - 1; i >= 0; --i)
                {
                    tnb = new TreeNode.Builder(trnodes[i]);

                    // Find the index of the tree reference for the current path component's name:
                    int tridx = tnb.Trees.FindIndex(trtr => trtr.Name == tbp.Path.Tree.Parts[i]);
                    Debug.Assert(tridx != -1);

                    // Create a builder to mutate the tree reference:
                    var trtrb = new TreeTreeReference.Builder(tnb.Trees[tridx]);
                    // Update the TreeID for the child TreeNode:
                    trtrb.TreeID = lastNode.ID;
                    tnb.Trees[tridx] = trtrb;

                    // Convert the builder to an immutable one (and compute its TreeID):
                    lastNode = tnb;
                    // Add this new TreeNode to the list of TreeNodes to create:
                    updateNodes.Add(lastNode);
                }

                // 4) persist all new tree nodes
                newRootTreeID = lastNode.ID;
                var eptr = await cms.trrepo.PersistTree(newRootTreeID, new ImmutableContainer<TreeID, TreeNode>(tr => tr.ID, updateNodes));
                if (eptr.HasErrors) return ErrorJson(eptr);
            }
            else
            {
                // Only part of the path exists:
                throw new NotImplementedException();
            }

            // optional 5) update stage with new root TreeID
            if (stage != null)
            {
                var epst = await cms.strepo.PersistStage(new Stage.Builder(stage.Value, newRootTreeID));
                if (epst.HasErrors) return ErrorJson(epst);
            }

            // Return the new information:
            return Json(new { blobid = eblob.Value.ID.ToString(), treeid = newRootTreeID.ToString() });
        }

        [HttpPost]
        [ActionName("validate")]
        [JsonHandleError]
        public ActionResult ValidateBlob()
        {
            // Validate the blob's contents:
            var vr = cms.GetContentEngine().ValidateBlob(Request.InputStream);

            // Return the validation results:
            return Json(new
            {
                successful = (vr == null),
                error = (vr == null) ? (object)null : new { message = vr.Message, lineNumber = vr.LineNumber, linePosition = vr.LinePosition }
            });
        }

        [HttpGet]
        [ActionName("compare")]
        [JsonHandleError]
        public async Task<ActionResult> CompareBlobs(Errorable<BlobID.Partial> id, Errorable<BlobID.Partial> against)
        {
            if (id.HasErrors || against.HasErrors) return Json(new { errors = (id.Errors + against.Errors).ToJSON() }, JsonRequestBehavior.AllowGet);

            // Resolve the partial IDs:
            var eids = await cms.blrepo.ResolvePartialIDs(id.Value, against.Value);
            if (eids[0].HasErrors || eids[1].HasErrors) return Json(new { errors = (eids[0].Errors + eids[1].Errors).ToJSON() }, JsonRequestBehavior.AllowGet);

            BlobID idA = eids[0].Value;
            BlobID idB = eids[1].Value;

            // Get the Blobs:
            var ebls = await cms.blrepo.GetBlobs(idA, idB);
            if (ebls[0].HasErrors || ebls[1].HasErrors) return Json(new { errors = (ebls[0].Errors + ebls[1].Errors).ToJSON() }, JsonRequestBehavior.AllowGet);

            IStreamedBlob blA = ebls[0].Value, blB = ebls[1].Value;

            // Stream in both blobs' contents to string values:
            var etextA = await blA.ReadStreamAsync<string>(async st => { using (var sr = new StreamReader(st, Encoding.UTF8)) return (Errorable<string>)await sr.ReadToEndAsync(); });
            if (etextA.HasErrors) return ErrorJson(etextA);
            
            var etextB = await blB.ReadStreamAsync<string>(async st => { using (var sr = new StreamReader(st, Encoding.UTF8)) return (Errorable<string>)await sr.ReadToEndAsync(); });
            if (etextB.HasErrors) return ErrorJson(etextB);

            // Create a diff engine:
            IDiffer differ = new Differ();
            ISideBySideDiffBuilder builder = new SideBySideDiffBuilder(differ);

            // Run the diff engine to get the output model:
            // NOTE: I would prefer it to read in via a Stream but perhaps that is not possible given the algorithm implemented.
            var sxs = builder.BuildDiffModel(etextA.Value, etextB.Value);

            // Return the result as JSON:
            return Json(sxs.ToJSON(), JsonRequestBehavior.AllowGet);
        }
    }
}
