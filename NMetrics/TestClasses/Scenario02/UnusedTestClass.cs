using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMetrics.TestClasses.Scenario02
{
    public class UnusedTestClass
    {
        public UnusedTestClass()
        {
            new TestDataModel();
            new TestDataRepository();
        }
    }
}
