using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(BraintreeSimpleServer.Startup))]
namespace BraintreeSimpleServer
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
