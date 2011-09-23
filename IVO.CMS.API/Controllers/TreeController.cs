using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Containers;
using IVO.Definition.Models;

namespace IVO.CMS.API.Controllers
{
    public class TreeController : TaskAsyncController
    {
        #region Private implementation

        private CMSContext cms;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            this.cms = new CMSContext(new DirectoryInfo(Server.MapPath("~/ivo/")));

            base.OnActionExecuting(filterContext);
        }

        private static TreeResponse projectTreeJSON(Tree tree)
        {
            return new TreeResponse
            {
                id = tree.ID.ToString(),
                blobs = tree.Blobs.SelectAsArray(bl => new TreeBlobRefResponse { name = bl.Name, blobid = bl.BlobID.ToString() }),
                trees = tree.Trees.SelectAsArray(tr => new TreeTreeRefResponse { name = tr.Name, treeid = tr.TreeID.ToString() })
            };
        }

        private static TreeResponse projectTreeJSON(TreeID rootid, ImmutableContainer<TreeID, Tree> trees)
        {
            Tree tree;
            if (!trees.TryGetValue(rootid, out tree)) return null;

            return new TreeResponse
            {
                id = tree.ID.ToString(),
                blobs = tree.Blobs.SelectAsArray(bl => new TreeBlobRefResponse { name = bl.Name, blobid = bl.BlobID.ToString() }),
                trees = tree.Trees.SelectAsArray(tr => new TreeTreeRefResponse { name = tr.Name, treeid = tr.TreeID.ToString(), tree = projectTreeJSON(tr.TreeID, trees) })
            };
        }

        private static Tree[] convertRecursively(TreeRequest tm)
        {
            int treeCount = tm.trees != null ? tm.trees.Length : 0;
            int blobCount = tm.blobs != null ? tm.blobs.Length : 0;

            Tree.Builder tb = new Tree.Builder(
                new List<TreeTreeReference>(treeCount),
                new List<TreeBlobReference>(blobCount)
            );

            // Add the blobs to the Tree.Builder:
            if ((tm.blobs != null) && (blobCount > 0))
                tb.Blobs.AddRange(from bl in tm.blobs select (TreeBlobReference)new TreeBlobReference.Builder(bl.name, BlobID.Parse(bl.blobid).Value));

            // Create our output list:
            List<Tree> trees = new List<Tree>(1 + treeCount /* + more, could calculate recursively but why bother */);

            // Dummy placeholder for this Tree:
            trees.Add((Tree)null);
            for (int i = 0; i < treeCount; ++i)
            {
                // If we have a `treeid` then skip recursion:
                if (!String.IsNullOrEmpty(tm.trees[i].treeid))
                {
                    tb.Trees.Add(new TreeTreeReference.Builder(tm.trees[i].name, TreeID.Parse(tm.trees[i].treeid).Value));
                    continue;
                }

                // Convert the child trees:
                Tree[] childTrees = convertRecursively(tm.trees[i].tree);
                // Add them to the output list:
                trees.AddRange(childTrees);
                // Add the child TreeTreeReference to this Tree.Builder:
                tb.Trees.Add(new TreeTreeReference.Builder(tm.trees[i].name, childTrees[0].ID));
            }

            // Set the first element (was a placeholder) to the built Tree:
            trees[0] = tb;
            return trees.ToArray();
        }

        #endregion

        [HttpGet]
        [ActionName("get")]
        public async Task<ActionResult> GetTreeByID(TreeID id)
        {
            var tree = await cms.trrepo.GetTree(id);

            return Json(new { tree = projectTreeJSON(tree) }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> CreateTree(TreeRequest tm)
        {
            TreeID root;
            ImmutableContainer<TreeID, Tree> trees;
            
            // Recursively convert the JSON-friendly `TreeModel` into our domain-friendly `Tree`s:
            Tree[] treeArr = convertRecursively(tm);

            root = treeArr[0].ID;
            trees = new ImmutableContainer<TreeID, Tree>(tr => tr.ID, treeArr);

            // Persist the tree:
            var tree = await cms.trrepo.PersistTree(root, trees);

            // Return the `Tree` recursive model we persisted:
            return Json(new { tree = projectTreeJSON(root, trees) });
        }
    }
}
