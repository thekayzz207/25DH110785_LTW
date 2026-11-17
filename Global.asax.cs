using System;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using LTW_25DH110785.Models;

namespace LTW_25DH110785
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            MvcHandler.DisableMvcResponseHeader = true;
            ModelBinders.Binders.Add(typeof(decimal), new DecimalModelBinder());
            ModelBinders.Binders.Add(typeof(decimal?), new DecimalModelBinder());
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            if (Context.User == null || !Context.User.Identity.IsAuthenticated) return;
            using (var db = new StoreDbContext())
            {
                var account = db.Users.Find(Context.User.Identity.Name);
                if (account == null)
                {
                    FormsAuthentication.SignOut(); Context.User = new GenericPrincipal(new GenericIdentity(""), new string[0]);
                    System.Threading.Thread.CurrentPrincipal = Context.User; return;
                }
                var principal = new GenericPrincipal(Context.User.Identity,new[] { account.UserRole });
                Context.User=principal; System.Threading.Thread.CurrentPrincipal=principal;
            }
        }
    }
}
