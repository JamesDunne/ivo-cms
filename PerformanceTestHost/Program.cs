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
        const string getURL1 = "http://localhost/blob/get/blob/91a97f8a57480e24f710cabc675636d4b9c3a197";
        const string getURL2 = "http://localhost/render/tree/84fff720f95ef66fe6dff04b62c36d74ca39d1c4:pages/helloWorld";
        const string getURL3 = "http://localhost/blob/compare/91a97f8a57480e24f710cabc675636d4b9c3a197/0d0a6d675674459214d970ebb9f7b0535313230e";
        static bool displayResponse = false;

        static void Main(string[] args)
        {
            var pr = new Program();

#if true
            int count = 3, per = 250;
            displayResponse = false;
#else
            int count = 1, per = 1;
            displayResponse = true;
#endif

#if true
            Console.WriteLine("POST blob/create");
            pr.TimeRequests(createPOSTRequest1, count * 100, 1).Wait();
#endif
#if true
            Console.WriteLine("POST blob/create");
            pr.TimeRequests(createPOSTRequest2, count * 100, 1).Wait();
#endif
#if true
            Console.WriteLine("POST commit/create/jsd/master/HEAD");
            pr.TimeRequests(createPOSTRequest3, 3, 1).Wait();
#endif
#if true
            Console.WriteLine("GET {0}", getURL1.Remove(0, "http://localhost/".Length));
            pr.TimeRequests(createGETRequest1, count, per).Wait();

            Console.WriteLine("GET {0}", getURL2.Remove(0, "http://localhost/".Length));
            pr.TimeRequests(createGETRequest2, count, per).Wait();

            Console.WriteLine("GET {0}", getURL3.Remove(0, "http://localhost/".Length));
            pr.TimeRequests(createGETRequest3, count, per).Wait();
#endif
        }

        private static async Task<string> readResponse(HttpWebRequest rq, string expectedContentType)
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

            string[] contentType = rsp.ContentType.Split(new string[1] { "; " }, StringSplitOptions.None);
            if (contentType[0] != expectedContentType) Console.Error.WriteLine("Received Content-Type '{0}', expected '{1}'", contentType[0], expectedContentType);

            using (var st = rsp.GetResponseStream())
                using (var tr = new StreamReader(st, Encoding.UTF8))
                {
                    string rspstr = await tr.ReadToEndAsync();
                    if (displayResponse)
                    {
                        Console.Out.WriteLineAsync(rspstr);
                    }
                    return rspstr;
                }
        }

        private static async Task createPOSTRequest1()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/blob/create/templates/main");
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

            string rsp = await readResponse(rq, "application/json");
            // {"blobid":"91a97f8a57480e24f710cabc675636d4b9c3a197","treeid":"c9bf735654356fa45a66a15e512dae6ef9fbbd15"}
        }

        private static async Task createPOSTRequest2()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/blob/create/c9bf735654356fa45a66a15e512dae6ef9fbbd15:pages/helloWorld");
            rq.Method = "POST";
            rq.Accept = "application/json";
            rq.ContentType = "application/xhtml+xml";
            byte[] msg = Encoding.UTF8.GetBytes(
@"<cms-import-template path=""/templates/main"">
  <area id=""head""></area>
  <area id=""body""><div>
    Hello, world! This is a templated page.
  </div></area>
</cms-import-template>");

            using (var rqs = await rq.GetRequestStreamAsync())
                await rqs.WriteAsync(msg, 0, msg.Length);

            string rsp = await readResponse(rq, "application/json");

            // {"blobid":"0d0a6d675674459214d970ebb9f7b0535313230e","treeid":"84fff720f95ef66fe6dff04b62c36d74ca39d1c4"}
        }

        private static async Task createPOSTRequest3()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create("http://localhost/commit/create/jsd/master/HEAD");
            rq.Method = "POST";
            rq.Accept = "application/json";
            rq.ContentType = "application/json";

            string json = new JavaScriptSerializer().Serialize(new
            {
                treeid = "84fff720f95ef66fe6dff04b62c36d74ca39d1c4",
                parents = new string[0],
                committer = "James S. Dunne <james.jdunne@gmail.com>",
                date_committed = DateTimeOffset.Now.ToString("s"),
                message = "Test commit."
            });
            byte[] msg = Encoding.UTF8.GetBytes(json);

            using (var rqs = await rq.GetRequestStreamAsync())
                await rqs.WriteAsync(msg, 0, msg.Length);

            string rsp = await readResponse(rq, "application/json");
        }

        private static async Task createGETRequest1()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create(getURL1);
            rq.Method = "GET";
            rq.Accept = "*/*";

            string rsp = await readResponse(rq, "application/xhtml+xml");
        }

        private static async Task createGETRequest2()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create(getURL2);
            rq.Method = "GET";
            rq.Accept = "*/*";

            string rsp = await readResponse(rq, "application/xhtml+xml");
        }

        private static async Task createGETRequest3()
        {
            HttpWebRequest rq = (HttpWebRequest)HttpWebRequest.Create(getURL3);
            rq.Method = "GET";
            rq.Accept = "*/*";

            string rsp = await readResponse(rq, "application/json");
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

                await Task.WhenAll(rt);
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
