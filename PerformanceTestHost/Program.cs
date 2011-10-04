using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TestCMS;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;

namespace PerformanceTestHost
{
    class Program
    {
        const string getURL1 = "http://localhost/blob/get/91a97f8a57480e24f710cabc675636d4b9c3a197";
        const string getURL2 = "http://localhost/render/tree/56d4d6806f048c304fc68303c90955de6115f256/pages/home";

        static void Main(string[] args)
        {
            var pr = new Program();

            Console.WriteLine("POST blob/create");
            pr.TimeRequests(createPOSTRequest1, count: 3, per: 250).Wait();
            Console.WriteLine("POST blob/create");
            pr.TimeRequests(createPOSTRequest2, count: 3, per: 250).Wait();
            Console.WriteLine("POST tree/create");
            pr.TimeRequests(createPOSTRequest3, count: 3, per: 250).Wait();
            Console.WriteLine("GET {0}", getURL1.Remove(0, "http://localhost/".Length));
            pr.TimeRequests(createGETRequest1, count: 3, per: 250).Wait();
            Console.WriteLine("GET {0}", getURL2.Remove(0, "http://localhost/".Length));
            pr.TimeRequests(createGETRequest2, count: 3, per: 250).Wait();
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
#if false
            using (var tr = new StreamReader(st, Encoding.UTF8))
            {
                string line;
                while ((line = await tr.ReadLineAsync()) != null)
                {
                    await Console.Out.WriteAsync(line + Environment.NewLine);
                }
            }
#else
                st.Close();
#endif
        }

        private static async Task createPOSTRequest1()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/blob/create");
            rq.Method = "POST";
            rq.Accept = "application/json;*/*";
            rq.ContentType = "application/xhtml+xml";
            byte[] msg = Encoding.UTF8.GetBytes(
@"<cms-template><cms-doctype type=""html"" />
<html>
  <head><cms-template-area id=""head"" /></head>
  <body><cms-template-area id=""body"" /></body>
</html></cms-template>");

            using (var rqs = await rq.GetRequestStreamAsync())
                await rqs.WriteAsync(msg, 0, msg.Length);

            await readResponse(rq);
        }

        private static async Task createPOSTRequest2()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/blob/create");
            rq.Method = "POST";
            rq.Accept = "application/json";
            rq.ContentType = "application/xhtml+xml";
            byte[] msg = Encoding.UTF8.GetBytes(
@"<cms-import-template path=""/templates/page"">
  <area id=""head""></area>
  <area id=""body""><div>
    Hello, world! This is a templated page.
  </div></area>
</cms-import-template>");

            using (var rqs = await rq.GetRequestStreamAsync())
                await rqs.WriteAsync(msg, 0, msg.Length);

            await readResponse(rq);
        }

        private static async Task createPOSTRequest3()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/tree/create");
            rq.Method = "POST";
            rq.Accept = "application/json";
            rq.ContentType = "application/json";

            string json = new JavaScriptSerializer().Serialize(new
            {
                blobs = new object[0],
                trees = new[] {
                    new {
                        name = "templates",
                        tree = new {
                            blobs = new[] {
                                new { name = "page", blobid = "91a97f8a57480e24f710cabc675636d4b9c3a197" }
                            },
                            trees = new object[0]
                        }
                    },
                    new {
                        name = "pages",
                        tree = new {
                            blobs = new[] {
                                new { name = "home", blobid = "e4ed960dead1600353ce2314df790ab376a9de7c" }
                            },
                            trees = new object[0]
                        }
                    }
                }
            });
            byte[] msg = Encoding.UTF8.GetBytes(json);

            using (var rqs = await rq.GetRequestStreamAsync())
                await rqs.WriteAsync(msg, 0, msg.Length);

            await readResponse(rq);
        }

        private static async Task createGETRequest1()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create(getURL1);
            rq.Method = "GET";
            rq.Accept = "*/*";

            await readResponse(rq);
        }

        private static async Task createGETRequest2()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create(getURL2);
            rq.Method = "GET";
            rq.Accept = "*/*";

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

                Console.WriteLine("      {0,5} req in {1,5} ms = {2,7:###0.00} req/sec, {3,7:#0.0000} ms/req", per, em, (per * 1000d) / (em), (double)em / per);
                lastTicks = currTicks;
            }
            sw.Stop();

            Console.WriteLine("Total {0,5} req in {1,5} ms = {2,7:###0.00} req/sec, {3,7:#0.0000} ms/req", count * per, sw.ElapsedMilliseconds, (count * per * 1000d) / (sw.ElapsedMilliseconds), (double)sw.ElapsedMilliseconds / (count * per));
        }
    }
}
