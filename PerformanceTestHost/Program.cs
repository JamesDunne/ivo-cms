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
            const int count = 16;
            const int per = 1024;

            HttpWebRequest[] rq = new HttpWebRequest[per];
            Task[] rspt = new Task[per];
            
            const int bufferSize = 512;
            byte[] tmp = new byte[bufferSize];

            Stopwatch sw = Stopwatch.StartNew();
            long lastTicks = sw.ElapsedTicks;
            for (int i = 0; i < count; ++i)
            {
                for (int j = 0; j < per; ++j)
                {
                    rq[j] = (HttpWebRequest)HttpWebRequest.Create("http://localhost/blob/get/a2c85c6c7be6fcd752ac6f55c1f078bd242c23ff");
                    rq[j].Accept = "application/json";
                    rq[j].Method = "GET";
                    rq[j].Pipelined = false;
                    rspt[j] = rq[j].GetResponseAsync().ContinueWith(wrt =>
                    {
                        using (var st = wrt.Result.GetResponseStream())
                        {
                            int nr;
                            while ((nr = st.Read(tmp, 0, bufferSize)) > 0) ;
                        }
                    });
                }

                await TaskEx.WhenAll(rspt);
                long currTicks = sw.ElapsedTicks;
                long em = ((currTicks - lastTicks) * 1000) / Stopwatch.Frequency;
                Console.WriteLine("      {0} requests in {1} msec = {2} req/sec, {3} msec/req", per, em, (per * 1000d) / (em), (double)em / per);
                lastTicks = currTicks;
#if false
                for (int j = 0; j < rsp.Length; ++j)
                {
#if true
                    using (var st = rsp[j].GetResponseStream())
                    {
                        int nr;
                        while ((nr = await st.ReadAsync(tmp, 0, bufferSize)) > 0) ;
                    }
#else
                    rsp[j].Close();
#endif
                }
#endif
            }
            sw.Stop();

            Console.WriteLine("Total {0} requests in {1} msec = {2} req/sec, {3} msec/req", count * per, sw.ElapsedMilliseconds, (count * per * 1000d) / (sw.ElapsedMilliseconds), (double)sw.ElapsedMilliseconds / (count * per));
        }
    }
}
