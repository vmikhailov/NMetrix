using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace NMetrics.TestClasses.Scenario02
{
    public class TestController : IHttpController
    {
        public async Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}