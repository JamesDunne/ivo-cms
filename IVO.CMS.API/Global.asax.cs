using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

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
            routes.MapRoute(
                "Render",
                "render/tree/{*rootedPath}",
                new { controller = "Render", action = "renderByTree" },
                new { rootedPath = @"[\w\d]{40}/.*" }
            );

            // BlobController
            routes.MapRoute(
                "BlobGetByPath",
                "blob/get/tree/{*rootedPath}",
                new { controller = "Blob", action = "getByPath" },
                new { rootedPath = @"[\w\d]{40}/.*" }
            );

            routes.MapRoute(
                "BlobGetByID",
                "blob/get/blob/{id}",
                new { controller = "Blob", action = "get" }
            );

            // CommitController
            routes.MapRoute(
                "CommitGetByID",
                "commit/get/id/{id}",
                new { controller = "Commit", action = "getByID" }
            );

            routes.MapRoute(
                "CommitGetByTag",
                "commit/get/tag/{*tagName}",
                new { controller = "Commit", action = "getByTag" }
            );

            routes.MapRoute(
                "CommitGetByRef",
                "commit/get/ref/{*refName}",
                new { controller = "Commit", action = "getByRef" }
            );

            routes.MapRoute(
                "CommitTreeGet",
                "commit/tree/id/{id}",
                new { controller = "Commit", action = "getTree" }
            );

            routes.MapRoute(
                "CommitTreeGetByTag",
                "commit/tree/tag/{*tagName}",
                new { controller = "Commit", action = "getTreeByTag" }
            );

            routes.MapRoute(
                "CommitTreeGetByRef",
                "commit/tree/ref/{*refName}",
                new { controller = "Commit", action = "getTreeByRef" }
            );

            routes.MapRoute(
                "CommitCreate",
                "commit/create/{*refName}",
                new { controller = "Commit", action = "create" }
            );

            // TagController
            routes.MapRoute(
                "TagGetByID",
                "tag/get/id/{id}",
                new { controller = "Tag", action = "getByID" }
            );

            routes.MapRoute(
                "TagGetByName",
                "tag/get/name/{*tagName}",
                new { controller = "Tag", action = "getByName" }
            );

            // RefController
            routes.MapRoute(
                "RefGetByName",
                "ref/get/name/{*refName}",
                new { controller = "Ref", action = "getByName" }
            );


            // Default
            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RegisterRoutes(RouteTable.Routes);
        }
    }
}