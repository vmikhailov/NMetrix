using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Linq;
using System.Text;
using System.Web.Http;

namespace NMetrics.TestClasses.Scenario02
{
    public class TestApiController : ApiController
    {
        IConfigurationService configuration;
        ITestDataService service;

        public TestApiController(IConfigurationService configuration, ITestDataService service)
        {
            this.configuration = configuration;
            this.service = service;
        }
    }
}
