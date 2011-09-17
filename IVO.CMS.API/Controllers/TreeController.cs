using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Models;
using IVO.Definition.Containers;
using IVO.CMS.API.Models;
using System.Collections.Generic;

namespace IVO.CMS.API.Controllers
{
    public class TreeController : TaskAsyncController
    {
        private CMSContext cms;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            this.cms = new CMSContext(new DirectoryInfo(Server.MapPath("~/ivo/")));

            base.OnActionExecuting(filterContext);
        }

        [HttpGet]
        [ActionName("get")]
        public async Task<ActionResult> GetTreeByID(TreeID id)
        {
            var tree = await cms.trrepo.GetTree(id);

            return Json(new { tree }, JsonRequestBehavior.AllowGet);
        }

        private static Tree[] convertRecursively(TreeModel tm)
        {
            Tree.Builder tb = new Tree.Builder(new List<TreeTreeReference>(tm.Blobs.Length), new List<TreeBlobReference>(tm.Trees.Length));
            
            tb.Blobs.AddRange(from bl in tm.Blobs select (TreeBlobReference)new TreeBlobReference.Builder(bl.Name, bl.BlobID));

            // Create our output list:
            List<Tree> trees = new List<Tree>(1 + tm.Trees.Length /* + more, could calculate recursively but why bother */);
            
            // Dummy placeholder for this Tree:
            trees.Add((Tree)null);
            for (int i = 0; i < tm.Trees.Length; ++i)
            {
                // Convert the child trees:
                Tree[] childTrees = convertRecursively(tm.Trees[i].Tree);
                // Add them to the output list:
                trees.AddRange(childTrees);
                // Add the child TreeTreeReference to this Tree.Builder:
                tb.Trees.Add(new TreeTreeReference.Builder(tm.Trees[i].Name, childTrees[0].ID));
            }

            trees[0] = tb;
            return trees.ToArray();
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> CreateTree(TreeModel tm)
        {
            TreeID root;
            ImmutableContainer<TreeID, Tree> trees;
            
            // Recursively convert the JSON-friendly `TreeModel` into our domain-friendly `Tree`s:
            Tree[] treeArr = convertRecursively(tm);

            root = treeArr[0].ID;
            trees = new ImmutableContainer<TreeID, Tree>(tr => tr.ID, treeArr);

            // Persist the tree:
            var tree = await cms.trrepo.PersistTree(root, trees);

            // Return the array of `Tree` models we persisted:
            return Json(new { trees = treeArr });
        }
    }
}
