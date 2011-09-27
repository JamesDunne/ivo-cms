using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TestCMS;
using System.Diagnostics;
using System.IO;

namespace PerformanceTestHost
{
    class Program
    {
        static void Main(string[] args)
        {
            var pr = new Program();

            pr.TimeRequests(createPOSTRequest, count: 4).Wait();
            pr.TimeRequests(createPOST2Request, count: 4).Wait();
            pr.TimeRequests(createGETRequest1).Wait();
            pr.TimeRequests(createGETRequest2).Wait();
        }

        private static async Task readResponse(HttpWebRequest rq)
        {
            HttpWebResponse rsp;
            try
            {
                rsp = (HttpWebResponse)await rq.GetResponseAsync();
            }
            catch (WebException wex)
            {
                rsp = (HttpWebResponse)wex.Response;
            }

            if (rsp.StatusCode != HttpStatusCode.OK) Console.Error.WriteLine(rsp.StatusCode.ToString());

            using (var st = rsp.GetResponseStream())
            using (var tr = new StreamReader(st, Encoding.UTF8))
            {
                string line;
                while ((line = await tr.ReadLineAsync()) != null)
                {
                    //await Console.Out.WriteAsync(line + Environment.NewLine);
                }
            }
        }

        private static async Task createPOST2Request()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/blob/create");
            rq.Method = "POST";
            rq.Accept = "application/json";
            rq.ContentType = "application/xhtml+xml";
            DateTimeOffset now = DateTimeOffset.Now;
            now = now.Date.AddHours(now.Hour);

            string msg =
                String.Format(
@"<cms-scheduled>
  <range from=""{0}"" to=""{1}"" />
  <content>Hello world for one day!</content>
</cms-scheduled>",
                    now.ToString("u"),
                    now.AddDays(1).ToString("u")
                );

            byte[] msgb = Encoding.UTF8.GetBytes(msg);
            //Console.WriteLine(msg);

            using (var rqs = await rq.GetRequestStreamAsync())
                await rqs.WriteAsync(msgb, 0, msgb.Length);

            await readResponse(rq);
        }

        private static async Task createPOSTRequest()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/tree/create");
            rq.Method = "POST";
            rq.Accept = "application/json";
            rq.ContentType = "application/json";
            byte[] msg = Encoding.UTF8.GetBytes("{\"blobs\":[{name:\"README\",\"blobid\":\"a2c85c6c7be6fcd752ac6f55c1f078bd242c23ff\"}],\"trees\":[]}");

            using (var rqs = await rq.GetRequestStreamAsync())
                await rqs.WriteAsync(msg, 0, msg.Length);

            await readResponse(rq);
        }

        private static async Task createGETRequest1()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/blob/get/a2c85c6c7be6fcd752ac6f55c1f078bd242c23ff");
            rq.Method = "GET";
            rq.Accept = "application/json";

            await readResponse(rq);
        }

        private static async Task createGETRequest2()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/render/tree/3f548219f220bdc07b2bac7db0208dfc8a3c4869/README");
            rq.Method = "GET";
            rq.Accept = "application/json";

            await readResponse(rq);
        }

        private async Task TimeRequests(Func<Task> createRequest, int count = 16, int per = 512)
        {
            Task[] rt = new Task[per];

            Console.WriteLine();

            Stopwatch sw = Stopwatch.StartNew();
            long lastTicks = sw.ElapsedTicks;
            for (int i = 0; i < count; ++i)
            {
                for (int j = 0; j < per; ++j)
                {
                    rt[j] = createRequest();
                }

                await TaskEx.WhenAll(rt);
                long currTicks = sw.ElapsedTicks;
                long em = ((currTicks - lastTicks) * 1000) / Stopwatch.Frequency;

                Console.WriteLine("      {0} requests in {1} msec = {2} req/sec, {3} msec/req", per, em, (per * 1000d) / (em), (double)em / per);
                lastTicks = currTicks;
            }
            sw.Stop();

            Console.WriteLine("Total {0} requests in {1} msec = {2} req/sec, {3} msec/req", count * per, sw.ElapsedMilliseconds, (count * per * 1000d) / (sw.ElapsedMilliseconds), (double)sw.ElapsedMilliseconds / (count * per));
        }
    }
}
