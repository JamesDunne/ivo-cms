using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestCMS;

namespace PerformanceTestHost
{
    class Program
    {
        static void Main(string[] args)
        {
            new FileSystemTests().SpeedTestRenderBlob();
        }
    }
}
