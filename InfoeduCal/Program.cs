using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Newtonsoft.Json;
using Calendar = Ical.Net.Calendar;

namespace InfoeduCal
{
    class Program
    {
        static void Main(string[] args)
        {
            string login_url = "https://student.racunarstvo.hr/pretinac/main.php";
            string table_url = "https://student.racunarstvo.hr/pretinac/main.php?what=raspored_tjedni&week=next&next=";
            string currWeekHtml, currWeekJson, username, password;

            Console.WriteLine("Infoeducal - Infoeduka iCal format converter\nUsername:");
            username = Console.ReadLine();
            Console.WriteLine("Password: ");
            password = Console.ReadLine();

            CookieContainer sessionCookie = SessionCookie(login_url);
            CookieContainer authcookie = AuthCookie(sessionCookie, username, password, login_url);

            List<Dogadaj> allEvents = new List<Dogadaj>();

            for (int week = 17; week <= 25; week++)
            {
                currWeekHtml = GetRasporedHtml(table_url, week, authcookie);
                currWeekJson = ExtractJsonArray(currWeekHtml);
                if (currWeekJson == "")
                {
                    Console.WriteLine("Error reading calendar, bad username/password or server unavailable");
                    Console.ReadKey();
                    Environment.Exit(1);
                }
                var parsedWeek = JsonConvert.DeserializeObject<IEnumerable<Dogadaj>>(currWeekJson).ToList();
                allEvents.AddRange(parsedWeek);
            }

            var calendar = new Calendar();

            for (int i = 0; i < allEvents.Count; i++)
            {
                var dogadaj = calendar.Create<CalendarEvent>();
                dogadaj.Summary = allEvents[i].title;
                dogadaj.Start = new CalDateTime(DateTime.ParseExact(allEvents[i].start, "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture));
                dogadaj.End = new CalDateTime(DateTime.ParseExact(allEvents[i].end, "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture));
            }

            var serializer = new CalendarSerializer();
            string icalString = serializer.SerializeToString(calendar);
            System.IO.File.WriteAllText(@"Kalendar_Infoeduka.ics", icalString);
        }

        static CookieContainer SessionCookie(string url)
        {
            CookieContainer cookies = new CookieContainer();
            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.Method = "GET";
            request.CookieContainer = cookies;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                string stranica = reader.ReadToEnd();
            }

            return request.CookieContainer;
        }

        static CookieContainer AuthCookie(CookieContainer sessionCookie, string user, string pass, string url)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.CookieContainer = sessionCookie;
            HttpClient client = new HttpClient(handler);

            MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(user), "login");
            form.Add(new StringContent(pass), "password");

            HttpResponseMessage response = client.PostAsync(url, form).Result;
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine("Error connecting to Infoeduka");
                Environment.Exit(1);
            }
            return handler.CookieContainer;
        }

        static string GetRasporedHtml(string url, int week, CookieContainer authcookie)
        {
            HttpWebRequest stranica = (HttpWebRequest)WebRequest.Create(url + week.ToString());
            stranica.CookieContainer = authcookie;
            stranica.AutomaticDecompression = DecompressionMethods.GZip;
            stranica.ContentType = "charset=iso-8859-2";
            HttpWebResponse stranicaOdg = (HttpWebResponse)stranica.GetResponse();
            Encoding encoding = Encoding.GetEncoding(28592);
            if (stranicaOdg.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine("Error connecting to Infoeduka");
                Environment.Exit(1);
            }
            StreamReader readStream = new StreamReader(stranicaOdg.GetResponseStream(), encoding);
            string stranicaString = readStream.ReadToEnd();
            stranicaOdg.Close();
            readStream.Close();
            return stranicaString;
        }

        static string ExtractJsonArray(string html)
        {
            int start = html.IndexOf("events: ");
            html = html.Remove(0, start + 8);
            html = html.Substring(0, html.IndexOf("]") + 1);
            return html;

        }
    }

    public class Dogadaj
    {
        public string title;
        public string start;
        public string end;
    }
}
