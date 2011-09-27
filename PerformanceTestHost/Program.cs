using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TestCMS;
using System.Diagnostics;

namespace PerformanceTestHost
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().Requests().Wait();
        }

        private async Task Requests()
        {
            const int per = 4;

            HttpWebRequest[] rq = new HttpWebRequest[per];
            Task<WebResponse>[] rspt = new Task<WebResponse>[per];
            
            for (int i = 0; i < per; ++i)
            {
                rq[i] = (HttpWebRequest)HttpWebRequest.Create("http://localhost:52729/commit/tree/ref/HEAD");
                rq[i].Accept = "application/json";
                rq[i].Method = "GET";
                rq[i].Pipelined = false;
            }

            const int count = 8192;
            
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < count; ++i)
            {
                for (int j = 0; j < per; ++j)
                {
                    rspt[j] = rq[j].GetResponseAsync();
                }
                
                var rsp = await TaskEx.WhenAll(rspt);
                for (int j = 0; j < per; ++j)
                    rsp[j].Close();
            }
            sw.Stop();

            Console.WriteLine("Total {0} requests in {1} msec = {2} req/sec", count * per, sw.ElapsedMilliseconds, (count * per) / (sw.ElapsedMilliseconds / 1000d));
        }
    }
}
