using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using IVO.Definition.Errors;
using IVO.Definition.Models;

namespace IVO.CMS.API
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // RenderController
            routes.MapRoute("Render",
                "render/tree/{*epath}",
                new { controller = "Render", action = "renderByTree" }
            );


            // BlobController
            routes.MapRoute("BlobGetByPath",
                "blob/get/tree/{*epath}",
                new { controller = "Blob", action = "getByPath" }
            );

            routes.MapRoute("BlobGetByID",
                "blob/get/blob/{epid}",
                new { controller = "Blob", action = "get" }
            );

            routes.MapRoute("BlobCompare",
                "blob/compare/{epida}/{epidb}",
                new { controller = "Blob", action = "compare" }
            );

            routes.MapRoute("BlobCreate",
                "blob/create/{*epath}",
                new { controller = "Blob", action = "create" }
            );


            // TreeController
            routes.MapRoute("TreeGetByID",
                "tree/get/id/{epid}",
                new { controller = "Tree", action = "getByID" }
            );

            routes.MapRoute("TreeCreate",
                "tree/create",
                new { controller = "Tree", action = "create" }
            );


            // StageController
            routes.MapRoute("StageGetByName",
                "stage/get/name/{*estageName}",
                new { controller = "Stage", action = "getByName" }
            );

            routes.MapRoute("StageCreate",
                "stage/create",
                new { controller = "Stage", action = "create" }
            );


            // CommitController
            routes.MapRoute("CommitGetByID",
                "commit/get/id/{epid}",
                new { controller = "Commit", action = "getByID" }
            );

            routes.MapRoute("CommitGetByTag",
                "commit/get/tag/{*etagName}",
                new { controller = "Commit", action = "getByTag" }
            );

            routes.MapRoute("CommitGetByRef",
                "commit/get/ref/{*erefName}",
                new { controller = "Commit", action = "getByRef" }
            );

            routes.MapRoute("CommitTreeGet",
                "commit/tree/id/{epid}",
                new { controller = "Commit", action = "getTree" }
            );

            routes.MapRoute("CommitTreeGetByTag",
                "commit/tree/tag/{*etagName}",
                new { controller = "Commit", action = "getTreeByTag" }
            );

            routes.MapRoute("CommitTreeGetByRef",
                "commit/tree/ref/{*erefName}",
                new { controller = "Commit", action = "getTreeByRef" }
            );

            routes.MapRoute("CommitCreate",
                "commit/create/{*erefName}",
                new { controller = "Commit", action = "create" }
            );


            // TagController
            routes.MapRoute("TagGetByID",
                "tag/get/id/{epid}",
                new { controller = "Tag", action = "getByID" }
            );

            routes.MapRoute("TagGetByName",
                "tag/get/name/{*etagName}",
                new { controller = "Tag", action = "getByName" }
            );

            routes.MapRoute("TagCreate",
                "tag/create",
                new { controller = "Tag", action = "create" }
            );


            // RefController
            routes.MapRoute("RefGetByName",
                "ref/get/name/{*erefName}",
                new { controller = "Ref", action = "getByName" }
            );

            routes.MapRoute("RefGetAll",
                "ref/all",
                new { controller = "Ref", action = "getAll" }
            );

            routes.MapRoute("RefCreate",
                "ref/create",
                new { controller = "Ref", action = "create" }
            );

#if false
            // Default
            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );
#endif
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RegisterRoutes(RouteTable.Routes);

            ModelBinderProviders.BinderProviders.Add(new Code.ErrorableModelBinderProvider());
        }
    }
}