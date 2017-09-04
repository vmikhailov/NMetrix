using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMetrics.TestClasses
{
    public class TestClass01<T> : TestClass00
    {
        public TestClass01()
            : base(null)
        {
        }

        public void TestCreation()
        {
            var x = new TestClass00(null);
        }

        public void TestCreation(ITestInterface interf)
        {
            var x = new TestClass00(interf);
        }
    }
}
